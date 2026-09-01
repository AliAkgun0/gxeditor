using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum ExtractorKind { Url, Domain, Email, IPv4, IPv6, Md5, Sha1, Sha256, CustomRegex }
public sealed record ExtractOptions(ExtractorKind Kind, string? CustomPattern = null, bool UniqueOnly = true, bool SortResults = false, bool CaseSensitive = false, bool CsvOutput = false);

public sealed class ExtractOperation
{
    private static readonly IReadOnlyDictionary<ExtractorKind, string> Patterns = new Dictionary<ExtractorKind, string>
    {
        [ExtractorKind.Url] = @"\bhttps?://[^\s<>\""']+",
        [ExtractorKind.Domain] = @"\b(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}\b",
        [ExtractorKind.Email] = @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,63}\b",
        [ExtractorKind.IPv4] = @"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b",
        // Candidate matching is intentionally permissive for compressed forms; IPAddress validates each match.
        [ExtractorKind.IPv6] = @"(?<![0-9A-F:])(?:[0-9A-F]{0,4}:){2,7}[0-9A-F]{0,4}(?![0-9A-F:])",
        [ExtractorKind.Md5] = @"\b[A-F0-9]{32}\b",
        [ExtractorKind.Sha1] = @"\b[A-F0-9]{40}\b",
        [ExtractorKind.Sha256] = @"\b[A-F0-9]{64}\b"
    };

    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, ExtractOptions options,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (new FileInfo(inputPath).Length > 256L * 1024 * 1024 && (options.UniqueOnly || options.SortResults))
            return await RunLargePostProcessedAsync(inputPath, outputPath, options, progress, cancellationToken);
        var regex = CreateRegex(options, compiled: true);
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var info = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, info.Length, "Değerler ayıklanıyor");
        var comparer = CreateComparer(options);
        HashSet<string>? unique = options.UniqueOnly ? new HashSet<string>(comparer) : null;
        List<string>? sorted = options.SortResults ? [] : null;
        long inputLines = 0, outputLines = 0, duplicates = 0;
        var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        await using var output = TransactionalTextOutput.Create(outputPath, new System.Text.UTF8Encoding(false, true));
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            inputLines++;
            foreach (Match match in regex.Matches(line))
            {
                var value = match.Value;
                if (!IsValidValue(value, options.Kind)) continue;
                if (unique is not null && !unique.Add(value)) { duplicates++; continue; }
                if (sorted is not null) sorted.Add(value);
                else { await WriteValueAsync(output.Writer, FormatValue(value, options.CsvOutput), cancellationToken); outputLines++; }
            }
            tracker.Report(stream.Position, inputLines, outputLines);
        }
        if (sorted is not null)
        {
            sorted.Sort(comparer);
            foreach (var value in sorted) { await WriteValueAsync(output.Writer, FormatValue(value, options.CsvOutput), cancellationToken); outputLines++; }
        }
        await output.CommitAsync(cancellationToken);
        tracker.Report(info.Length, inputLines, outputLines, true);
        return new OperationResult(outputPath, inputLines, outputLines, duplicates, tracker.Elapsed);
    }

    private async Task<OperationResult> RunLargePostProcessedAsync(string inputPath, string outputPath, ExtractOptions options,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "GalaXakoEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var raw = Path.Combine(tempRoot, "extracted.raw");
        var unique = Path.Combine(tempRoot, "extracted.unique");
        try
        {
            var rawResult = await RunAsync(inputPath, raw, options with { UniqueOnly = false, SortResults = false }, progress, cancellationToken);
            var current = raw;
            OperationResult result = rawResult;
            if (options.UniqueOnly)
            {
                var target = options.SortResults ? unique : outputPath;
                result = await new DedupeOperation().RunAsync(current, target,
                    new DedupeOptions(CaseInsensitive: !options.CaseSensitive, DiskBackedThresholdBytes: 0), progress, cancellationToken);
                current = target;
            }
            if (options.SortResults)
                result = await new SortOperation().RunAsync(current, outputPath, new SortOptions(CaseSensitive: options.CaseSensitive), progress, cancellationToken);
            return result;
        }
        finally { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true); }
    }

    public static IReadOnlyList<string> ExtractSample(string text, ExtractOptions options)
    {
        var values = CreateRegex(options, compiled: false).Matches(text)
            .Select(static match => match.Value)
            .Where(value => IsValidValue(value, options.Kind));
        var comparer = CreateComparer(options);
        if (options.UniqueOnly) values = values.Distinct(comparer);
        if (options.SortResults) values = values.Order(comparer);
        return values.Select(value => FormatValue(value, options.CsvOutput)).ToArray();
    }

    private static Regex CreateRegex(ExtractOptions options, bool compiled)
    {
        var pattern = options.Kind == ExtractorKind.CustomRegex
            ? options.CustomPattern ?? throw new ArgumentException("A custom regex pattern is required.")
            : Patterns[options.Kind];
        var flags = RegexOptions.CultureInvariant;
        if (compiled) flags |= RegexOptions.Compiled;
        if (!options.CaseSensitive) flags |= RegexOptions.IgnoreCase;
        return new Regex(pattern, flags, TimeSpan.FromSeconds(2));
    }

    private static StringComparer CreateComparer(ExtractOptions options) =>
        options.CaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    private static bool IsValidValue(string value, ExtractorKind kind) =>
        kind != ExtractorKind.IPv6 ||
        IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetworkV6;

    private static string FormatValue(string value, bool csv) =>
        csv ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static async Task WriteValueAsync(StreamWriter writer, string value, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync(value.AsMemory(), cancellationToken);
    }
}
