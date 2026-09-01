using System.Collections.Concurrent;
using GalaXako.Editor.Core.Models;
using GalaXako.Editor.Core.Operations;

namespace GalaXako.Editor.Core.Pipeline;

public sealed class PipelineRunner
{
    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, PipelineDefinition pipeline,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var steps = pipeline.Steps.Where(static step => step.Enabled).ToArray();
        if (steps.Length == 0) throw new InvalidOperationException("The pipeline has no enabled steps.");
        var tempRoot = Path.Combine(Path.GetTempPath(), "GalaXakoEditor", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var currentInput = inputPath;
        OperationResult? last = null;
        try
        {
            for (var index = 0; index < steps.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentOutput = index == steps.Length - 1 ? outputPath : Path.Combine(tempRoot, $"step-{index:D3}.tmp");
                last = await RunStepAsync(currentInput, currentOutput, steps[index], progress, cancellationToken);
                if (!string.Equals(currentInput, inputPath, StringComparison.OrdinalIgnoreCase) && File.Exists(currentInput)) File.Delete(currentInput);
                currentInput = currentOutput;
            }
            return last!;
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    private static Task<OperationResult> RunStepAsync(string inputPath, string outputPath, PipelineStep step,
        IProgress<OperationProgress>? progress, CancellationToken cancellationToken) => step.Type switch
    {
        PipelineStepType.Clean => new CleanOperation().RunAsync(inputPath, outputPath, step.Clean ?? new CleanOptions(), progress: progress, cancellationToken: cancellationToken),
        PipelineStepType.Filter => new FilterOperation().RunAsync(inputPath, outputPath, step.FilterRules ?? throw new InvalidOperationException("Filter rules are missing."), step.FilterLogic, progress, cancellationToken),
        PipelineStepType.Dedupe => new DedupeOperation().RunAsync(inputPath, outputPath, step.Dedupe ?? new DedupeOptions(), progress, cancellationToken),
        PipelineStepType.Extract => new ExtractOperation().RunAsync(inputPath, outputPath, step.Extract ?? throw new InvalidOperationException("Extractor settings are missing."), progress, cancellationToken),
        PipelineStepType.Sort => new SortOperation().RunAsync(inputPath, outputPath, step.Sort ?? new SortOptions(), progress, cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(step))
    };
}

public sealed class BatchPipelineRunner
{
    public async Task<IReadOnlyList<BatchFileResult>> RunAsync(IReadOnlyList<string> inputPaths, PipelineDefinition pipeline, BatchOptions options,
        IProgress<(string InputPath, OperationProgress Progress)>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        using var gate = new SemaphoreSlim(Math.Clamp(options.MaxConcurrency, 1, 8));
        var results = new ConcurrentDictionary<int, BatchFileResult>();
        var tasks = inputPaths.Select((path, index) => RunOneAsync(index, path)).ToArray();
        await Task.WhenAll(tasks);
        return results.OrderBy(static item => item.Key).Select(static item => item.Value).ToArray();

        async Task RunOneAsync(int index, string inputPath)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var destinationDirectory = options.OutputDirectory;
                if (options.PreserveDirectoryStructure && options.InputRoot is { Length: > 0 })
                {
                    var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(options.InputRoot, inputPath));
                    if (!string.IsNullOrEmpty(relativeDirectory)) destinationDirectory = Path.Combine(destinationDirectory, relativeDirectory);
                }
                Directory.CreateDirectory(destinationDirectory);
                var outputPath = Path.Combine(destinationDirectory, Path.GetFileNameWithoutExtension(inputPath) + options.OutputSuffix + Path.GetExtension(inputPath));
                if (File.Exists(outputPath)) throw new IOException("The output file already exists.");
                var localProgress = progress is null ? null : new Progress<OperationProgress>(value => progress.Report((inputPath, value)));
                var result = await new PipelineRunner().RunAsync(inputPath, outputPath, pipeline, localProgress, cancellationToken);
                results[index] = new BatchFileResult(inputPath, outputPath, result, null, false);
            }
            catch (OperationCanceledException)
            {
                results[index] = new BatchFileResult(inputPath, null, null, null, true);
            }
            catch (Exception exception)
            {
                results[index] = new BatchFileResult(inputPath, null, null, exception.Message, false);
            }
            finally { gate.Release(); }
        }
    }
}
