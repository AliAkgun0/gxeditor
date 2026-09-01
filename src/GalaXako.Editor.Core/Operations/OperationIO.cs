using System.Diagnostics;
using System.Text;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

internal sealed class TransactionalTextOutput : IAsyncDisposable
{
    private bool _committed;

    private TransactionalTextOutput(string destinationPath, string tempPath, FileStream stream, StreamWriter writer)
    {
        DestinationPath = destinationPath;
        TempPath = tempPath;
        Stream = stream;
        Writer = writer;
    }

    public string DestinationPath { get; }
    public string TempPath { get; }
    public FileStream Stream { get; }
    public StreamWriter Writer { get; }

    public static TransactionalTextOutput Create(string destinationPath, Encoding encoding, int bufferSize = 1024 * 1024)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        if (File.Exists(fullPath)) throw new IOException("The output file already exists.");
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Output directory is unavailable.");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
        var writer = new StreamWriter(stream, encoding, bufferSize, leaveOpen: true);
        return new TransactionalTextOutput(fullPath, tempPath, stream, writer);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await Writer.FlushAsync(cancellationToken);
        Stream.Flush(flushToDisk: true);
        await Writer.DisposeAsync();
        await Stream.DisposeAsync();
        File.Move(TempPath, DestinationPath);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await Writer.DisposeAsync();
            await Stream.DisposeAsync();
            if (File.Exists(TempPath)) File.Delete(TempPath);
        }
    }
}

internal sealed class ThrottledProgress(IProgress<OperationProgress>? progress, long totalBytes, string phase)
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _lastReportTicks;

    public void Report(long bytes, long lines, long affected, bool force = false)
    {
        if (progress is null) return;
        var now = _stopwatch.ElapsedTicks;
        if (!force && now - _lastReportTicks < Stopwatch.Frequency / 4) return;
        _lastReportTicks = now;
        progress.Report(new OperationProgress(bytes, totalBytes, lines, affected, _stopwatch.Elapsed, phase));
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;
}

internal static class OperationIO
{
    public static (FileStream Stream, StreamReader Reader) OpenReader(string path, Encoding encoding, int bufferSize)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return (stream, new StreamReader(stream, encoding, false, bufferSize, leaveOpen: true));
    }
}
