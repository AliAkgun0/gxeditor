using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public sealed class FileInspectionService : IFileInspectionService
{
    public async Task<TextFileInfo> InspectAsync(string path, long normalThresholdBytes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
            throw new FileNotFoundException("The selected file no longer exists.", fullPath);

        var encoding = await EncodingDetector.DetectAsync(fullPath, cancellationToken);
        var lineEnding = await DetectLineEndingAsync(fullPath, encoding, cancellationToken);
        return new TextFileInfo(
            fullPath,
            info.Length,
            info.LastWriteTimeUtc,
            encoding,
            lineEnding,
            info.Length <= normalThresholdBytes ? FileOpenMode.Normal : FileOpenMode.Large);
    }

    private static async Task<string> DetectLineEndingAsync(string path, TextEncodingInfo encoding, CancellationToken cancellationToken)
    {
        var buffer = new char[32 * 1024];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, encoding.Encoding, false, buffer.Length, leaveOpen: false);
        var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
        var sample = buffer.AsSpan(0, read);
        var crlf = 0;
        var lf = 0;
        var cr = 0;
        for (var index = 0; index < sample.Length; index++)
        {
            if (sample[index] == '\r')
            {
                if (index + 1 < sample.Length && sample[index + 1] == '\n')
                {
                    crlf++;
                    index++;
                }
                else cr++;
            }
            else if (sample[index] == '\n') lf++;
        }

        if (crlf == 0 && lf == 0 && cr == 0) return "Bilinmiyor";
        if (crlf >= lf && crlf >= cr) return "CRLF";
        return lf >= cr ? "LF" : "CR";
    }
}
