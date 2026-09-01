using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.App.ViewModels;

public sealed class JobItemViewModel : ViewModelBase
{
    private readonly Func<JobItemViewModel, Task> _retry;
    private readonly Action<JobItemViewModel> _remove;
    private CancellationTokenSource _cancellation = new();
    private JobStatus _status = JobStatus.Queued;
    private OperationProgress? _progress;
    private string? _error;

    public JobItemViewModel(string operation, string inputPath, string outputPath,
        Func<JobItemViewModel, Task> retry, Action<JobItemViewModel> remove)
    {
        Operation = operation; InputPath = inputPath; OutputPath = outputPath; _retry = retry; _remove = remove;
        CancelCommand = new RelayCommand(_ => _cancellation.Cancel(), _ => Status is JobStatus.Queued or JobStatus.Running);
        RetryCommand = new AsyncRelayCommand(_ => _retry(this), _ => Status is JobStatus.Failed or JobStatus.Cancelled);
        RemoveCommand = new RelayCommand(_ => _remove(this), _ => Status != JobStatus.Running);
        OpenOutputFolderCommand = new RelayCommand(_ => OpenOutputFolder(), _ => File.Exists(OutputPath) || Directory.Exists(OutputPath));
    }

    public string Operation { get; }
    public string InputPath { get; }
    public string OutputPath { get; }
    public string InputFileName => Path.GetFileName(InputPath);
    public CancellationToken CancellationToken => _cancellation.Token;
    public JobStatus Status { get => _status; private set { if (SetProperty(ref _status, value)) { OnPropertyChanged(nameof(StatusText)); RefreshCommands(); } } }
    public string StatusText => Status switch { JobStatus.Queued => "Sırada", JobStatus.Running => "Çalışıyor", JobStatus.Completed => "Tamamlandı", JobStatus.Cancelled => "İptal edildi", JobStatus.Failed => "Başarısız", _ => Status.ToString() };
    public OperationProgress? Progress { get => _progress; private set { if (SetProperty(ref _progress, value)) { OnPropertyChanged(nameof(Percentage)); OnPropertyChanged(nameof(ProgressSummary)); OnPropertyChanged(nameof(SpeedText)); OnPropertyChanged(nameof(EtaText)); } } }
    public double Percentage => Progress?.Percentage ?? 0;
    public string ProgressSummary => Progress is null ? "Bekliyor" : $"{FormatBytes(Progress.ProcessedBytes)} / {FormatBytes(Progress.TotalBytes)} · {Progress.ProcessedLines:N0} satır";
    public string SpeedText => Progress is { BytesPerSecond: > 0 } ? $"{FormatBytes((long)Progress.BytesPerSecond)}/sn" : "—";
    public string EtaText => Progress?.EstimatedRemaining is { } eta ? "ETA " + eta.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) : "ETA —";
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }
    public RelayCommand CancelCommand { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand OpenOutputFolderCommand { get; }

    public void MarkRunning()
    {
        if (_cancellation.IsCancellationRequested) { _cancellation.Dispose(); _cancellation = new CancellationTokenSource(); }
        Error = null; Status = JobStatus.Running;
    }
    public void Report(OperationProgress value) => Progress = value;
    public void MarkCompleted() => Status = JobStatus.Completed;
    public void MarkCancelled() => Status = JobStatus.Cancelled;
    public void MarkFailed(Exception exception) { Error = exception.Message; Status = JobStatus.Failed; }

    private void RefreshCommands() { CancelCommand.RaiseCanExecuteChanged(); RetryCommand.RaiseCanExecuteChanged(); RemoveCommand.RaiseCanExecuteChanged(); OpenOutputFolderCommand.RaiseCanExecuteChanged(); }
    private void OpenOutputFolder()
    {
        if (File.Exists(OutputPath)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{OutputPath}\"") { UseShellExecute = true });
        else if (Directory.Exists(OutputPath)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{OutputPath}\"") { UseShellExecute = true });
    }
    public void DisposeCancellation() => _cancellation.Dispose();
    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"]; var value = (double)Math.Max(0, bytes); var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return value.ToString("0.##", CultureInfo.CurrentCulture) + " " + units[unit];
    }
}

public sealed class JobsViewModel : ViewModelBase
{
    private readonly Dictionary<JobItemViewModel, Func<IProgress<OperationProgress>, CancellationToken, Task<OperationResult>>> _operations = [];
    public ObservableCollection<JobItemViewModel> Jobs { get; } = [];
    public bool HasJobs => Jobs.Count > 0;
    public event Action<JobItemViewModel, OperationResult>? JobCompleted;

    public async Task<JobItemViewModel> RunAsync(string operation, string inputPath, string outputPath,
        Func<IProgress<OperationProgress>, CancellationToken, Task<OperationResult>> action)
    {
        var job = new JobItemViewModel(operation, inputPath, outputPath, RetryAsync, Remove);
        _operations[job] = action; Jobs.Insert(0, job); OnPropertyChanged(nameof(HasJobs));
        await ExecuteAsync(job, action);
        return job;
    }

    private async Task ExecuteAsync(JobItemViewModel job, Func<IProgress<OperationProgress>, CancellationToken, Task<OperationResult>> action)
    {
        job.MarkRunning();
        try
        {
            var result = await action(new Progress<OperationProgress>(job.Report), job.CancellationToken);
            job.MarkCompleted();
            JobCompleted?.Invoke(job, result);
        }
        catch (OperationCanceledException) { job.MarkCancelled(); }
        catch (Exception exception) { job.MarkFailed(exception); App.Logger.Error($"Job failed: {job.Operation}", exception); }
    }

    private Task RetryAsync(JobItemViewModel job) => _operations.TryGetValue(job, out var action) ? ExecuteAsync(job, action) : Task.CompletedTask;
    private void Remove(JobItemViewModel job) { if (Jobs.Remove(job)) { _operations.Remove(job); job.DisposeCancellation(); OnPropertyChanged(nameof(HasJobs)); } }
}
