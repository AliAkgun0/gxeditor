using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsStore _store;
    private AppSettings _settings = new();
    private int _thresholdMb = 32;
    private int _ioBufferKb = 1024;
    private string _status = string.Empty;

    public SettingsViewModel(ISettingsStore store)
    {
        _store = store;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
    }

    public AppSettings Settings { get => _settings; private set { if (SetProperty(ref _settings, value)) NotifySettings(); } }
    public int ThresholdMb { get => _thresholdMb; set => SetProperty(ref _thresholdMb, value); }
    public int IoBufferKb { get => _ioBufferKb; set => SetProperty(ref _ioBufferKb, value); }
    public int LargeFileChunkLineCount { get => Settings.LargeFileChunkLineCount; set { Settings.LargeFileChunkLineCount = value; OnPropertyChanged(); } }
    public int MaxConcurrentJobs { get => Settings.MaxConcurrentJobs; set { Settings.MaxConcurrentJobs = Math.Clamp(value, 1, 8); OnPropertyChanged(); } }
    public int RecentFileCount { get => Settings.RecentFileCount; set { Settings.RecentFileCount = Math.Clamp(value, 1, 50); OnPropertyChanged(); } }
    public bool PreserveEncoding { get => Settings.PreserveEncoding; set { Settings.PreserveEncoding = value; OnPropertyChanged(); } }
    public bool ConfirmDestructiveActions { get => Settings.ConfirmDestructiveActions; set { Settings.ConfirmDestructiveActions = value; OnPropertyChanged(); } }
    public bool ReopenLastFile { get => Settings.ReopenLastFile; set { Settings.ReopenLastFile = value; OnPropertyChanged(); } }
    public string Theme { get => Settings.Theme; set { Settings.Theme = value; OnPropertyChanged(); } }
    public string DefaultOutputFolder { get => Settings.DefaultOutputFolder ?? string.Empty; set { Settings.DefaultOutputFolder = string.IsNullOrWhiteSpace(value) ? null : value; OnPropertyChanged(); } }
    public string OutputSuffix { get => Settings.OutputSuffix; set { Settings.OutputSuffix = value; OnPropertyChanged(); } }
    public string DefaultEncoding { get => Settings.DefaultEncoding; set { Settings.DefaultEncoding = value; OnPropertyChanged(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public IReadOnlyList<int> ThresholdPresets { get; } = [16, 32, 64, 128];
    public IReadOnlyList<int> IoBufferPresets { get; } = [64, 256, 1024, 4096];
    public IReadOnlyList<int> ChunkPresets { get; } = [1_000, 5_000, 10_000, 50_000];
    public IReadOnlyList<int> ConcurrentJobPresets { get; } = [1, 2, 4, 8];
    public IReadOnlyList<string> Themes { get; } = ["Dark", "System"];
    public IReadOnlyList<string> Encodings { get; } = ["utf-8", "utf-8-bom", "utf-16"];
    public string VersionText { get; } = $"Sürüm {typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"} · Windows x64";
    public AsyncRelayCommand SaveCommand { get; }

    public async Task LoadAsync()
    {
        Settings = await _store.LoadAsync();
        ThresholdMb = (int)Math.Clamp(Settings.NormalFileThresholdBytes / (1024 * 1024), 16, 128);
        IoBufferKb = Math.Clamp(Settings.IoBufferSize / 1024, 64, 4096);
        ApplyTheme();
        Status = "Ayarlar yüklendi.";
    }

    private async Task SaveAsync()
    {
        Settings.NormalFileThresholdBytes = ThresholdMb * 1024L * 1024;
        Settings.IoBufferSize = IoBufferKb * 1024;
        await _store.SaveAsync(Settings);
        ApplyTheme();
        Status = "Ayarlar yerel olarak kaydedildi.";
    }

    private void NotifySettings()
    {
        OnPropertyChanged(nameof(LargeFileChunkLineCount)); OnPropertyChanged(nameof(MaxConcurrentJobs)); OnPropertyChanged(nameof(RecentFileCount));
        OnPropertyChanged(nameof(PreserveEncoding)); OnPropertyChanged(nameof(ConfirmDestructiveActions)); OnPropertyChanged(nameof(ReopenLastFile));
        OnPropertyChanged(nameof(Theme)); OnPropertyChanged(nameof(DefaultOutputFolder)); OnPropertyChanged(nameof(OutputSuffix)); OnPropertyChanged(nameof(DefaultEncoding));
    }

#pragma warning disable WPF0001 // Native WPF Fluent ThemeMode is the requested .NET 10 UI foundation.
    private void ApplyTheme() => System.Windows.Application.Current.ThemeMode = Theme == "System" ? System.Windows.ThemeMode.System : System.Windows.ThemeMode.Dark;
#pragma warning restore WPF0001
}
