using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum DelimiterOperationKind { ExtractColumn, RemoveColumn, ReorderColumns, JoinColumns, FilterColumn }
public sealed record DelimiterOptions(string Delimiter, DelimiterOperationKind Operation, int Column = 0,
    IReadOnlyList<int>? Columns = null, string JoinWith = "|", FilterRule? Filter = null);

public sealed class DelimiterOperation
{
    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, DelimiterOptions options,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.Delimiter)) throw new ArgumentException("Delimiter cannot be empty.", nameof(options));
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var info = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, info.Length, "Sütunlar işleniyor");
        var filter = CreateFilter(options);
        long inputLines = 0, outputLines = 0, malformed = 0;
        var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        await using var output = TransactionalTextOutput.Create(outputPath, detected.Encoding);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            inputLines++;
            try
            {
                var transformed = TransformLine(line, options, filter);
                if (transformed is not null) { await output.Writer.WriteLineAsync(transformed.AsMemory(), cancellationToken); outputLines++; }
            }
            catch (ArgumentOutOfRangeException) { malformed++; }
            tracker.Report(stream.Position, inputLines, malformed);
        }
        await output.CommitAsync(cancellationToken);
        tracker.Report(info.Length, inputLines, malformed, true);
        return new OperationResult(outputPath, inputLines, outputLines, malformed, tracker.Elapsed);
    }

    public static IReadOnlyList<string> TransformSample(IEnumerable<string> lines, DelimiterOptions options)
    {
        if (string.IsNullOrEmpty(options.Delimiter)) throw new ArgumentException("Delimiter cannot be empty.", nameof(options));
        var filter = CreateFilter(options);
        var transformed = new List<string>();
        foreach (var line in lines)
        {
            try
            {
                var value = TransformLine(line, options, filter);
                if (value is not null) transformed.Add(value);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Preview follows the engine's malformed-row behavior: skip and continue.
            }
        }
        return transformed;
    }

    private static CompiledFilter? CreateFilter(DelimiterOptions options) =>
        options.Filter is null ? null : new CompiledFilter([options.Filter], FilterLogic.And);

    private static string? TransformLine(string line, DelimiterOptions options, CompiledFilter? filter) =>
        options.Operation switch
        {
            DelimiterOperationKind.ExtractColumn => DelimiterTools.ExtractColumn(line, options.Delimiter, options.Column),
            DelimiterOperationKind.RemoveColumn => DelimiterTools.RemoveColumn(line, options.Delimiter, options.Column),
            DelimiterOperationKind.ReorderColumns => DelimiterTools.ReorderColumns(line, options.Delimiter, options.Columns ?? throw new ArgumentException("Column order is required.")),
            DelimiterOperationKind.JoinColumns => DelimiterTools.JoinColumns(line, options.Delimiter, options.Columns ?? throw new ArgumentException("Columns are required."), options.JoinWith),
            DelimiterOperationKind.FilterColumn => (filter ?? throw new ArgumentException("A filter rule is required."))
                .IsMatch(DelimiterTools.ExtractColumn(line, options.Delimiter, options.Column)) ? line : null,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
}
