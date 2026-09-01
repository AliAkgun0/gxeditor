using System.Windows;
using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Infrastructure.Logging;
using GalaXako.Editor.Infrastructure.Storage;

namespace GalaXako.Editor.App;

public partial class App : Application
{
    public static ISettingsStore SettingsStore { get; } = new JsonSettingsStore();
    public static IHistoryStore HistoryStore { get; } = new JsonHistoryStore();
    public static IApplicationLogger Logger { get; } = new RollingFileLogger();
    public static event Action<string>? RecoverableErrorOccurred;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            if (IsCritical(args.Exception))
            {
                Logger.Error("Critical UI error", args.Exception);
                MessageBox.Show("Uygulamanın güvenle devam edemeyeceği kritik bir hata oluştu. Teknik ayrıntılar yerel günlüğe yazıldı.",
                    "GalaXako Editor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ReportRecoverableError("Bu bölümde bir sorun oluştu. Çalışmaya devam edebilirsiniz; teknik ayrıntılar yerel günlüğe yazıldı.", args.Exception);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("Unobserved background task error", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Logger.Error("Fatal application error", exception);
            }
        };
        Logger.Information("Application startup");
    }

    public static void ReportRecoverableError(string userMessage, Exception exception)
    {
        Logger.Error("Recoverable application error", exception);
        var dispatcher = Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(() => RecoverableErrorOccurred?.Invoke(userMessage));
            return;
        }

        RecoverableErrorOccurred?.Invoke(userMessage);
    }

    internal static bool IsCritical(Exception exception) =>
        exception is OutOfMemoryException or AccessViolationException or BadImageFormatException;

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Information("Application shutdown");
        base.OnExit(e);
    }
}
