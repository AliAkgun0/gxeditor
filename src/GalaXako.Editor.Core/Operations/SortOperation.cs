using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum SortMode { AlphabeticalAscending, AlphabeticalDescending, ShortestFirst, LongestFirst, NumericAscending, NumericDescending, Natural }
public sealed record SortOptions(SortMode Mode = SortMode.AlphabeticalAscending, bool CaseSensitive = false, long ChunkMemoryBytes = 128L * 1024 * 1024);

public sealed class SortOperation
{
    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, SortOptions options,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var info = new FileInfo(inputPath);
        var comparer = CreateComparer(options);
        var tempRoot = Path.Combine(Path.GetTempPath(), "GalaXakoEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var runs = new List<string>();
        var tracker = new ThrottledProgress(progress, info.Length * 2, "Harici birleştirmeli sıralama");
        long lines = 0;
        try
        {
            var chunk = new List<string>();
            long estimatedBytes = 0;
            var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
            await using (stream)
            using (reader)
            {
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
                {
                    chunk.Add(line);
                    lines++;
                    estimatedBytes += 24 + line.Length * 2L;
                    if (estimatedBytes >= Math.Max(8L * 1024 * 1024, options.ChunkMemoryBytes))
                    {
                        runs.Add(await WriteRunAsync(chunk, comparer, tempRoot, runs.Count, cancellationToken));
                        chunk.Clear(); estimatedBytes = 0;
                    }
                    tracker.Report(stream.Position, lines, runs.Count);
                }
            }
            if (chunk.Count > 0) runs.Add(await WriteRunAsync(chunk, comparer, tempRoot, runs.Count, cancellationToken));
            await MergeRunsAsync(runs, outputPath, detected.Encoding, comparer, cancellationToken, (written) => tracker.Report(info.Length + Math.Min(info.Length, written), lines, runs.Count));
            tracker.Report(info.Length * 2, lines, runs.Count, true);
            return new OperationResult(outputPath, lines, lines, 0, tracker.Elapsed);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static async Task<string> WriteRunAsync(List<string> lines, IComparer<string> comparer, string tempRoot, int index, CancellationToken cancellationToken)
    {
        lines.Sort(comparer);
        var path = Path.Combine(tempRoot, $"sort-run-{index:D5}.tmp");
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false, true), 1024 * 1024);
        foreach (var line in lines) await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        return path;
    }

    private static async Task MergeRunsAsync(IReadOnlyList<string> runs, string outputPath, Encoding outputEncoding, IComparer<string> comparer,
        CancellationToken cancellationToken, Action<long> report)
    {
        var readers = runs.Select(path => new StreamReader(path, Encoding.UTF8, false, 1024 * 1024)).ToArray();
        var queue = new PriorityQueue<(string Line, int Reader), string>(comparer);
        long written = 0;
        try
        {
            for (var index = 0; index < readers.Length; index++)
            {
                var line = await readers[index].ReadLineAsync(cancellationToken);
                if (line is not null) queue.Enqueue((line, index), line);
            }
            await using var output = TransactionalTextOutput.Create(outputPath, outputEncoding);
            while (queue.TryDequeue(out var item, out _))
            {
                await output.Writer.WriteLineAsync(item.Line.AsMemory(), cancellationToken);
                written += Encoding.UTF8.GetByteCount(item.Line) + 1;
                var next = await readers[item.Reader].ReadLineAsync(cancellationToken);
                if (next is not null) queue.Enqueue((next, item.Reader), next);
                if ((written & 0x7FFFFF) < 4096) report(written);
            }
            await output.CommitAsync(cancellationToken);
        }
        finally
        {
            foreach (var reader in readers) reader.Dispose();
        }
    }

    public static IComparer<string> CreateComparer(SortOptions options)
    {
        var textComparer = options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        return options.Mode switch
        {
            SortMode.AlphabeticalAscending => textComparer,
            SortMode.AlphabeticalDescending => Comparer<string>.Create((left, right) => textComparer.Compare(right, left)),
            SortMode.ShortestFirst => Comparer<string>.Create((left, right) => CompareLength(left, right, textComparer)),
            SortMode.LongestFirst => Comparer<string>.Create((left, right) => CompareLength(right, left, textComparer)),
            SortMode.NumericAscending => Comparer<string>.Create((left, right) => CompareNumeric(left, right, textComparer)),
            SortMode.NumericDescending => Comparer<string>.Create((left, right) => CompareNumeric(right, left, textComparer)),
            SortMode.Natural => new NaturalStringComparer(textComparer),
            _ => textComparer
        };
    }

    private static int CompareLength(string left, string right, StringComparer fallback)
    {
        var value = left.Length.CompareTo(right.Length);
        return value != 0 ? value : fallback.Compare(left, right);
    }

    private static int CompareNumeric(string left, string right, StringComparer fallback)
    {
        var leftParsed = decimal.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var leftValue);
        var rightParsed = decimal.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var rightValue);
        if (leftParsed && rightParsed) return leftValue.CompareTo(rightValue);
        if (leftParsed != rightParsed) return leftParsed ? -1 : 1;
        return fallback.Compare(left, right);
    }

    private sealed class NaturalStringComparer(StringComparer fallback) : IComparer<string>
    {
        private static readonly Regex Parts = new(@"\d+|\D+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var left = Parts.Matches(x); var right = Parts.Matches(y);
            for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
            {
                var leftPart = left[index].Value; var rightPart = right[index].Value;
                int result;
                if (long.TryParse(leftPart, out var ln) && long.TryParse(rightPart, out var rn)) result = ln.CompareTo(rn);
                else result = fallback.Compare(leftPart, rightPart);
                if (result != 0) return result;
            }
            return left.Count.CompareTo(right.Count);
        }
    }
}
