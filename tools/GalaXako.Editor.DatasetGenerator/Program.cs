using System.Globalization;
using System.Text;

var arguments = args.Select((value, index) => (value, index)).ToDictionary(item => item.value, item => item.index, StringComparer.OrdinalIgnoreCase);
if (!arguments.TryGetValue("--output", out var outputIndex) || outputIndex + 1 >= args.Length ||
    !arguments.TryGetValue("--size", out var sizeIndex) || sizeIndex + 1 >= args.Length)
{
    Console.Error.WriteLine("Usage: dotnet run -- --output <path> --size <100MB|1GB|bytes>");
    return 2;
}

var outputPath = Path.GetFullPath(args[outputIndex + 1]);
var targetBytes = ParseSize(args[sizeIndex + 1]);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var lineNumber = 0L;
await using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024,
    FileOptions.Asynchronous | FileOptions.SequentialScan);
await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024 * 1024);
while (stream.Position < targetBytes)
{
    lineNumber++;
    var category = lineNumber % 17 == 0 ? "duplicate-heavy" : "dataset";
    await writer.WriteLineAsync($"{lineNumber:D12}|{category}|user{lineNumber % 100_000:D6}@example.test|https://example.test/items/{lineNumber % 10_000}");
    if (lineNumber % 250_000 == 0)
    {
        await writer.FlushAsync();
        Console.WriteLine($"{stream.Position * 100d / targetBytes:0.0}% · {stream.Position:N0} bytes");
    }
}
await writer.FlushAsync();
Console.WriteLine($"Created {outputPath} ({stream.Length:N0} bytes, {lineNumber:N0} lines)");
return 0;

static long ParseSize(string value)
{
    value = value.Trim().ToUpperInvariant();
    if (value.EndsWith("GB", StringComparison.Ordinal)) return checked((long)(double.Parse(value[..^2], CultureInfo.InvariantCulture) * 1024 * 1024 * 1024));
    if (value.EndsWith("MB", StringComparison.Ordinal)) return checked((long)(double.Parse(value[..^2], CultureInfo.InvariantCulture) * 1024 * 1024));
    return long.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
}
