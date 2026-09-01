using System.Globalization;
using System.IO;
using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;
using System.Collections.ObjectModel;

namespace GalaXako.Editor.App.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    private readonly ITextFileService _textFileService;
    private readonly Func<string, string?> _saveAsPicker;
    private LargeFileDocument? _largeDocument;
    private TextFileInfo? _file;
    private string _text = string.Empty;
    private string _previewText = string.Empty;
    private string _statusMessage = "Dosya seçilmedi";
    private string _busyMessage = string.Empty;
    private bool _isBusy;
    private long _currentFirstLine;
    private long _currentLastLine;
    private long _goToLine = 1;
    private long _goToByte;
    private int _chunkLineCount = 5_000;
    private double _zoomFontSize = 14;
    private string _searchQuery = string.Empty;
    private bool _searchRegex;
    private bool _searchCaseSensitive;
    private string _searchStatus = string.Empty;
    private string _statisticsText = string.Empty;
    private CancellationTokenSource? _searchCancellation;

    public EditorViewModel(ITextFileService textFileService, Func<string, string?> saveAsPicker)
    {
        _textFileService = textFileService;
        _saveAsPicker = saveAsPicker;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync(), _ => IsNormalMode && !IsBusy);
        SaveAsCommand = new AsyncRelayCommand(_ => SaveAsAsync(), _ => IsNormalMode && !IsBusy);
        BeginningCommand = new AsyncRelayCommand(_ => LoadChunkAsync(1), _ => IsLargeMode && !IsBusy);
        PreviousChunkCommand = new AsyncRelayCommand(_ => LoadChunkAsync(Math.Max(1, CurrentFirstLine - ChunkLineCount)), _ => IsLargeMode && !IsBusy);
        NextChunkCommand = new AsyncRelayCommand(_ => LoadChunkAsync(Math.Min(LineCount, CurrentFirstLine + ChunkLineCount)), _ => IsLargeMode && !IsBusy);
        EndCommand = new AsyncRelayCommand(_ => LoadChunkAsync(Math.Max(1, LineCount - ChunkLineCount + 1)), _ => IsLargeMode && !IsBusy);
        GoToLineCommand = new AsyncRelayCommand(_ => LoadChunkAsync(GoToLine), _ => IsLargeMode && !IsBusy);
        GoToByteCommand = new AsyncRelayCommand(_ => LoadChunkByByteAsync(GoToByte), _ => IsLargeMode && !IsBusy);
        GoToPercentCommand = new AsyncRelayCommand(value => GoToPercentAsync(value), _ => IsLargeMode && !IsBusy);
        SearchLargeFileCommand = new AsyncRelayCommand(_ => SearchLargeFileAsync(), _ => IsLargeMode && !IsBusy && !string.IsNullOrWhiteSpace(SearchQuery));
        CancelSearchCommand = new RelayCommand(_ => _searchCancellation?.Cancel());
        NavigateSearchResultCommand = new AsyncRelayCommand(result => NavigateSearchResultAsync(result as LargeFileSearchResult), _ => IsLargeMode && !IsBusy);
        AnalyzeCommand = new AsyncRelayCommand(_ => AnalyzeAsync(), _ => HasFile && !IsBusy);
    }

    public TextFileInfo? File
    {
        get => _file;
        private set
        {
            if (!SetProperty(ref _file, value)) return;
            OnPropertyChanged(nameof(HasFile)); OnPropertyChanged(nameof(IsNormalMode)); OnPropertyChanged(nameof(IsLargeMode));
            OnPropertyChanged(nameof(FileName)); OnPropertyChanged(nameof(FilePath)); OnPropertyChanged(nameof(FileSize));
            OnPropertyChanged(nameof(EncodingName)); OnPropertyChanged(nameof(LineEnding)); OnPropertyChanged(nameof(LastModified));
        }
    }
    public bool HasFile => File is not null;
    public bool IsNormalMode => File?.Mode == FileOpenMode.Normal;
    public bool IsLargeMode => File?.Mode == FileOpenMode.Large;
    public string FileName => File is null ? "Dosya seçilmedi" : Path.GetFileName(File.Path);
    public string FilePath => File?.Path ?? string.Empty;
    public string FileSize => File is null ? "—" : FormatBytes(File.Size);
    public string EncodingName => File?.Encoding.DisplayName ?? "—";
    public string LineEnding => File?.LineEnding ?? "—";
    public string LastModified => File?.LastModifiedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—";
    public long LineCount { get; private set; }
    public long CharacterCount { get; private set; }
    public string Text { get => _text; set { if (SetProperty(ref _text, value)) UpdateNormalStatistics(); } }
    public string PreviewText { get => _previewText; private set => SetProperty(ref _previewText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string BusyMessage { get => _busyMessage; private set => SetProperty(ref _busyMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public long CurrentFirstLine { get => _currentFirstLine; private set => SetProperty(ref _currentFirstLine, value); }
    public long CurrentLastLine { get => _currentLastLine; private set => SetProperty(ref _currentLastLine, value); }
    public long GoToLine { get => _goToLine; set => SetProperty(ref _goToLine, Math.Max(1, value)); }
    public long GoToByte { get => _goToByte; set => SetProperty(ref _goToByte, Math.Max(0, value)); }
    public int ChunkLineCount { get => _chunkLineCount; set => SetProperty(ref _chunkLineCount, Math.Clamp(value, 1_000, 50_000)); }
    public double ZoomFontSize { get => _zoomFontSize; set => SetProperty(ref _zoomFontSize, Math.Clamp(value, 9, 32)); }
    public string SearchQuery { get => _searchQuery; set { if (SetProperty(ref _searchQuery, value)) SearchLargeFileCommand.RaiseCanExecuteChanged(); } }
    public bool SearchRegex { get => _searchRegex; set => SetProperty(ref _searchRegex, value); }
    public bool SearchCaseSensitive { get => _searchCaseSensitive; set => SetProperty(ref _searchCaseSensitive, value); }
    public string SearchStatus { get => _searchStatus; private set => SetProperty(ref _searchStatus, value); }
    public string StatisticsText { get => _statisticsText; private set => SetProperty(ref _statisticsText, value); }
    public ObservableCollection<LargeFileSearchResult> SearchResults { get; } = [];
    public AsyncRelayCommand SaveCommand { get; }
    public AsyncRelayCommand SaveAsCommand { get; }
    public AsyncRelayCommand BeginningCommand { get; }
    public AsyncRelayCommand PreviousChunkCommand { get; }
    public AsyncRelayCommand NextChunkCommand { get; }
    public AsyncRelayCommand EndCommand { get; }
    public AsyncRelayCommand GoToLineCommand { get; }
    public AsyncRelayCommand GoToByteCommand { get; }
    public AsyncRelayCommand GoToPercentCommand { get; }
    public AsyncRelayCommand SearchLargeFileCommand { get; }
    public RelayCommand CancelSearchCommand { get; }
    public AsyncRelayCommand NavigateSearchResultCommand { get; }
    public AsyncRelayCommand AnalyzeCommand { get; }

    public async Task OpenAsync(TextFileInfo file, AppSettings settings, IProgress<OperationProgress>? progress, CancellationToken cancellationToken)
    {
        IsBusy = true;
        BusyMessage = "Dosya inceleniyor";
        try
        {
            File = file;
            SearchResults.Clear(); SearchStatus = string.Empty; StatisticsText = string.Empty;
            ChunkLineCount = settings.LargeFileChunkLineCount;
            if (file.Mode == FileOpenMode.Normal)
            {
                Text = await _textFileService.LoadNormalAsync(file, cancellationToken);
                _largeDocument = null;
                StatusMessage = "Normal Düzenleme Modu";
            }
            else
            {
                Text = string.Empty;
                _largeDocument = new LargeFileDocument(file, settings.SparseIndexIntervalLines);
                BusyMessage = "Seyrek satır indeksi oluşturuluyor";
                await _largeDocument.BuildIndexAsync(progress, cancellationToken);
                LineCount = _largeDocument.Index.LineCount;
                OnPropertyChanged(nameof(LineCount));
                await LoadChunkCoreAsync(1, cancellationToken);
                StatusMessage = "Büyük Dosya Modu · salt okunur önizleme";
            }
        }
        finally
        {
            IsBusy = false;
            BusyMessage = string.Empty;
        }
    }

    private async Task SaveAsync()
    {
        if (File is null) return;
        IsBusy = true;
        try { await _textFileService.SaveSafeAsync(File.Path, Text, File.Encoding); StatusMessage = "Dosya güvenli biçimde kaydedildi"; }
        finally { IsBusy = false; }
    }

    private async Task SaveAsAsync()
    {
        if (File is null) return;
        var selected = _saveAsPicker(File.Path);
        if (string.IsNullOrWhiteSpace(selected)) return;
        IsBusy = true;
        try { await _textFileService.SaveSafeAsync(selected, Text, File.Encoding); StatusMessage = "Yeni dosya kaydedildi"; }
        finally { IsBusy = false; }
    }

    private async Task LoadChunkAsync(long line)
    {
        IsBusy = true;
        try { await LoadChunkCoreAsync(line, CancellationToken.None); }
        finally { IsBusy = false; }
    }

    private async Task LoadChunkCoreAsync(long line, CancellationToken cancellationToken)
    {
        if (_largeDocument is null) return;
        var chunk = await _largeDocument.ReadChunkByLineAsync(line, ChunkLineCount, cancellationToken);
        ApplyChunk(chunk);
    }

    private async Task LoadChunkByByteAsync(long offset)
    {
        if (_largeDocument is null) return;
        IsBusy = true;
        try { ApplyChunk(await _largeDocument.ReadChunkByByteOffsetAsync(offset, ChunkLineCount)); }
        finally { IsBusy = false; }
    }

    private Task GoToPercentAsync(object? value)
    {
        var percent = value is string text && double.TryParse(text, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        return LoadChunkAsync(Math.Max(1, (long)Math.Round(LineCount * percent / 100d)));
    }

    private async Task SearchLargeFileAsync()
    {
        if (File is null || !IsLargeMode) return;
        _searchCancellation?.Cancel(); _searchCancellation?.Dispose(); _searchCancellation = new CancellationTokenSource();
        SearchResults.Clear(); SearchStatus = "Aranıyor…";
        try
        {
            var results = new Progress<LargeFileSearchResult>(result => SearchResults.Add(result));
            var progress = new Progress<OperationProgress>(value => SearchStatus = $"{value.Percentage:0.0}% · {value.ProcessedLines:N0} satır · {value.AffectedLines:N0} eşleşme");
            var summary = await new LargeFileSearchService().SearchAsync(File,
                new SearchOptions(SearchQuery, SearchCaseSensitive, SearchRegex), results, progress, _searchCancellation.Token);
            SearchStatus = $"{summary.MatchCount:N0} eşleşme" + (summary.ResultLimitReached ? " · ilk 10.000 sonuç gösteriliyor" : string.Empty);
        }
        catch (OperationCanceledException) { SearchStatus = "Arama iptal edildi."; }
        catch (Exception exception) { SearchStatus = exception.Message; }
    }

    private Task NavigateSearchResultAsync(LargeFileSearchResult? result)
    {
        if (result is null) return Task.CompletedTask;
        GoToByte = result.ByteOffset;
        return LoadChunkByByteAsync(result.ByteOffset);
    }

    private async Task AnalyzeAsync()
    {
        if (File is null) return;
        IsBusy = true; BusyMessage = "Dosya istatistikleri hesaplanıyor";
        try
        {
            var stats = await new FileStatisticsService().AnalyzeAsync(File);
            StatisticsText = $"{stats.LineCount:N0} satır · {stats.EmptyLines:N0} boş · en kısa {stats.ShortestLine:N0} · en uzun {stats.LongestLine:N0} · ortalama {stats.AverageLineLength:N1} karakter";
        }
        finally { IsBusy = false; BusyMessage = string.Empty; }
    }

    private void ApplyChunk(PreviewChunk chunk)
    {
        PreviewText = string.Join(Environment.NewLine, chunk.Lines);
        CurrentFirstLine = chunk.FirstLineNumber;
        CurrentLastLine = chunk.FirstLineNumber + Math.Max(0, chunk.Lines.Count - 1);
        StatusMessage = $"Görüntülenen satırlar {CurrentFirstLine:N0} – {CurrentLastLine:N0}";
    }

    private void UpdateNormalStatistics()
    {
        CharacterCount = Text.Length;
        LineCount = Text.Length == 0 ? 0 : 1 + Text.Count(static character => character == '\n');
        OnPropertyChanged(nameof(CharacterCount)); OnPropertyChanged(nameof(LineCount));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
