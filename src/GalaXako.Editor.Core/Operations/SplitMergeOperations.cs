using System.Text;
using System.Text.RegularExpressions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum SplitMode { LineCount, ApproximateBytes, BeforeRegex, AfterRegex }
public sealed record SplitOptions(SplitMode Mode, long Value, string? RegexPattern = null, int NumberPadding = 3);
public sealed record SplitResult(IReadOnlyList<string> OutputPaths, long InputLines, TimeSpan Elapsed);

public sealed class SplitOperation
{
    public async Task<SplitResult> RunAsync(string inputPath, string outputDirectory, SplitOptions options,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        Directory.CreateDirectory(outputDirectory);
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var file = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, file.Length, "Dosya bölünüyor");
        var regex = CreateRegex(options, compiled: true);
        var outputs = new List<string>();
        var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        TransactionalTextOutput? current = null;
        long totalLines = 0, partLines = 0, partBytes = 0;
        var part = 0;
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                totalLines++;
                var lineBytes = detected.Encoding.GetByteCount(line) + detected.Encoding.GetByteCount(Environment.NewLine);
                var splitBefore = current is not null && ShouldSplitBefore(options, regex, partLines, partBytes, lineBytes, line);
                if (splitBefore) { await CommitPartAsync(current!, outputs, cancellationToken); current = null; partLines = 0; partBytes = 0; }
                if (current is null) current = CreatePart(inputPath, outputDirectory, ++part, options.NumberPadding, detected.Encoding);
                await current.Writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                partLines++; partBytes += lineBytes;
                if (ShouldSplitAfter(options, regex, line)) { await CommitPartAsync(current, outputs, cancellationToken); current = null; partLines = 0; partBytes = 0; }
                tracker.Report(stream.Position, totalLines, outputs.Count);
            }
            if (current is not null) { await CommitPartAsync(current, outputs, cancellationToken); current = null; }
            tracker.Report(file.Length, totalLines, outputs.Count, true);
            return new SplitResult(outputs, totalLines, tracker.Elapsed);
        }
        catch
        {
            foreach (var path in outputs) if (File.Exists(path)) File.Delete(path);
            throw;
        }
        finally
        {
            if (current is not null) await current.DisposeAsync();
        }
    }

    public static IReadOnlyList<IReadOnlyList<string>> SplitSample(IReadOnlyList<string> lines, SplitOptions options, Encoding encoding)
    {
        ValidateOptions(options);
        var regex = CreateRegex(options, compiled: false);
        var result = new List<IReadOnlyList<string>>();
        List<string>? current = null;
        long partLines = 0, partBytes = 0;
        foreach (var line in lines)
        {
            var lineBytes = encoding.GetByteCount(line) + encoding.GetByteCount(Environment.NewLine);
            if (current is not null && ShouldSplitBefore(options, regex, partLines, partBytes, lineBytes, line))
            {
                result.Add(current);
                current = null;
                partLines = 0;
                partBytes = 0;
            }
            current ??= [];
            current.Add(line);
            partLines++;
            partBytes += lineBytes;
            if (ShouldSplitAfter(options, regex, line))
            {
                result.Add(current);
                current = null;
                partLines = 0;
                partBytes = 0;
            }
        }
        if (current is not null) result.Add(current);
        return result;
    }

    private static void ValidateOptions(SplitOptions options)
    {
        if (options.Value <= 0 && options.Mode is SplitMode.LineCount or SplitMode.ApproximateBytes)
            throw new ArgumentOutOfRangeException(nameof(options), "Satır veya bayt değeri sıfırdan büyük olmalıdır.");
        if ((options.Mode is SplitMode.BeforeRegex or SplitMode.AfterRegex) && string.IsNullOrWhiteSpace(options.RegexPattern))
            throw new ArgumentException("A regex pattern is required.", nameof(options));
    }

    private static Regex? CreateRegex(SplitOptions options, bool compiled)
    {
        if (options.Mode is not (SplitMode.BeforeRegex or SplitMode.AfterRegex)) return null;
        var regexOptions = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        return new Regex(options.RegexPattern!, regexOptions, TimeSpan.FromSeconds(2));
    }

    private static bool ShouldSplitBefore(SplitOptions options, Regex? regex, long partLines, long partBytes, long lineBytes, string line) =>
        partLines > 0 &&
        (options.Mode == SplitMode.LineCount && partLines >= options.Value ||
         options.Mode == SplitMode.ApproximateBytes && partBytes + lineBytes > options.Value ||
         options.Mode == SplitMode.BeforeRegex && regex!.IsMatch(line));

    private static bool ShouldSplitAfter(SplitOptions options, Regex? regex, string line) =>
        options.Mode == SplitMode.AfterRegex && regex!.IsMatch(line);

    private static TransactionalTextOutput CreatePart(string inputPath, string directory, int part, int padding, Encoding encoding)
    {
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        return TransactionalTextOutput.Create(Path.Combine(directory, $"{name}_part_{part.ToString($"D{Math.Clamp(padding, 1, 8)}")}{extension}"), encoding);
    }

    private static async Task CommitPartAsync(TransactionalTextOutput output, ICollection<string> outputs, CancellationToken cancellationToken)
    {
        await output.CommitAsync(cancellationToken);
        outputs.Add(output.DestinationPath);
    }
}

public sealed record MergeOptions(bool InsertNewlineBetweenFiles = true, string? CustomSeparator = null, Encoding? OutputEncoding = null);

public sealed class MergeOperation
{
    public async Task<OperationResult> RunAsync(IReadOnlyList<string> inputPaths, string outputPath, MergeOptions options,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (inputPaths.Count == 0) throw new ArgumentException("At least one input file is required.", nameof(inputPaths));
        var encodings = new List<TextEncodingInfo>(inputPaths.Count);
        foreach (var path in inputPaths) encodings.Add(await EncodingDetector.DetectAsync(path, cancellationToken));
        var outputEncoding = options.OutputEncoding ?? encodings[0].Encoding;
        var totalBytes = inputPaths.Sum(path => new FileInfo(path).Length);
        var tracker = new ThrottledProgress(progress, totalBytes, "Dosyalar birleştiriliyor");
        long processedBytes = 0, lines = 0;
        await using var output = TransactionalTextOutput.Create(outputPath, outputEncoding);
        for (var index = 0; index < inputPaths.Count; index++)
        {
            var (stream, reader) = OperationIO.OpenReader(inputPaths[index], encodings[index].Encoding, 1024 * 1024);
            await using var ownedStream = stream;
            using var ownedReader = reader;
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                await output.Writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                lines++;
                tracker.Report(processedBytes + stream.Position, lines, 0);
            }
            processedBytes += new FileInfo(inputPaths[index]).Length;
            if (index < inputPaths.Count - 1)
            {
                if (options.CustomSeparator is { Length: > 0 }) await output.Writer.WriteLineAsync(options.CustomSeparator.AsMemory(), cancellationToken);
                else if (options.InsertNewlineBetweenFiles) await output.Writer.WriteLineAsync(ReadOnlyMemory<char>.Empty, cancellationToken);
            }
        }
        await output.CommitAsync(cancellationToken);
        tracker.Report(totalBytes, lines, 0, true);
        return new OperationResult(outputPath, lines, lines, 0, tracker.Elapsed);
    }
}
