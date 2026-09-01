using System.Text;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public sealed record DedupeOptions(bool CaseInsensitive = false, bool KeepLastOccurrence = false, long DiskBackedThresholdBytes = 256L * 1024 * 1024, int PartitionCount = 128);

public sealed class DedupeOperation
{
    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, DedupeOptions options,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var info = new FileInfo(inputPath);
        return info.Length <= options.DiskBackedThresholdBytes && !options.KeepLastOccurrence
            ? await RunInMemoryAsync(inputPath, outputPath, options, progress, cancellationToken)
            : await RunDiskBackedAsync(inputPath, outputPath, options, progress, cancellationToken);
    }

    private static async Task<OperationResult> RunInMemoryAsync(string inputPath, string outputPath, DedupeOptions options,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var info = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, info.Length, "Tekrarlar kaldırılıyor");
        var seen = new HashSet<string>(options.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        long inputLines = 0, outputLines = 0;
        var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        await using var output = TransactionalTextOutput.Create(outputPath, detected.Encoding);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            inputLines++;
            if (seen.Add(line))
            {
                await output.Writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                outputLines++;
            }
            tracker.Report(stream.Position, inputLines, inputLines - outputLines);
        }
        await output.CommitAsync(cancellationToken);
        tracker.Report(info.Length, inputLines, inputLines - outputLines, true);
        return new OperationResult(outputPath, inputLines, outputLines, inputLines - outputLines, tracker.Elapsed);
    }

    private static async Task<OperationResult> RunDiskBackedAsync(string inputPath, string outputPath, DedupeOptions options,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var info = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, info.Length * 2, "Disk destekli tekrar kaldırma");
        var partitions = Math.Clamp(options.PartitionCount, 16, 1024);
        var tempRoot = Path.Combine(Path.GetTempPath(), "GalaXakoEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var bucketPaths = Enumerable.Range(0, partitions).Select(index => Path.Combine(tempRoot, $"bucket-{index:D4}.tmp")).ToArray();
        var runPaths = new List<string>();
        long inputLines = 0;
        try
        {
            await PartitionAsync(inputPath, detected.Encoding, bucketPaths, options, tracker, cancellationToken, count => inputLines = count);
            var comparer = options.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            long uniqueLines = 0;
            for (var index = 0; index < bucketPaths.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(bucketPaths[index])) continue;
                var records = new Dictionary<string, Record>(comparer);
                using (var reader = new StreamReader(bucketPaths[index], Encoding.UTF8, false, 1024 * 1024))
                {
                    string? serialized;
                    while ((serialized = await reader.ReadLineAsync(cancellationToken)) is not null)
                    {
                        var record = ParseRecord(serialized);
                        if (!records.ContainsKey(record.Value) || options.KeepLastOccurrence) records[record.Value] = record;
                    }
                }
                var runPath = Path.Combine(tempRoot, $"run-{index:D4}.tmp");
                await using (var stream = new FileStream(runPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, true))
                await using (var writer = new StreamWriter(stream, new UTF8Encoding(false, true), 1024 * 1024))
                {
                    foreach (var record in records.Values.OrderBy(static record => record.LineNumber))
                        await writer.WriteLineAsync(SerializeRecord(record).AsMemory(), cancellationToken);
                }
                uniqueLines += records.Count;
                runPaths.Add(runPath);
                File.Delete(bucketPaths[index]);
                tracker.Report(info.Length + info.Length * (index + 1) / partitions, inputLines, inputLines - uniqueLines);
            }

            await MergeRunsAsync(runPaths, outputPath, detected.Encoding, cancellationToken);
            tracker.Report(info.Length * 2, inputLines, inputLines - uniqueLines, true);
            return new OperationResult(outputPath, inputLines, uniqueLines, inputLines - uniqueLines, tracker.Elapsed);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static async Task PartitionAsync(string inputPath, Encoding encoding, string[] bucketPaths, DedupeOptions options,
        ThrottledProgress tracker, CancellationToken cancellationToken, Action<long> setCount)
    {
        var comparer = options.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var writers = new StreamWriter?[bucketPaths.Length];
        var (stream, reader) = OperationIO.OpenReader(inputPath, encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        long lineNumber = 0;
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                lineNumber++;
                var bucket = (comparer.GetHashCode(line) & int.MaxValue) % bucketPaths.Length;
                writers[bucket] ??= new StreamWriter(new FileStream(bucketPaths[bucket], FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true), new UTF8Encoding(false, true), 64 * 1024);
                await writers[bucket]!.WriteLineAsync(SerializeRecord(new Record(lineNumber, line)).AsMemory(), cancellationToken);
                tracker.Report(stream.Position, lineNumber, 0);
            }
            setCount(lineNumber);
        }
        finally
        {
            foreach (var writer in writers)
                if (writer is not null) await writer.DisposeAsync();
        }
    }

    private static async Task MergeRunsAsync(IReadOnlyList<string> runPaths, string outputPath, Encoding encoding, CancellationToken cancellationToken)
    {
        var readers = runPaths.Select(path => new StreamReader(path, Encoding.UTF8, false, 64 * 1024)).ToArray();
        var queue = new PriorityQueue<(Record Record, int ReaderIndex), long>();
        try
        {
            for (var index = 0; index < readers.Length; index++)
            {
                var line = await readers[index].ReadLineAsync(cancellationToken);
                if (line is not null) { var record = ParseRecord(line); queue.Enqueue((record, index), record.LineNumber); }
            }
            await using var output = TransactionalTextOutput.Create(outputPath, encoding);
            while (queue.TryDequeue(out var item, out _))
            {
                await output.Writer.WriteLineAsync(item.Record.Value.AsMemory(), cancellationToken);
                var next = await readers[item.ReaderIndex].ReadLineAsync(cancellationToken);
                if (next is not null) { var record = ParseRecord(next); queue.Enqueue((record, item.ReaderIndex), record.LineNumber); }
            }
            await output.CommitAsync(cancellationToken);
        }
        finally
        {
            foreach (var reader in readers) reader.Dispose();
        }
    }

    private static string SerializeRecord(Record record) => $"{record.LineNumber}\t{Convert.ToBase64String(Encoding.UTF8.GetBytes(record.Value))}";
    private static Record ParseRecord(string value)
    {
        var separator = value.IndexOf('\t');
        return new Record(long.Parse(value.AsSpan(0, separator), System.Globalization.CultureInfo.InvariantCulture), Encoding.UTF8.GetString(Convert.FromBase64String(value[(separator + 1)..])));
    }
    private sealed record Record(long LineNumber, string Value);
}
