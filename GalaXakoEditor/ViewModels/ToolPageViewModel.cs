using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;
using GalaXako.Editor.Core.Operations;
using GalaXako.Editor.Core.Pipeline;

namespace GalaXako.Editor.App.ViewModels;

public sealed class FilterRuleItemViewModel : ViewModelBase
{
    private FilterCondition _condition = FilterCondition.Contains;
    private string _value = string.Empty;
    private string _secondValue = string.Empty;
    private bool _caseSensitive;
    public FilterCondition Condition { get => _condition; set => SetProperty(ref _condition, value); }
    public string Value { get => _value; set => SetProperty(ref _value, value); }
    public string SecondValue { get => _secondValue; set => SetProperty(ref _secondValue, value); }
    public bool CaseSensitive { get => _caseSensitive; set => SetProperty(ref _caseSensitive, value); }
    public FilterRule ToModel() => new(Condition, Value, string.IsNullOrWhiteSpace(SecondValue) ? null : SecondValue, CaseSensitive);
}

public sealed class PipelineStepItemViewModel(PipelineStepType type) : ViewModelBase
{
    public PipelineStepType Type { get; set; } = type;
}

public sealed class ToolPageViewModel : ViewModelBase
{
    private readonly Func<TextFileInfo?> _currentFile;
    private readonly Func<IReadOnlyList<string>> _pickMultipleFiles;
    private readonly Func<string?> _pickSecondFile;
    private readonly ITextFileService _fileService;
    private readonly JobsViewModel _jobs;
    private readonly IPipelineStore _pipelineStore;
    private readonly Func<int> _maxConcurrentJobs;
    private readonly Func<AppSettings> _settings;
    private string _status = "Bir giriş dosyası seçin.";
    private string _previewBefore = string.Empty;
    private string _previewAfter = string.Empty;
    private bool _isRunning;
    private bool _trimWhitespace = true;
    private bool _removeEmptyLines = true;
    private bool _normalizeWhitespace;
    private bool _removeDuplicates;
    private FilterLogic _filterLogic = FilterLogic.And;
    private ExtractorKind _extractorKind = ExtractorKind.Url;
    private string _customRegex = string.Empty;
    private SortMode _sortMode = SortMode.AlphabeticalAscending;
    private SplitMode _splitMode = SplitMode.LineCount;
    private long _splitValue = 500_000;
    private string _splitRegex = string.Empty;
    private string? _secondFilePath;
    private string _pipelineName = "Yeni Pipeline";
    private PipelineDefinition? _selectedPipeline;
    private string _delimiter = ",";
    private DelimiterOperationKind _delimiterOperation = DelimiterOperationKind.ExtractColumn;
    private int _delimiterColumn;
    private string _delimiterColumns = "0,1";
    private string _joinWith = "|";
    private string _minimumLength = string.Empty;
    private string _maximumLength = string.Empty;
    private TextCaseTransform _caseTransform;
    private bool _extractUnique = true;
    private bool _extractSort;
    private bool _extractCaseSensitive;
    private bool _extractCsv;
    private CompareMode _compareMode = CompareMode.Different;

