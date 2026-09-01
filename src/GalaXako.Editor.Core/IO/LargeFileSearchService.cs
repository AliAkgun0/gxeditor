using System.Text.RegularExpressions;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public sealed record SearchOptions(string Query, bool CaseSensitive = false, bool UseRegex = false, int MaximumResults = 10_000);
public sealed record LargeFileSearchResult(long LineNumber, long ByteOffset, string Preview);
public sealed record LargeFileSearchSummary(long MatchCount, bool ResultLimitReached, TimeSpan Elapsed);

public sealed class LargeFileSearchService
{
    public async Task<LargeFileSearchSummary> SearchAsync(TextFileInfo file, SearchOptions options,
        IProgress<LargeFileSearchResult> results, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Query)) throw new ArgumentException("Search query cannot be empty.", nameof(options));
        Regex? regex = null;
        if (options.UseRegex)
        {
            var flags = RegexOptions.Compiled | RegexOptions.CultureInvariant;
            if (!options.CaseSensitive) flags |= RegexOptions.IgnoreCase;
            regex = new Regex(options.Query, flags, TimeSpan.FromSeconds(2));
        }
        var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var started = DateTime.UtcNow;
        long lineNumber = 0, matches = 0, lastReport = 0, processedBytes = file.Encoding.PreambleLength;
        await using var reader = new BoundedLineReader(file.Path, file.Encoding.PreambleLength, file.Encoding.Encoding);
        BoundedLine? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            lineNumber++; processedBytes = line.EndOffset;
            var isMatch = regex?.IsMatch(line.Text) ?? line.Text.Contains(options.Query, comparison);
            if (isMatch)
            {
                matches++;
                if (matches <= options.MaximumResults)
                    results.Report(new LargeFileSearchResult(lineNumber, line.StartOffset, CreatePreview(line.Text, options.Query, comparison)));
            }
            if (processedBytes - lastReport >= 8L * 1024 * 1024)
            {
                lastReport = processedBytes;
                progress?.Report(new OperationProgress(processedBytes, file.Size, lineNumber, matches, DateTime.UtcNow - started, "Büyük dosyada aranıyor"));
            }
        }
        progress?.Report(new OperationProgress(file.Size, file.Size, lineNumber, matches, DateTime.UtcNow - started, "Arama tamamlandı"));
        return new LargeFileSearchSummary(matches, matches > options.MaximumResults, DateTime.UtcNow - started);
    }

    private static string CreatePreview(string line, string query, StringComparison comparison)
    {
        if (line.Length <= 240) return line;
        var match = line.IndexOf(query, comparison);
        var start = Math.Max(0, match < 0 ? 0 : match - 80);
        var length = Math.Min(240, line.Length - start);
        return (start > 0 ? "…" : string.Empty) + line.Substring(start, length) + (start + length < line.Length ? "…" : string.Empty);
    }
}
