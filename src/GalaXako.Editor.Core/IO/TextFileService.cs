using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public sealed class TextFileService : ITextFileService
{
    public async Task<string> LoadNormalAsync(TextFileInfo file, CancellationToken cancellationToken = default)
    {
        if (file.Mode != FileOpenMode.Normal)
            throw new InvalidOperationException("Large files cannot be loaded into the normal editor.");

        await using var stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, file.Encoding.Encoding, false, 1024 * 1024, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task SaveSafeAsync(string destinationPath, string text, TextEncodingInfo encoding, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Output directory is unavailable.");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                             1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            await using (var writer = new StreamWriter(stream, encoding.Encoding, 1024 * 1024, leaveOpen: true))
            {
                await writer.WriteAsync(text.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(fullPath))
                File.Replace(tempPath, fullPath, null, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, fullPath);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public string CreateOutputPath(string inputPath, string suffix, string? outputDirectory = null)
    {
        var directory = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var extension = Path.GetExtension(inputPath);
        var candidate = Path.Combine(directory, name + suffix + extension);
        for (var index = 2; File.Exists(candidate); index++)
            candidate = Path.Combine(directory, $"{name}{suffix}_{index}{extension}");
        return candidate;
    }

    public string CreateOutputDirectory(string inputPath, string suffix, string? outputDirectory = null)
    {
        var parentDirectory = outputDirectory ?? Path.GetDirectoryName(Path.GetFullPath(inputPath))!;
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var candidate = Path.Combine(parentDirectory, name + suffix);
        for (var index = 2; Directory.Exists(candidate) || File.Exists(candidate); index++)
            candidate = Path.Combine(parentDirectory, $"{name}{suffix}_{index}");
        return candidate;
    }
}