    public ToolPageViewModel(string key, string title, string description, IEnumerable<string> capabilities,
        Func<TextFileInfo?> currentFile, JobsViewModel jobs, Func<IReadOnlyList<string>> pickMultipleFiles,
        Func<string?> pickSecondFile, IPipelineStore pipelineStore, Func<int> maxConcurrentJobs, Func<AppSettings> settings)
    {
        Key = key; Title = title; Description = description; Capabilities = capabilities.ToArray();
        _currentFile = currentFile; _jobs = jobs; _pickMultipleFiles = pickMultipleFiles; _pickSecondFile = pickSecondFile; _pipelineStore = pipelineStore;
        _maxConcurrentJobs = maxConcurrentJobs;
        _settings = settings;
        _fileService = new TextFileService();
        FilterRules.Add(new FilterRuleItemViewModel());
        PipelineSteps.Add(new PipelineStepItemViewModel(PipelineStepType.Clean));
        PipelineSteps.Add(new PipelineStepItemViewModel(PipelineStepType.Dedupe));
        RunCommand = new AsyncRelayCommand(_ => RunAsync(), _ => HasInput && !IsRunning);
        PreviewCommand = new AsyncRelayCommand(_ => PreviewAsync(), _ => CanPreview());
        CancelLatestCommand = new RelayCommand(_ => _jobs.Jobs.FirstOrDefault()?.CancelCommand.Execute(null), _ => _jobs.Jobs.FirstOrDefault()?.Status == JobStatus.Running);
        AddRuleCommand = new RelayCommand(_ => FilterRules.Add(new FilterRuleItemViewModel()));
        RemoveRuleCommand = new RelayCommand(rule => { if (rule is FilterRuleItemViewModel item && FilterRules.Count > 1) FilterRules.Remove(item); });
        ChooseFilesCommand = new RelayCommand(_ => ChooseFiles());
        ChooseSecondFileCommand = new RelayCommand(_ => { SecondFilePath = _pickSecondFile(); });
        AddPipelineStepCommand = new RelayCommand(_ => PipelineSteps.Add(new PipelineStepItemViewModel(PipelineStepType.Clean)));
        RemovePipelineStepCommand = new RelayCommand(step => { if (step is PipelineStepItemViewModel item) PipelineSteps.Remove(item); });
        MovePipelineStepUpCommand = new RelayCommand(step => MoveStep(step as PipelineStepItemViewModel, -1));
        MovePipelineStepDownCommand = new RelayCommand(step => MoveStep(step as PipelineStepItemViewModel, 1));
        SavePipelineCommand = new AsyncRelayCommand(_ => SavePipelineAsync());
        LoadPipelinesCommand = new AsyncRelayCommand(_ => LoadPipelinesAsync());
        ChooseBatchFilesCommand = new RelayCommand(_ => ChooseBatchFiles());
        RunBatchCommand = new AsyncRelayCommand(_ => RunBatchAsync(), _ => BatchFiles.Count > 0 && PipelineSteps.Count > 0);
    }

