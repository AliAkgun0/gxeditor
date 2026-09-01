namespace GalaXako.Editor.Core.Models;

public sealed class AppSettings
{
    public const long DefaultNormalFileThresholdBytes = 32L * 1024 * 1024;

    public long NormalFileThresholdBytes { get; set; } = DefaultNormalFileThresholdBytes;
    public int IoBufferSize { get; set; } = 1024 * 1024;
    public int MaxConcurrentJobs { get; set; } = 2;
    public int LargeFileChunkLineCount { get; set; } = 5_000;
    public int SparseIndexIntervalLines { get; set; } = 10_000;
    public int RecentFileCount { get; set; } = 10;
    public bool PreserveEncoding { get; set; } = true;
    public bool ConfirmDestructiveActions { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    public string DefaultEncoding { get; set; } = "utf-8";
    public string? DefaultOutputFolder { get; set; }
    public string OutputSuffix { get; set; } = "_processed";
    public bool ReopenLastFile { get; set; }
    public bool ReducedMotion { get; set; }
}
