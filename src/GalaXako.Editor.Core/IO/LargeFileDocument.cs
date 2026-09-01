using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public sealed class LargeFileDocument
{
    public LargeFileDocument(TextFileInfo file, int indexIntervalLines)
    {
        File = file;
        Index = new SparseLineIndex(file.Path, file.Encoding, indexIntervalLines);
    }

    public TextFileInfo File { get; }
    public SparseLineIndex Index { get; }

    public Task BuildIndexAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default) =>
        Index.BuildAsync(progress, cancellationToken);

    public async Task<PreviewChunk> ReadChunkByLineAsync(long requestedLine, int lineCount, CancellationToken cancellationToken = default)
    {
        if (!Index.IsBuilt) throw new InvalidOperationException("Build the sparse line index before reading chunks.");
        requestedLine = Math.Clamp(requestedLine, 1, Math.Max(1, Index.LineCount));
        lineCount = Math.Clamp(lineCount, 1, 50_000);
        var nearest = Index.FindNearestLine(requestedLine);
        await using var reader = new BoundedLineReader(File.Path, nearest.ByteOffset, File.Encoding.Encoding);
        var currentLine = nearest.LineNumber;
        while (currentLine < requestedLine && await reader.ReadLineAsync(cancellationToken) is not null)
            currentLine++;
        var lines = new List<string>(lineCount);
        long startOffset = nearest.ByteOffset, endOffset = nearest.ByteOffset;
        while (lines.Count < lineCount)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (lines.Count == 0) startOffset = line.StartOffset;
            endOffset = line.EndOffset;
            lines.Add(line.Text);
        }
        return new PreviewChunk(lines, requestedLine, startOffset, endOffset, lines.Count < lineCount);
    }

    public async Task<PreviewChunk> ReadChunkByByteOffsetAsync(long byteOffset, int lineCount, CancellationToken cancellationToken = default)
    {
        if (!Index.IsBuilt) throw new InvalidOperationException("Build the sparse line index before reading chunks.");
        byteOffset = Math.Clamp(byteOffset, File.Encoding.PreambleLength, Math.Max(File.Encoding.PreambleLength, File.Size - 1));
        var nearest = Index.FindNearestOffset(byteOffset);
        await using var reader = new BoundedLineReader(File.Path, nearest.ByteOffset, File.Encoding.Encoding);
        long line = nearest.LineNumber;
        BoundedLine? first = null;
        while (true)
        {
            var candidate = await reader.ReadLineAsync(cancellationToken);
            if (candidate is null) break;
            if (candidate.EndOffset >= byteOffset) { first = candidate; break; }
            line++;
        }
        var lines = new List<string>(Math.Clamp(lineCount, 1, 50_000));
        long startOffset = first?.StartOffset ?? byteOffset;
        long endOffset = first?.EndOffset ?? byteOffset;
        if (first is not null) lines.Add(first.Text);
        while (lines.Count < lineCount)
        {
            var value = await reader.ReadLineAsync(cancellationToken);
            if (value is null) break;
            lines.Add(value.Text);
            endOffset = value.EndOffset;
        }
        return new PreviewChunk(lines, line, startOffset, endOffset, lines.Count < lineCount);
    }
}