    public string Key { get; }
    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<string> Capabilities { get; }
    public bool IsClean => Key == "clean";
    public bool IsFilter => Key == "filter";
    public bool IsExtract => Key == "extract";
    public bool IsSort => Key == "sort";
    public bool IsSplitMerge => Key == "splitmerge";
    public bool IsCompare => Key == "compare";
    public bool IsPipeline => Key == "pipeline";
    public bool IsDelimiter => Key == "delimiter";
    public bool SupportsPreview => IsClean || IsFilter || IsExtract || IsSort || IsDelimiter || IsSplitMerge;
    public bool HasInput => _currentFile() is not null;
    public string InputPath => _currentFile()?.Path ?? "Dosya seçilmedi";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string PreviewBefore { get => _previewBefore; private set => SetProperty(ref _previewBefore, value); }
    public string PreviewAfter { get => _previewAfter; private set => SetProperty(ref _previewAfter, value); }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) { RunCommand.RaiseCanExecuteChanged(); PreviewCommand.RaiseCanExecuteChanged(); } } }
    public bool TrimWhitespace { get => _trimWhitespace; set => SetProperty(ref _trimWhitespace, value); }
    public bool RemoveEmptyLines { get => _removeEmptyLines; set => SetProperty(ref _removeEmptyLines, value); }
    public bool NormalizeWhitespace { get => _normalizeWhitespace; set => SetProperty(ref _normalizeWhitespace, value); }
    public bool RemoveDuplicates { get => _removeDuplicates; set => SetProperty(ref _removeDuplicates, value); }
    public FilterLogic FilterLogic { get => _filterLogic; set => SetProperty(ref _filterLogic, value); }
    public ExtractorKind ExtractorKind { get => _extractorKind; set { if (SetProperty(ref _extractorKind, value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public string CustomRegex { get => _customRegex; set { if (SetProperty(ref _customRegex, value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public SortMode SortMode { get => _sortMode; set => SetProperty(ref _sortMode, value); }
    public SplitMode SplitMode { get => _splitMode; set { if (SetProperty(ref _splitMode, value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public long SplitValue { get => _splitValue; set { if (SetProperty(ref _splitValue, Math.Max(1, value))) PreviewCommand.RaiseCanExecuteChanged(); } }
    public string SplitRegex { get => _splitRegex; set { if (SetProperty(ref _splitRegex, value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public string? SecondFilePath { get => _secondFilePath; private set => SetProperty(ref _secondFilePath, value); }
    public string PipelineName { get => _pipelineName; set => SetProperty(ref _pipelineName, value); }
    public PipelineDefinition? SelectedPipeline
    {
        get => _selectedPipeline;
        set { if (SetProperty(ref _selectedPipeline, value) && value is not null) ApplyPipeline(value); }
    }
    public string Delimiter { get => _delimiter; set { if (SetProperty(ref _delimiter, value == "TAB" ? "\t" : value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public DelimiterOperationKind DelimiterOperation { get => _delimiterOperation; set { if (SetProperty(ref _delimiterOperation, value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public int DelimiterColumn { get => _delimiterColumn; set => SetProperty(ref _delimiterColumn, Math.Max(0, value)); }
    public string DelimiterColumns { get => _delimiterColumns; set { if (SetProperty(ref _delimiterColumns, value)) PreviewCommand.RaiseCanExecuteChanged(); } }
    public string JoinWith { get => _joinWith; set => SetProperty(ref _joinWith, value); }
    public string MinimumLength { get => _minimumLength; set => SetProperty(ref _minimumLength, value); }
    public string MaximumLength { get => _maximumLength; set => SetProperty(ref _maximumLength, value); }
    public TextCaseTransform CaseTransform { get => _caseTransform; set => SetProperty(ref _caseTransform, value); }
    public bool ExtractUnique { get => _extractUnique; set => SetProperty(ref _extractUnique, value); }
    public bool ExtractSort { get => _extractSort; set => SetProperty(ref _extractSort, value); }
    public bool ExtractCaseSensitive { get => _extractCaseSensitive; set => SetProperty(ref _extractCaseSensitive, value); }
    public bool ExtractCsv { get => _extractCsv; set => SetProperty(ref _extractCsv, value); }
    public CompareMode CompareMode { get => _compareMode; set => SetProperty(ref _compareMode, value); }
    public ObservableCollection<FilterRuleItemViewModel> FilterRules { get; } = [];
    public ObservableCollection<string> MergeFiles { get; } = [];
    public ObservableCollection<PipelineStepItemViewModel> PipelineSteps { get; } = [];
    public ObservableCollection<PipelineDefinition> SavedPipelines { get; } = [];
    public ObservableCollection<string> BatchFiles { get; } = [];
    public Array FilterConditions => Enum.GetValues<FilterCondition>();
    public Array FilterLogics => Enum.GetValues<FilterLogic>();
    public Array ExtractorKinds => Enum.GetValues<ExtractorKind>();
    public Array SortModes => Enum.GetValues<SortMode>();
    public Array SplitModes => Enum.GetValues<SplitMode>();
    public Array PipelineStepTypes => Enum.GetValues<PipelineStepType>();
    public Array DelimiterOperationKinds => Enum.GetValues<DelimiterOperationKind>();
    public Array CaseTransforms => Enum.GetValues<TextCaseTransform>();
    public Array CompareModes => Enum.GetValues<CompareMode>();
    public AsyncRelayCommand RunCommand { get; }
    public AsyncRelayCommand PreviewCommand { get; }
    public RelayCommand CancelLatestCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand RemoveRuleCommand { get; }
    public RelayCommand ChooseFilesCommand { get; }
    public RelayCommand ChooseSecondFileCommand { get; }
    public RelayCommand AddPipelineStepCommand { get; }
    public RelayCommand RemovePipelineStepCommand { get; }
    public RelayCommand MovePipelineStepUpCommand { get; }
    public RelayCommand MovePipelineStepDownCommand { get; }
    public AsyncRelayCommand SavePipelineCommand { get; }
    public AsyncRelayCommand LoadPipelinesCommand { get; }
    public RelayCommand ChooseBatchFilesCommand { get; }
    public AsyncRelayCommand RunBatchCommand { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(HasInput)); OnPropertyChanged(nameof(InputPath)); RunCommand.RaiseCanExecuteChanged(); PreviewCommand.RaiseCanExecuteChanged();
        Status = HasInput ? "Yapılandırma hazır." : "Bir giriş dosyası seçin.";
    }

    private async Task RunAsync()
    {
        var file = _currentFile();
        if (file is null) return;
        IsRunning = true;
        try
        {
            var preferences = _settings();
            var suffix = string.IsNullOrWhiteSpace(preferences.OutputSuffix) ? "_processed" : preferences.OutputSuffix;
            var output = _fileService.CreateOutputPath(file.Path, suffix, preferences.DefaultOutputFolder);
            JobItemViewModel job;
            if (IsClean && RemoveDuplicates)
            {
                var pipeline = new PipelineDefinition { Name = "Clean + Dedupe", Steps = [new PipelineStep { Type = PipelineStepType.Clean, Clean = CleanOptions() }, new PipelineStep { Type = PipelineStepType.Dedupe, Dedupe = new DedupeOptions() }] };
                job = await _jobs.RunAsync("Temizle + tekrarları kaldır", file.Path, output, (p, ct) => new PipelineRunner().RunAsync(file.Path, output, pipeline, p, ct));
            }
            else if (IsClean) job = await _jobs.RunAsync("Temizle", file.Path, output, (p, ct) => new CleanOperation().RunAsync(file.Path, output, CleanOptions(), progress: p, cancellationToken: ct));
            else if (IsFilter) job = await _jobs.RunAsync("Filtrele", file.Path, output, (p, ct) => new FilterOperation().RunAsync(file.Path, output, FilterRules.Select(rule => rule.ToModel()), FilterLogic, p, ct));
            else if (IsExtract) job = await _jobs.RunAsync("Ayıkla", file.Path, output, (p, ct) => new ExtractOperation().RunAsync(file.Path, output, ExtractOptions(), p, ct));
            else if (IsSort) job = await _jobs.RunAsync("Sırala", file.Path, output, (p, ct) => new SortOperation().RunAsync(file.Path, output, new SortOptions(SortMode), p, ct));
            else if (IsDelimiter) job = await _jobs.RunAsync("Sütun işlemi", file.Path, output, (p, ct) => new DelimiterOperation().RunAsync(file.Path, output, BuildDelimiterOptions(), p, ct));
            else if (IsSplitMerge && MergeFiles.Count > 0) job = await _jobs.RunAsync("Birleştir", file.Path, output, (p, ct) => new MergeOperation().RunAsync(MergeFiles, output, new MergeOptions(), p, ct));
            else if (IsSplitMerge)
            {
                var directory = _fileService.CreateOutputDirectory(file.Path, "_parts", preferences.DefaultOutputFolder);
                job = await _jobs.RunAsync("Böl", file.Path, directory, async (p, ct) => { var result = await new SplitOperation().RunAsync(file.Path, directory, new SplitOptions(SplitMode, SplitValue, string.IsNullOrWhiteSpace(SplitRegex) ? null : SplitRegex), p, ct); return new OperationResult(directory, result.InputLines, result.InputLines, result.OutputPaths.Count, result.Elapsed); });
            }
            else if (IsCompare)
            {
                if (string.IsNullOrWhiteSpace(SecondFilePath)) { Status = "Karşılaştırmak için ikinci dosyayı seçin."; return; }
                job = await _jobs.RunAsync("Karşılaştır", file.Path, output, async (p, ct) => { var result = await new CompareOperation().RunAsync(file.Path, SecondFilePath, output, CompareMode, progress: p, cancellationToken: ct); return new OperationResult(output, 0, result.OnlyInA + result.OnlyInB, result.InBoth, result.Elapsed); });
            }
            else if (IsPipeline)
            {
                var pipeline = BuildPipeline();
                job = await _jobs.RunAsync("Pipeline", file.Path, output, (p, ct) => new PipelineRunner().RunAsync(file.Path, output, pipeline, p, ct));
            }
            else return;
            Status = job.Status == JobStatus.Completed ? $"Tamamlandı: {job.OutputPath}" : job.StatusText;
        }
        finally { IsRunning = false; }
    }

    private async Task PreviewAsync()
    {
        var file = _currentFile(); if (file is null) return;
        IsRunning = true;
        try
        {
            var encoding = await EncodingDetector.DetectAsync(file.Path);
            using var reader = new StreamReader(file.Path, encoding.Encoding, false, 64 * 1024);
            var before = new List<string>();
            while (before.Count < 50 && await reader.ReadLineAsync() is { } line) before.Add(line);
            PreviewBefore = string.Join(Environment.NewLine, before);
            IEnumerable<string> after = before;
            if (IsClean) after = before.Select(line => CleanOperation.Transform(line, CleanOptions())).Where(static line => line is not null)!;
            else if (IsFilter) { var filter = new CompiledFilter(FilterRules.Select(rule => rule.ToModel()), FilterLogic); after = before.Where(filter.IsMatch); }
            else if (IsExtract) after = ExtractOperation.ExtractSample(PreviewBefore, ExtractOptions());
            else if (IsSort) after = before.Order(SortOperation.CreateComparer(new SortOptions(SortMode)));
            else if (IsDelimiter) after = GalaXako.Editor.Core.Operations.DelimiterOperation.TransformSample(before, BuildDelimiterOptions());
            else if (IsSplitMerge)
            {
                var parts = SplitOperation.SplitSample(before,
                    new SplitOptions(SplitMode, SplitValue, string.IsNullOrWhiteSpace(SplitRegex) ? null : SplitRegex),
                    encoding.Encoding);
                after = parts.SelectMany((part, index) => new[] { $"— Parça {index + 1} —" }.Concat(part));
            }
            PreviewAfter = string.Join(Environment.NewLine, after);
            Status = "İlk 50 satır, gerçek işlem kurallarıyla önizlendi.";
        }
        catch (Exception exception) { Status = exception.Message; }
        finally { IsRunning = false; }
    }

    private CleanOptions CleanOptions() => new(TrimWhitespace, RemoveEmptyLines, true, NormalizeWhitespace,
        MinimumLength: int.TryParse(MinimumLength, out var min) ? min : null,
        MaximumLength: int.TryParse(MaximumLength, out var max) ? max : null,
        CaseTransform: CaseTransform);
    private ExtractOptions ExtractOptions() => new(ExtractorKind, string.IsNullOrWhiteSpace(CustomRegex) ? null : CustomRegex, ExtractUnique, ExtractSort, ExtractCaseSensitive, ExtractCsv);
    private PipelineDefinition BuildPipeline() => new() { Name = PipelineName, Steps = PipelineSteps.Select(step => step.Type switch { PipelineStepType.Clean => new PipelineStep { Type = step.Type, Clean = CleanOptions() }, PipelineStepType.Filter => new PipelineStep { Type = step.Type, FilterRules = FilterRules.Select(rule => rule.ToModel()).ToList(), FilterLogic = FilterLogic }, PipelineStepType.Dedupe => new PipelineStep { Type = step.Type, Dedupe = new DedupeOptions() }, PipelineStepType.Extract => new PipelineStep { Type = step.Type, Extract = ExtractOptions() }, PipelineStepType.Sort => new PipelineStep { Type = step.Type, Sort = new SortOptions(SortMode) }, _ => throw new ArgumentOutOfRangeException() }).ToList() };
    private DelimiterOptions BuildDelimiterOptions()
    {
        IReadOnlyList<int>? columns = DelimiterOperation is DelimiterOperationKind.ReorderColumns or DelimiterOperationKind.JoinColumns
            ? DelimiterColumns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToArray()
            : null;
        var filter = FilterRules.FirstOrDefault()?.ToModel();
        return new DelimiterOptions(Delimiter, DelimiterOperation, DelimiterColumn, columns, JoinWith, filter);
    }
    private bool CanPreview()
    {
        if (!HasInput || IsRunning || !SupportsPreview) return false;
        try
        {
            if (IsExtract && ExtractorKind == ExtractorKind.CustomRegex)
            {
                if (string.IsNullOrWhiteSpace(CustomRegex)) return false;
                _ = new Regex(CustomRegex, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            }
            if (IsDelimiter)
            {
                if (string.IsNullOrEmpty(Delimiter)) return false;
                _ = BuildDelimiterOptions();
            }
            if (IsSplitMerge)
            {
                if (MergeFiles.Count > 0) return false;
                if (SplitMode is SplitMode.BeforeRegex or SplitMode.AfterRegex)
                {
                    if (string.IsNullOrWhiteSpace(SplitRegex)) return false;
                    _ = new Regex(SplitRegex, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
                }
                else if (SplitValue <= 0) return false;
            }
            return true;
        }
        catch (ArgumentException) { return false; }
    }
    private void ChooseFiles()
    {
        MergeFiles.Clear();
        foreach (var path in _pickMultipleFiles()) MergeFiles.Add(path);
        PreviewCommand.RaiseCanExecuteChanged();
        Status = MergeFiles.Count > 0
            ? "Birleştirme seçildi. Önizleme bölme işlemi için kullanılabilir; dosya listesini temizleyerek yeniden etkinleştirebilirsiniz."
            : "Bölme önizlemesi kullanılabilir.";
    }
    private void ChooseBatchFiles() { BatchFiles.Clear(); foreach (var path in _pickMultipleFiles()) BatchFiles.Add(path); RunBatchCommand.RaiseCanExecuteChanged(); }
    private async Task RunBatchAsync()
    {
        var pipeline = BuildPipeline();
        using var gate = new SemaphoreSlim(Math.Clamp(_maxConcurrentJobs(), 1, 8));
        var tasks = BatchFiles.Select(async input =>
        {
            await gate.WaitAsync();
            try
            {
                var preferences = _settings();
                var output = _fileService.CreateOutputPath(input, string.IsNullOrWhiteSpace(preferences.OutputSuffix) ? "_processed" : preferences.OutputSuffix, preferences.DefaultOutputFolder);
                return await _jobs.RunAsync("Batch pipeline", input, output, (p, ct) => new PipelineRunner().RunAsync(input, output, pipeline, p, ct));
            }
            finally { gate.Release(); }
        });
        var completedJobs = await Task.WhenAll(tasks);
        var completed = completedJobs.Count(job => job.Status == JobStatus.Completed);
        var failed = completedJobs.Count(job => job.Status == JobStatus.Failed);
        var cancelled = completedJobs.Count(job => job.Status == JobStatus.Cancelled);
        Status = $"Batch sonucu: {completed:N0} tamamlandı, {failed:N0} başarısız, {cancelled:N0} iptal.";
    }
    private void MoveStep(PipelineStepItemViewModel? step, int delta) { if (step is null) return; var old = PipelineSteps.IndexOf(step); var target = old + delta; if (old >= 0 && target >= 0 && target < PipelineSteps.Count) PipelineSteps.Move(old, target); }
    private async Task SavePipelineAsync() { var all = (await _pipelineStore.LoadAsync()).Where(item => !item.Name.Equals(PipelineName, StringComparison.OrdinalIgnoreCase)).ToList(); all.Add(BuildPipeline()); await _pipelineStore.SaveAsync(all); await LoadPipelinesAsync(); Status = "Pipeline yerel olarak kaydedildi."; }
    private async Task LoadPipelinesAsync() { SavedPipelines.Clear(); foreach (var pipeline in await _pipelineStore.LoadAsync()) SavedPipelines.Add(pipeline); }
    private void ApplyPipeline(PipelineDefinition pipeline)
    {
        PipelineName = pipeline.Name; PipelineSteps.Clear();
        foreach (var step in pipeline.Steps) PipelineSteps.Add(new PipelineStepItemViewModel(step.Type));
        Status = $"'{pipeline.Name}' pipeline'ı yüklendi.";
    }
}
