using System.Text.Json;
using GalaXako.Editor.Core.Abstractions;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Infrastructure.Storage;

public sealed class JsonHistoryStore : IHistoryStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;
    public JsonHistoryStore(string? rootDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GalaXakoEditor");
        _path = Path.Combine(root, "history.json");
    }
    public async Task<AppHistory> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return new AppHistory();
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true);
        return await JsonSerializer.DeserializeAsync<AppHistory>(stream, Options, cancellationToken) ?? new AppHistory();
    }
    public async Task SaveAsync(AppHistory history, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true))
                await JsonSerializer.SerializeAsync(stream, history, Options, cancellationToken);
            File.Move(temp, _path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
