namespace GalaXako.Editor.Core.Models;

public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Cancelled,
    Failed
}

public sealed record OperationProgress(
    long ProcessedBytes,
    long TotalBytes,
    long ProcessedLines,
    long AffectedLines,
    TimeSpan Elapsed,
    string Phase)
{
    public double? Percentage => TotalBytes > 0 ? Math.Clamp(ProcessedBytes * 100d / TotalBytes, 0, 100) : null;
    public double BytesPerSecond => Elapsed.TotalSeconds > 0 ? ProcessedBytes / Elapsed.TotalSeconds : 0;
    public TimeSpan? EstimatedRemaining => BytesPerSecond > 0 && TotalBytes >= ProcessedBytes
        ? TimeSpan.FromSeconds((TotalBytes - ProcessedBytes) / BytesPerSecond)
        : null;
}

public sealed record OperationResult(
    string OutputPath,
    long InputLines,
    long OutputLines,
    long AffectedLines,
    TimeSpan Elapsed);
