using GalaXako.Editor.Core.Operations;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Pipeline;

public enum PipelineStepType { Clean, Filter, Dedupe, Extract, Sort }

public sealed class PipelineStep
{
    public required PipelineStepType Type { get; init; }
    public bool Enabled { get; set; } = true;
    public CleanOptions? Clean { get; init; }
    public List<FilterRule>? FilterRules { get; init; }
    public FilterLogic FilterLogic { get; init; } = FilterLogic.And;
    public DedupeOptions? Dedupe { get; init; }
    public ExtractOptions? Extract { get; init; }
    public SortOptions? Sort { get; init; }
}

public sealed class PipelineDefinition
{
    public required string Name { get; init; }
    public List<PipelineStep> Steps { get; init; } = [];
}

public sealed record BatchOptions(string OutputDirectory, string OutputSuffix = "_processed", bool PreserveDirectoryStructure = false, string? InputRoot = null, int MaxConcurrency = 2);
public sealed record BatchFileResult(string InputPath, string? OutputPath, OperationResult? Result, string? Error, bool Cancelled);
