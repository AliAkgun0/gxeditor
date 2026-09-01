using GalaXako.Editor.Core.Abstractions;

namespace GalaXako.Editor.Infrastructure.Logging;

public sealed class RollingFileLogger : IApplicationLogger
{
    private readonly string _logDirectory;
    private readonly object _gate = new();

    public RollingFileLogger(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GalaXakoEditor");
        _logDirectory = Path.Combine(root, "logs");
    }

    public void Information(string message) => Write("INF", message, null);

    public void Error(string message, Exception exception) => Write("ERR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(_logDirectory);
            var path = Path.Combine(_logDirectory, $"gx-{DateTime.UtcNow:yyyyMMdd}.log");
            var safeMessage = message.ReplaceLineEndings(" ");
            var entry = $"{DateTime.UtcNow:O} [{level}] {safeMessage}";
            if (exception is not null)
            {
                // Exception.ToString() includes the complete stack and all inner exceptions.
                // Keeping this in the local log makes page-load failures diagnosable without
                // exposing technical details in the UI.
                entry += Environment.NewLine + exception;
            }

            lock (_gate)
            {
                File.AppendAllText(path, entry + Environment.NewLine);
                RotateOldLogs();
            }
        }
        catch (Exception loggingException)
        {
            System.Diagnostics.Debug.WriteLine($"GalaXako logging failure: {loggingException.Message}");
        }
    }

    private void RotateOldLogs()
    {
        foreach (var file in Directory.EnumerateFiles(_logDirectory, "gx-*.log")
                     .Select(static path => new FileInfo(path))
                     .OrderByDescending(static file => file.CreationTimeUtc)
                     .Skip(14))
        {
            file.Delete();
        }
    }
}
