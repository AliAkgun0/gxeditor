using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.IO;

public sealed class SparseLineIndex
{
    private readonly List<LineIndexEntry> _entries = [];

    public SparseLineIndex(string path, TextEncodingInfo encoding, int intervalLines)
    {
        Path = System.IO.Path.GetFullPath(path);
        Encoding = encoding;
        IntervalLines = Math.Max(100, intervalLines);
    }

    public string Path { get; }
    public TextEncodingInfo Encoding { get; }
    public int IntervalLines { get; }
    public IReadOnlyList<LineIndexEntry> Entries => _entries;
    public long LineCount { get; private set; }
    public long FileLength { get; private set; }
    public bool IsBuilt { get; private set; }

    public async Task BuildAsync(IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        _entries.Clear();
        _entries.Add(new LineIndexEntry(1, Encoding.PreambleLength));
        var file = new FileInfo(Path);
        FileLength = file.Length;
        if (file.Length <= Encoding.PreambleLength)
        {
            LineCount = 0;
            IsBuilt = true;
            return;
        }

        var newline = GetNewlineBytes(Encoding);
        var buffer = new byte[1024 * 1024];
        long lineNumber = 1;
        long absoluteOffset = Encoding.PreambleLength;
        long lastReport = 0;
        var matchIndex = 0;
        var started = DateTime.UtcNow;

        await using var stream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan);
        stream.Position = Encoding.PreambleLength;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            for (var index = 0; index < read; index++)
            {
                if (buffer[index] == newline[matchIndex])
                {
                    matchIndex++;
                    if (matchIndex == newline.Length)
                    {
                        lineNumber++;
                        var nextLineOffset = absoluteOffset + index + 1;
                        if ((lineNumber - 1) % IntervalLines == 0)
                            _entries.Add(new LineIndexEntry(lineNumber, nextLineOffset));
                        matchIndex = 0;
                    }
                }
                else
                {
                    matchIndex = buffer[index] == newline[0] ? 1 : 0;
                }
            }

            absoluteOffset += read;
            if (absoluteOffset - lastReport >= 8L * 1024 * 1024)
            {
                lastReport = absoluteOffset;
                progress?.Report(new OperationProgress(absoluteOffset, file.Length, lineNumber, 0, DateTime.UtcNow - started, "Satır indeksi oluşturuluyor"));
            }
        }

        LineCount = lineNumber;
        IsBuilt = true;
        progress?.Report(new OperationProgress(file.Length, file.Length, lineNumber, 0, DateTime.UtcNow - started, "İndeks hazır"));
    }

    public LineIndexEntry FindNearestLine(long lineNumber)
    {
        EnsureBuilt();
        lineNumber = Math.Max(1, lineNumber);
        var low = 0;
        var high = _entries.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (_entries[middle].LineNumber <= lineNumber) low = middle + 1; else high = middle - 1;
        }
        return _entries[Math.Max(0, high)];
    }

    public LineIndexEntry FindNearestOffset(long byteOffset)
    {
        EnsureBuilt();
        byteOffset = Math.Max(Encoding.PreambleLength, byteOffset);
        var low = 0;
        var high = _entries.Count - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (_entries[middle].ByteOffset <= byteOffset) low = middle + 1; else high = middle - 1;
        }
        return _entries[Math.Max(0, high)];
    }

    private void EnsureBuilt()
    {
        if (!IsBuilt) throw new InvalidOperationException("The sparse line index has not been built.");
    }

    private static byte[] GetNewlineBytes(TextEncodingInfo encoding) => encoding.Encoding.GetBytes("\n");
}
