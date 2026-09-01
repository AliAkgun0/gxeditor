using System.Text.Json;
using System.Text.Json.Serialization;
using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.Pipeline;

namespace GalaXako.Editor.Infrastructure.Storage;

public sealed class JsonPipelineStore : IPipelineStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly string _path;

    public JsonPipelineStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GalaXakoEditor");
        _path = Path.Combine(root, "pipelines.json");
    }

    public async Task<IReadOnlyList<PipelineDefinition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return Array.Empty<PipelineDefinition>();
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        return await JsonSerializer.DeserializeAsync<List<PipelineDefinition>>(stream, Options, cancellationToken) ?? [];
    }

    public async Task SaveAsync(IReadOnlyList<PipelineDefinition> pipelines, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tempPath = _path + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true))
            {
                await JsonSerializer.SerializeAsync(stream, pipelines, Options, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(tempPath, _path, true);
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }
}
