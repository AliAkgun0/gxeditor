namespace GalaXako.Editor.Core.Models;

public sealed class AppHistory
{
    public List<RecentFile> RecentFiles { get; set; } = [];
    public Dictionary<string, long> ProcessedLinesByDay { get; set; } = [];
    public string? LastOperation { get; set; }
}
