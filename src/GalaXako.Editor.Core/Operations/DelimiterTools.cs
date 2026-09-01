namespace GalaXako.Editor.Core.Operations;

public static class DelimiterTools
{
    public static string[] Parse(string line, string delimiter) => line.Split([delimiter], StringSplitOptions.None);
    public static string ExtractColumn(string line, string delimiter, int column) => GetColumn(Parse(line, delimiter), column);
    public static string RemoveColumn(string line, string delimiter, int column)
    {
        var columns = Parse(line, delimiter).ToList();
        ValidateColumn(columns.Count, column); columns.RemoveAt(column); return string.Join(delimiter, columns);
    }
    public static string ReorderColumns(string line, string delimiter, IReadOnlyList<int> order)
    {
        var columns = Parse(line, delimiter); return string.Join(delimiter, order.Select(index => GetColumn(columns, index)));
    }
    public static string JoinColumns(string line, string delimiter, IReadOnlyList<int> columns, string joinWith)
    {
        var parsed = Parse(line, delimiter); return string.Join(joinWith, columns.Select(index => GetColumn(parsed, index)));
    }
    public static bool ColumnMatches(string line, string delimiter, int column, CompiledFilter filter) => filter.IsMatch(ExtractColumn(line, delimiter, column));
    private static string GetColumn(IReadOnlyList<string> columns, int column) { ValidateColumn(columns.Count, column); return columns[column]; }
    private static void ValidateColumn(int count, int column) { if (column < 0 || column >= count) throw new ArgumentOutOfRangeException(nameof(column)); }
}
