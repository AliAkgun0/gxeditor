using System.Collections.ObjectModel;
using System.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.App.ViewModels;

public sealed class HomeViewModel(Func<string?, Task> openFile, Action<RecentFile> removeRecent) : ViewModelBase
{
    private int _activeJobs;
    private long _processedToday;
    private string _lastOperation = "Henüz işlem yok";
    public AsyncRelayCommand OpenFileCommand { get; } = new(_ => openFile(null));
    public AsyncRelayCommand OpenRecentCommand { get; } = new(file => file is RecentFile recent ? openFile(recent.Path) : Task.CompletedTask);
    public RelayCommand RemoveRecentCommand { get; } = new(file => { if (file is RecentFile recent) removeRecent(recent); });
    public RelayCommand OpenContainingFolderCommand { get; } = new(file => { if (file is RecentFile recent && File.Exists(recent.Path)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{recent.Path}\"") { UseShellExecute = true }); });
    public RelayCommand CopyPathCommand { get; } = new(file => { if (file is RecentFile recent) System.Windows.Clipboard.SetText(recent.Path); });
    public ObservableCollection<RecentFile> RecentFiles { get; } = [];
    public bool HasRecentFiles => RecentFiles.Count > 0;
    public int ActiveJobs { get => _activeJobs; set => SetProperty(ref _activeJobs, value); }
    public long ProcessedToday { get => _processedToday; set => SetProperty(ref _processedToday, value); }
    public string LastOperation { get => _lastOperation; set => SetProperty(ref _lastOperation, value); }
    public void NotifyRecentFilesChanged() => OnPropertyChanged(nameof(HasRecentFiles));
}

public sealed class NavigationItem(string key, string label, string glyph, object page) : ViewModelBase
{
    private bool _isSelected;
    public string Key { get; } = key;
    public string Label { get; } = label;
    public string Glyph { get; } = glyph;
    public object Page { get; } = page;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
