using System.Text;
using System.Text.RegularExpressions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum TextCaseTransform { None, Lowercase, Uppercase }

public sealed record CleanOptions(
    bool TrimWhitespace = true,
    bool RemoveEmptyLines = true,
    bool RemoveWhitespaceOnlyLines = true,
    bool NormalizeWhitespace = false,
    bool RemoveRepeatedSpaces = false,
    int? MinimumLength = null,
    int? MaximumLength = null,
    TextCaseTransform CaseTransform = TextCaseTransform.None,
    string LineEnding = "\r\n");

public sealed class CleanOperation
{
    private static readonly Regex AnyWhitespace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex RepeatedSpaces = new(" {2,}", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, CleanOptions options,
        Encoding? outputEncoding = null, IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var encoding = outputEncoding ?? detected.Encoding;
        var info = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, info.Length, "Temizleniyor");
        long inputLines = 0, outputLines = 0, affected = 0;
        var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        await using var output = TransactionalTextOutput.Create(outputPath, encoding);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            inputLines++;
            var transformed = Transform(line, options);
            if (transformed is null)
            {
                affected++;
            }
            else
            {
                if (!string.Equals(line, transformed, StringComparison.Ordinal)) affected++;
                await output.Writer.WriteAsync(transformed.AsMemory(), cancellationToken);
                await output.Writer.WriteAsync(options.LineEnding.AsMemory(), cancellationToken);
                outputLines++;
            }
            tracker.Report(stream.Position, inputLines, affected);
        }

        await output.CommitAsync(cancellationToken);
        tracker.Report(info.Length, inputLines, affected, true);
        return new OperationResult(outputPath, inputLines, outputLines, affected, tracker.Elapsed);
    }

    public static string? Transform(string line, CleanOptions options)
    {
        var value = options.TrimWhitespace ? line.Trim() : line;
        if (options.RemoveWhitespaceOnlyLines && string.IsNullOrWhiteSpace(value)) return null;
        if (options.RemoveEmptyLines && value.Length == 0) return null;
        if (options.NormalizeWhitespace) value = AnyWhitespace.Replace(value, " ");
        else if (options.RemoveRepeatedSpaces) value = RepeatedSpaces.Replace(value, " ");
        if (options.MinimumLength is { } min && value.Length < min) return null;
        if (options.MaximumLength is { } max && value.Length > max) return null;
        return options.CaseTransform switch
        {
            TextCaseTransform.Lowercase => value.ToLowerInvariant(),
            TextCaseTransform.Uppercase => value.ToUpperInvariant(),
            _ => value
        };
    }
}
