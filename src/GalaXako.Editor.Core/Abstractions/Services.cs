using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Abstractions;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IApplicationLogger
{
    void Information(string message);
    void Error(string message, Exception exception);
}

public interface IFileInspectionService
{
    Task<TextFileInfo> InspectAsync(string path, long normalThresholdBytes, CancellationToken cancellationToken = default);
}

public interface ITextFileService
{
    Task<string> LoadNormalAsync(TextFileInfo file, CancellationToken cancellationToken = default);
    Task SaveSafeAsync(string destinationPath, string text, TextEncodingInfo encoding, CancellationToken cancellationToken = default);
    string CreateOutputPath(string inputPath, string suffix, string? outputDirectory = null);
    string CreateOutputDirectory(string inputPath, string suffix, string? outputDirectory = null);
}

public interface IHistoryStore
{
    Task<AppHistory> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppHistory history, CancellationToken cancellationToken = default);
}
