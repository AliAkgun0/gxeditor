using GalaXako.Editor.Core.Pipeline;

namespace GalaXako.Editor.Core.Abstractions;

public interface IPipelineStore
{
    Task<IReadOnlyList<PipelineDefinition>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<PipelineDefinition> pipelines, CancellationToken cancellationToken = default);
}
