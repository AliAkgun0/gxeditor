using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public sealed record FileStatistics(long FileSize, long LineCount, long EmptyLines, long ShortestLine, long LongestLine,
    double AverageLineLength, long CharacterCount, long? UniqueLineCount, string Encoding, string LineEnding);

public sealed class FileStatisticsService
{
    public async Task<FileStatistics> AnalyzeAsync(TextFileInfo file, bool calculateUniqueLines = false,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var buffer = new char[64 * 1024];
        long lines = 0, empty = 0, shortest = long.MaxValue, longest = 0, totalCharacters = 0, currentLength = 0;
        var lastWasCr = false;
        var started = DateTime.UtcNow;
        await using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, file.Encoding.Encoding, false, 1024 * 1024, leaveOpen: true);
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (var index = 0; index < read; index++)
            {
                var character = buffer[index];
                if (character == '\r') { FinalizeLine(); lastWasCr = true; }
                else if (character == '\n') { if (!lastWasCr) FinalizeLine(); lastWasCr = false; }
                else { currentLength++; lastWasCr = false; }
            }
            progress?.Report(new OperationProgress(stream.Position, file.Size, lines, 0, DateTime.UtcNow - started, "Dosya analiz ediliyor"));
        }
        if (currentLength > 0) FinalizeLine();
        long? unique = null;
        if (calculateUniqueLines)
        {
            if (file.Size > 256L * 1024 * 1024) throw new InvalidOperationException("Exact unique-line statistics are limited to 256 MB; use the dedupe operation for larger files.");
            var set = new HashSet<string>(StringComparer.Ordinal);
            stream.Position = 0;
            reader.DiscardBufferedData();
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null) set.Add(line);
            unique = set.Count;
        }
        progress?.Report(new OperationProgress(file.Size, file.Size, lines, 0, DateTime.UtcNow - started, "Analiz tamamlandı"));
        return new FileStatistics(file.Size, lines, empty, lines == 0 ? 0 : shortest, longest,
            lines == 0 ? 0 : totalCharacters / (double)lines, totalCharacters, unique, file.Encoding.DisplayName, file.LineEnding);

        void FinalizeLine()
        {
            lines++; totalCharacters += currentLength;
            if (currentLength == 0) empty++;
            shortest = Math.Min(shortest, currentLength); longest = Math.Max(longest, currentLength); currentLength = 0;
        }
    }
}
