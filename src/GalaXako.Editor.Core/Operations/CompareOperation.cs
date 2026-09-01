using System.Text;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum CompareMode { OnlyInA, OnlyInB, InBoth, Different }
public sealed record CompareResult(string OutputPath, long OnlyInA, long OnlyInB, long InBoth, TimeSpan Elapsed);

public sealed class CompareOperation
{
    public async Task<CompareResult> RunAsync(string fileA, string fileB, string outputPath, CompareMode mode, bool caseSensitive = false,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GalaXakoEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var sortedA = Path.Combine(tempRoot, "a.sorted");
        var sortedB = Path.Combine(tempRoot, "b.sorted");
        var tracker = new ThrottledProgress(progress, new FileInfo(fileA).Length + new FileInfo(fileB).Length, "Dosyalar karşılaştırılıyor");
        try
        {
            var sort = new SortOperation();
            var sortOptions = new SortOptions(SortMode.AlphabeticalAscending, caseSensitive);
            await sort.RunAsync(fileA, sortedA, sortOptions, cancellationToken: cancellationToken);
            await sort.RunAsync(fileB, sortedB, sortOptions, cancellationToken: cancellationToken);
            var encodingA = await EncodingDetector.DetectAsync(sortedA, cancellationToken);
            var encodingB = await EncodingDetector.DetectAsync(sortedB, cancellationToken);
            var comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            using var readerA = new StreamReader(sortedA, encodingA.Encoding, false, 1024 * 1024);
            using var readerB = new StreamReader(sortedB, encodingB.Encoding, false, 1024 * 1024);
            await using var output = TransactionalTextOutput.Create(outputPath, new UTF8Encoding(false, true));
            var a = await ReadNextDistinctAsync(readerA, null, comparer, cancellationToken);
            var b = await ReadNextDistinctAsync(readerB, null, comparer, cancellationToken);
            long onlyA = 0, onlyB = 0, both = 0, processed = 0;
            while (a is not null || b is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var comparison = a is null ? 1 : b is null ? -1 : comparer.Compare(a, b);
                if (comparison == 0)
                {
                    both++; if (mode == CompareMode.InBoth) await output.Writer.WriteLineAsync(a!.AsMemory(), cancellationToken);
                    var oldA = a; var oldB = b; a = await ReadNextDistinctAsync(readerA, oldA, comparer, cancellationToken); b = await ReadNextDistinctAsync(readerB, oldB, comparer, cancellationToken);
                }
                else if (comparison < 0)
                {
                    onlyA++; if (mode is CompareMode.OnlyInA or CompareMode.Different) await output.Writer.WriteLineAsync(a!.AsMemory(), cancellationToken);
                    var old = a; a = await ReadNextDistinctAsync(readerA, old, comparer, cancellationToken);
                }
                else
                {
                    onlyB++; if (mode is CompareMode.OnlyInB or CompareMode.Different) await output.Writer.WriteLineAsync(b!.AsMemory(), cancellationToken);
                    var old = b; b = await ReadNextDistinctAsync(readerB, old, comparer, cancellationToken);
                }
                processed++; tracker.Report(processed, processed, onlyA + onlyB);
            }
            await output.CommitAsync(cancellationToken);
            tracker.Report(new FileInfo(fileA).Length + new FileInfo(fileB).Length, processed, onlyA + onlyB, true);
            return new CompareResult(outputPath, onlyA, onlyB, both, tracker.Elapsed);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static async Task<string?> ReadNextDistinctAsync(StreamReader reader, string? previous, StringComparer comparer, CancellationToken cancellationToken)
    {
        string? value;
        do value = await reader.ReadLineAsync(cancellationToken); while (value is not null && previous is not null && comparer.Equals(value, previous));
        return value;
    }
}
