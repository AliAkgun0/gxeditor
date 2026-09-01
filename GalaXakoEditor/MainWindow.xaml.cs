using System.Windows;
using System.Windows.Input;
using System.Text;
using System.IO;
using GalaXako.Editor.App.ViewModels;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;
using Microsoft.Win32;

namespace GalaXako.Editor.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly FileInspectionService _inspectionService = new();
    private AppHistory _history = new();
    private CancellationTokenSource? _openCancellation;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(path => OpenFileAsync(path), PickSavePath, PickMultipleFiles, PickSecondFile, RemoveRecent);
        _viewModel.MinimizeCommand = new RelayCommand(_ => WindowState = WindowState.Minimized);
        _viewModel.MaximizeCommand = new RelayCommand(_ => ToggleMaximize());
        _viewModel.CloseCommand = new RelayCommand(_ => Close());
        DataContext = _viewModel;
        App.RecoverableErrorOccurred += OnRecoverableError;
        Closed += (_, _) => App.RecoverableErrorOccurred -= OnRecoverableError;
        Loaded += MainWindow_Loaded;
    }

    private async Task OpenFileAsync(string? path = null)
    {
        if (path is null)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Metin veya veri dosyası seçin",
                Filter = "Desteklenen dosyalar|*.txt;*.log;*.csv;*.tsv;*.jsonl|Tüm dosyalar|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;
            path = dialog.FileName;
        }

        if (!IsSupported(path))
        {
            _viewModel.ShowNotification("Bu dosya türü desteklenmiyor. TXT, LOG, CSV, TSV veya JSONL seçin.");
            return;
        }

        _openCancellation?.Cancel();
        _openCancellation?.Dispose();
        _openCancellation = new CancellationTokenSource();
        try
        {
            var settings = _viewModel.Settings.Settings;
            var file = await _inspectionService.InspectAsync(path, settings.NormalFileThresholdBytes, _openCancellation.Token);
            var progress = new Progress<OperationProgress>(_ => { });
            await _viewModel.Editor.OpenAsync(file, settings, progress, _openCancellation.Token);
            _viewModel.NavigateTo("editor");
            await AddRecentFileAsync(file);
            App.Logger.Information($"Opened file metadata: size={file.Size}, mode={file.Mode}, encoding={file.Encoding.DisplayName}");
        }
        catch (OperationCanceledException)
        {
            App.Logger.Information("File open cancelled");
        }
        catch (Exception exception)
        {
            App.ReportRecoverableError(FriendlyMessage(exception), exception);
        }
    }

    private string? PickSavePath(string currentPath)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Farklı kaydet",
            FileName = Path.GetFileName(currentPath),
            InitialDirectory = Path.GetDirectoryName(currentPath),
            Filter = "Metin dosyası|*.txt|Tüm dosyalar|*.*",
            AddExtension = true
        };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private IReadOnlyList<string> PickMultipleFiles()
    {
        var dialog = new OpenFileDialog { Title = "Birleştirilecek dosyaları seçin", Filter = "Desteklenen dosyalar|*.txt;*.log;*.csv;*.tsv;*.jsonl|Tüm dosyalar|*.*", Multiselect = true, CheckFileExists = true };
        return dialog.ShowDialog(this) == true ? dialog.FileNames : Array.Empty<string>();
    }

    private string? PickSecondFile()
    {
        var dialog = new OpenFileDialog { Title = "İkinci dosyayı seçin", Filter = "Desteklenen dosyalar|*.txt;*.log;*.csv;*.tsv;*.jsonl|Tüm dosyalar|*.*", Multiselect = false, CheckFileExists = true };
        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }
    private void ToggleMaximize() => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) ToggleMaximize(); else DragMove();
    }

    private void Window_DragOver(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) await OpenFileAsync(files[0]);
    }

    private static bool IsSupported(string path) => new[] { ".txt", ".log", ".csv", ".tsv", ".jsonl" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Key == Key.O)
        {
            e.Handled = true;
            await OpenFileAsync();
        }
        else if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            e.Handled = true;
            if (_viewModel.Editor.SaveAsCommand.CanExecute(null)) _viewModel.Editor.SaveAsCommand.Execute(null);
        }
        else if (e.Key == Key.S)
        {
            e.Handled = true;
            if (_viewModel.Editor.SaveCommand.CanExecute(null)) _viewModel.Editor.SaveCommand.Execute(null);
        }
    }

    private static string FriendlyMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Bu dosyaya erişim izni yok.",
        FileNotFoundException => "Seçilen dosya artık mevcut değil.",
        DirectoryNotFoundException => "Dosyanın bulunduğu klasör artık mevcut değil.",
        DecoderFallbackException => "Dosyanın karakter kodlaması güvenilir biçimde çözülemedi.",
        IOException when (exception.HResult & 0xFFFF) == 32 => "Dosya şu anda başka bir uygulama tarafından kullanılıyor.",
        IOException => "Dosya okunurken bir G/Ç hatası oluştu. Dosyanın kullanımda olmadığını ve diskin erişilebilir olduğunu doğrulayın.",
        _ => "Dosya açılırken beklenmeyen bir hata oluştu. Ayrıntılar yerel günlük dosyasına yazıldı."
    };

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.Settings.LoadAsync();
        }
        catch (Exception exception)
        {
            App.ReportRecoverableError("Ayarlar okunamadı; güvenli varsayılanlar kullanılıyor.", exception);
        }

        try
        {
            _history = await App.HistoryStore.LoadAsync();
        }
        catch (Exception exception)
        {
            App.ReportRecoverableError("Son dosya geçmişi okunamadı; boş geçmişle devam ediliyor.", exception);
            _history = new AppHistory();
        }

        RefreshHomeHistory();
        _viewModel.Jobs.JobCompleted += JobCompleted;
        if (_viewModel.Settings.ReopenLastFile && _history.RecentFiles.FirstOrDefault(item => File.Exists(item.Path)) is { } recent)
        {
            await OpenFileAsync(recent.Path);
        }
    }

    private void OnRecoverableError(string message) => _viewModel.ShowNotification(message);

    private async Task AddRecentFileAsync(TextFileInfo file)
    {
        _history.RecentFiles.RemoveAll(item => item.Path.Equals(file.Path, StringComparison.OrdinalIgnoreCase));
        _history.RecentFiles.Insert(0, new RecentFile(file.Path, file.Size, DateTime.UtcNow, Path.GetExtension(file.Path).TrimStart('.').ToUpperInvariant()));
        if (_history.RecentFiles.Count > _viewModel.Settings.Settings.RecentFileCount)
            _history.RecentFiles.RemoveRange(_viewModel.Settings.Settings.RecentFileCount, _history.RecentFiles.Count - _viewModel.Settings.Settings.RecentFileCount);
        RefreshHomeHistory();
        await App.HistoryStore.SaveAsync(_history);
    }

    private async void JobCompleted(JobItemViewModel job, OperationResult result)
    {
        var day = DateTime.Now.ToString("yyyy-MM-dd");
        _history.ProcessedLinesByDay[day] = _history.ProcessedLinesByDay.GetValueOrDefault(day) + result.InputLines;
        _history.LastOperation = job.Operation;
        RefreshHomeHistory();
        try { await App.HistoryStore.SaveAsync(_history); }
        catch (Exception exception) { App.Logger.Error("History save failed", exception); }
    }

    private void RefreshHomeHistory()
    {
        _viewModel.Home.RecentFiles.Clear();
        foreach (var item in _history.RecentFiles.Where(item => File.Exists(item.Path))) _viewModel.Home.RecentFiles.Add(item);
        _viewModel.Home.NotifyRecentFilesChanged();
        _viewModel.Home.ProcessedToday = _history.ProcessedLinesByDay.GetValueOrDefault(DateTime.Now.ToString("yyyy-MM-dd"));
        _viewModel.Home.LastOperation = _history.LastOperation ?? "Henüz işlem yok";
        _viewModel.Home.ActiveJobs = _viewModel.Jobs.Jobs.Count(job => job.Status == JobStatus.Running);
    }

    private async void RemoveRecent(RecentFile recent)
    {
        _history.RecentFiles.RemoveAll(item => item.Path.Equals(recent.Path, StringComparison.OrdinalIgnoreCase));
        RefreshHomeHistory();
        try { await App.HistoryStore.SaveAsync(_history); }
        catch (Exception exception) { App.Logger.Error("Recent-file history save failed", exception); }
    }
}
