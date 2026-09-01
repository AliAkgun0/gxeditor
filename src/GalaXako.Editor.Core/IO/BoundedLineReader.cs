using System.Text;
using System.Buffers;

namespace GalaXako.Editor.Core.IO;

internal sealed class BoundedLineReader : IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly Encoding _encoding;
    private readonly byte[] _buffer = new byte[64 * 1024];
    private readonly int _maxLineBytes;
    private readonly byte[] _lineBuffer;
    private readonly bool _utf16;
    private readonly bool _bigEndian;
    private int _bufferIndex;
    private int _bufferLength;
    private long _consumedOffset;

    public BoundedLineReader(string path, long offset, Encoding encoding, int maxLineCharacters = 32_768)
    {
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, _buffer.Length,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        _stream.Position = offset;
        _consumedOffset = offset;
        _encoding = encoding;
        _utf16 = encoding.CodePage is 1200 or 1201;
        _bigEndian = encoding.CodePage == 1201;
        _maxLineBytes = Math.Max(1024, maxLineCharacters * (_utf16 ? 2 : 4));
        _lineBuffer = ArrayPool<byte>.Shared.Rent(_maxLineBytes);
    }

    public async Task<BoundedLine?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_consumedOffset >= _stream.Length) return null;
        var start = _consumedOffset;
        var bytes = _lineBuffer;
        var count = 0;
        var truncated = false;

        if (_utf16)
        {
            while (true)
            {
                var first = await ReadByteAsync(cancellationToken);
                if (first < 0) break;
                var second = await ReadByteAsync(cancellationToken);
                if (second < 0) { truncated = truncated || count >= bytes.Length; if (count < bytes.Length) bytes[count++] = (byte)first; break; }
                var newline = _bigEndian ? first == 0 && second == 0x0A : first == 0x0A && second == 0;
                if (newline) break;
                if (count + 2 <= bytes.Length) { bytes[count++] = (byte)first; bytes[count++] = (byte)second; }
                else truncated = true;
            }
        }
        else
        {
            while (true)
            {
                var value = await ReadByteAsync(cancellationToken);
                if (value < 0 || value == 0x0A) break;
                if (count < bytes.Length) bytes[count++] = (byte)value; else truncated = true;
            }
        }

        string text;
        while (true)
        {
            try { text = _encoding.GetString(bytes, 0, count).TrimEnd('\r'); break; }
            catch (DecoderFallbackException) when (truncated && count > 0) { count--; }
        }
        if (truncated) text += " … [satır önizlemesi kısaltıldı]";
        return new BoundedLine(text, start, _consumedOffset, truncated);
    }

    private async ValueTask<int> ReadByteAsync(CancellationToken cancellationToken)
    {
        if (_bufferIndex >= _bufferLength)
        {
            _bufferLength = await _stream.ReadAsync(_buffer, cancellationToken);
            _bufferIndex = 0;
            if (_bufferLength == 0) return -1;
        }
        _consumedOffset++;
        return _buffer[_bufferIndex++];
    }

    public async ValueTask DisposeAsync()
    {
        ArrayPool<byte>.Shared.Return(_lineBuffer);
        await _stream.DisposeAsync();
    }
}

internal sealed record BoundedLine(string Text, long StartOffset, long EndOffset, bool IsTruncated);
