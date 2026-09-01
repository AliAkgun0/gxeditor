using System.Globalization;
using System.Text.RegularExpressions;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Models;

namespace GalaXako.Editor.Core.Operations;

public enum FilterCondition { Contains, DoesNotContain, StartsWith, EndsWith, Equals, DoesNotEqual, RegexMatches, RegexDoesNotMatch, LengthGreaterThan, LengthLessThan, LengthBetween }
public enum FilterLogic { And, Or }
public sealed record FilterRule(FilterCondition Condition, string Value, string? SecondValue = null, bool CaseSensitive = false);

public sealed class CompiledFilter
{
    private readonly IReadOnlyList<Func<string, bool>> _predicates;
    private readonly FilterLogic _logic;

    public CompiledFilter(IEnumerable<FilterRule> rules, FilterLogic logic)
    {
        _logic = logic;
        _predicates = rules.Select(Compile).ToArray();
        if (_predicates.Count == 0) throw new ArgumentException("At least one filter rule is required.", nameof(rules));
    }

    public bool IsMatch(string line) => _logic == FilterLogic.And
        ? _predicates.All(predicate => predicate(line))
        : _predicates.Any(predicate => predicate(line));

    private static Func<string, bool> Compile(FilterRule rule)
    {
        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return rule.Condition switch
        {
            FilterCondition.Contains => line => line.Contains(rule.Value, comparison),
            FilterCondition.DoesNotContain => line => !line.Contains(rule.Value, comparison),
            FilterCondition.StartsWith => line => line.StartsWith(rule.Value, comparison),
            FilterCondition.EndsWith => line => line.EndsWith(rule.Value, comparison),
            FilterCondition.Equals => line => line.Equals(rule.Value, comparison),
            FilterCondition.DoesNotEqual => line => !line.Equals(rule.Value, comparison),
            FilterCondition.RegexMatches => CompileRegex(rule, negate: false),
            FilterCondition.RegexDoesNotMatch => CompileRegex(rule, negate: true),
            FilterCondition.LengthGreaterThan => line => line.Length > ParseInteger(rule.Value),
            FilterCondition.LengthLessThan => line => line.Length < ParseInteger(rule.Value),
            FilterCondition.LengthBetween => CompileBetween(rule),
            _ => throw new ArgumentOutOfRangeException(nameof(rule))
        };
    }

    private static Func<string, bool> CompileRegex(FilterRule rule, bool negate)
    {
        var options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        if (!rule.CaseSensitive) options |= RegexOptions.IgnoreCase;
        var regex = new Regex(rule.Value, options, TimeSpan.FromSeconds(2));
        return line => negate != regex.IsMatch(line);
    }

    private static Func<string, bool> CompileBetween(FilterRule rule)
    {
        var min = ParseInteger(rule.Value);
        var max = ParseInteger(rule.SecondValue ?? throw new ArgumentException("A second length is required."));
        if (max < min) (min, max) = (max, min);
        return line => line.Length >= min && line.Length <= max;
    }

    private static int ParseInteger(string value) => int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
}

public sealed class FilterOperation
{
    public async Task<OperationResult> RunAsync(string inputPath, string outputPath, IEnumerable<FilterRule> rules, FilterLogic logic,
        IProgress<OperationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var filter = new CompiledFilter(rules, logic);
        var detected = await EncodingDetector.DetectAsync(inputPath, cancellationToken);
        var info = new FileInfo(inputPath);
        var tracker = new ThrottledProgress(progress, info.Length, "Filtreleniyor");
        long inputLines = 0, outputLines = 0;
        var (stream, reader) = OperationIO.OpenReader(inputPath, detected.Encoding, 1024 * 1024);
        await using var ownedStream = stream;
        using var ownedReader = reader;
        await using var output = TransactionalTextOutput.Create(outputPath, detected.Encoding);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            inputLines++;
            if (filter.IsMatch(line))
            {
                await output.Writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                outputLines++;
            }
            tracker.Report(stream.Position, inputLines, inputLines - outputLines);
        }
        await output.CommitAsync(cancellationToken);
        tracker.Report(info.Length, inputLines, inputLines - outputLines, true);
        return new OperationResult(outputPath, inputLines, outputLines, inputLines - outputLines, tracker.Elapsed);
    }
}
