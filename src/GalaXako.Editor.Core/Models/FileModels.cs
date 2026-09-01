using System.Text;

namespace GalaXako.Editor.Core.Models;

public enum FileOpenMode
{
    Normal,
    Large
}

public sealed record TextEncodingInfo(Encoding Encoding, bool HasBom, int PreambleLength, string DisplayName);

public sealed record TextFileInfo(
    string Path,
    long Size,
    DateTime LastModifiedUtc,
    TextEncodingInfo Encoding,
    string LineEnding,
    FileOpenMode Mode,
    long? LineCount = null);

public sealed record RecentFile(string Path, long Size, DateTime LastOpenedUtc, string FileType);

public sealed record PreviewChunk(
    IReadOnlyList<string> Lines,
    long FirstLineNumber,
    long StartByteOffset,
    long EndByteOffset,
    bool IsEndOfFile);

public sealed record LineIndexEntry(long LineNumber, long ByteOffset);
