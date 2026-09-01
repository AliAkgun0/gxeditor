using System.Text;
using GalaXako.Editor.Core.IO;
using GalaXako.Editor.Core.Operations;
using GalaXako.Editor.Core.Pipeline;

namespace GalaXako.Editor.Tests;

public sealed class OperationTests
{
    [Fact]
    public async Task Clean_trims_removes_empty_and_normalizes_spaces()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", "  alpha   beta  \n   \n gamma ");
        var output = temp.PathFor("output.txt");

        var result = await new CleanOperation().RunAsync(input, output,
            new CleanOptions(NormalizeWhitespace: true, LineEnding: "\n"), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("alpha beta\ngamma\n", File.ReadAllText(output));
        Assert.Equal(3, result.InputLines);
        Assert.Equal(2, result.OutputLines);
    }

    [Fact]
    public async Task Filter_compiles_and_combines_rules_once()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", "apple\napricot\npear\nAPPLE PIE\n");
        var output = temp.PathFor("filtered.txt");
        var rules = new[] { new FilterRule(FilterCondition.StartsWith, "ap"), new FilterRule(FilterCondition.LengthGreaterThan, "5") };

        await new FilterOperation().RunAsync(input, output, rules, FilterLogic.And, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["apricot", "APPLE PIE"], File.ReadAllLines(output));
    }

    [Fact]
    public async Task Dedupe_preserves_first_occurrence_and_supports_case_insensitive_mode()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", "Alpha\nbeta\nalpha\nbeta\ngamma\n");
        var output = temp.PathFor("unique.txt");

        var result = await new DedupeOperation().RunAsync(input, output, new DedupeOptions(CaseInsensitive: true), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["Alpha", "beta", "gamma"], File.ReadAllLines(output));
        Assert.Equal(2, result.AffectedLines);
    }

    [Fact]
    public async Task Disk_backed_dedupe_keeps_last_occurrences_in_original_order()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", "a\nb\na\nc\nb\n");
        var output = temp.PathFor("unique.txt");

        await new DedupeOperation().RunAsync(input, output,
            new DedupeOptions(KeepLastOccurrence: true, DiskBackedThresholdBytes: 0, PartitionCount: 16), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["a", "c", "b"], File.ReadAllLines(output));
    }

    [Fact]
    public async Task Split_and_merge_stream_files()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("source.txt", "1\n2\n3\n4\n5\n");
        var parts = await new SplitOperation().RunAsync(input, temp.PathFor("parts"), new SplitOptions(SplitMode.LineCount, 2), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(3, parts.OutputPaths.Count);
        Assert.Equal(["1", "2"], File.ReadAllLines(parts.OutputPaths[0]));

        var merged = temp.PathFor("merged.txt");
        await new MergeOperation().RunAsync(parts.OutputPaths, merged, new MergeOptions(InsertNewlineBetweenFiles: false), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["1", "2", "3", "4", "5"], File.ReadAllLines(merged));
    }

    [Fact]
    public async Task Split_line_count_creates_exact_parts_without_temporary_files()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("03_columns.csv", "id,name,city,score\n1,Ali,Adiyaman,90\n2,Ayse,Istanbul,85\n3,Mehmet,Ankara,77\n4,Ali,Izmir,92\n");
        var directory = temp.PathFor("03_columns_parts");

        var result = await new SplitOperation().RunAsync(input, directory,
            new SplitOptions(SplitMode.LineCount, 2), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, result.OutputPaths.Count);
        Assert.Equal(["id,name,city,score", "1,Ali,Adiyaman,90"], File.ReadAllLines(result.OutputPaths[0]));
        Assert.Equal(["2,Ayse,Istanbul,85", "3,Mehmet,Ankara,77"], File.ReadAllLines(result.OutputPaths[1]));
        Assert.Equal(["4,Ali,Izmir,92"], File.ReadAllLines(result.OutputPaths[2]));
        Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Split_output_directory_is_unique_when_a_previous_run_exists()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("03_columns.csv", "header\nvalue\n");
        Directory.CreateDirectory(temp.PathFor("03_columns_parts"));
        Directory.CreateDirectory(temp.PathFor("03_columns_parts_2"));

        var directory = new TextFileService().CreateOutputDirectory(input, "_parts");

        Assert.Equal(temp.PathFor("03_columns_parts_3"), directory);
    }

    [Fact]
    public async Task Extractor_finds_local_formats_without_network_calls()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.log", "mail a@example.com and b@example.com\na@example.com\n");
        var output = temp.PathFor("emails.txt");

        await new ExtractOperation().RunAsync(input, output, new ExtractOptions(ExtractorKind.Email, UniqueOnly: true), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["a@example.com", "b@example.com"], File.ReadAllLines(output));
    }

    [Fact]
    public async Task IPv6_extractor_supports_compressed_addresses_without_affecting_IPv4()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("02_extract.txt",
            "2001:db8::1\n::1\nfe80::1\n2001:db8:85a3::8a2e:370:7334\n192.168.1.10\n");
        var ipv6Output = temp.PathFor("ipv6.txt");
        var ipv4Output = temp.PathFor("ipv4.txt");

        await new ExtractOperation().RunAsync(input, ipv6Output,
            new ExtractOptions(ExtractorKind.IPv6), cancellationToken: TestContext.Current.CancellationToken);
        await new ExtractOperation().RunAsync(input, ipv4Output,
            new ExtractOptions(ExtractorKind.IPv4), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["2001:db8::1", "::1", "fe80::1", "2001:db8:85a3::8a2e:370:7334"], File.ReadAllLines(ipv6Output));
        Assert.Equal(["192.168.1.10"], File.ReadAllLines(ipv4Output));
    }

    [Fact]
    public void Extract_preview_uses_unique_and_sort_semantics_of_the_real_operation()
    {
        const string text = "https://example.com/path?q=1\nhttps://other.example/path\nhttps://example.com/path?q=1";

        var preview = ExtractOperation.ExtractSample(text,
            new ExtractOptions(ExtractorKind.Url, UniqueOnly: true, SortResults: true));

        Assert.Equal(["https://example.com/path?q=1", "https://other.example/path"], preview);
    }

    [Fact]
    public void Delimiter_tools_extract_remove_reorder_and_join_columns()
    {
        const string line = "one:two:three";
        Assert.Equal("two", DelimiterTools.ExtractColumn(line, ":", 1));
        Assert.Equal("one:three", DelimiterTools.RemoveColumn(line, ":", 1));
        Assert.Equal("three:one", DelimiterTools.ReorderColumns(line, ":", [2, 0]));
        Assert.Equal("one|three", DelimiterTools.JoinColumns(line, ":", [0, 2], "|"));
    }

    [Fact]
    public void Delimiter_and_split_previews_share_engine_semantics()
    {
        var rows = new[] { "id,name", "1,Ali", "2,Ayse", "3,Mehmet", "4,Veli" };

        var columnPreview = DelimiterOperation.TransformSample(rows,
            new DelimiterOptions(",", DelimiterOperationKind.ExtractColumn, Column: 1));
        var splitPreview = SplitOperation.SplitSample(rows,
            new SplitOptions(SplitMode.LineCount, 2), new UTF8Encoding(false));

        Assert.Equal(["name", "Ali", "Ayse", "Mehmet", "Veli"], columnPreview);
        Assert.Collection(splitPreview,
            part => Assert.Equal(["id,name", "1,Ali"], part),
            part => Assert.Equal(["2,Ayse", "3,Mehmet"], part),
            part => Assert.Equal(["4,Veli"], part));
    }

    [Fact]
    public async Task Encoding_detector_handles_utf8_bom_and_utf16()
    {
        using var temp = new TestWorkspace();
        var utf8 = temp.Write("utf8.txt", "Türkçe", new UTF8Encoding(true));
        var utf16 = temp.Write("utf16.txt", "Türkçe", Encoding.Unicode);

        var utf8Info = await EncodingDetector.DetectAsync(utf8, TestContext.Current.CancellationToken);
        var utf16Info = await EncodingDetector.DetectAsync(utf16, TestContext.Current.CancellationToken);

        Assert.True(utf8Info.HasBom);
        Assert.Equal("UTF-8 BOM", utf8Info.DisplayName);
        Assert.Equal("UTF-16 LE", utf16Info.DisplayName);
    }

    [Fact]
    public async Task Pipeline_executes_steps_in_order()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", "  apple \npear\napple\n  \n");
        var output = temp.PathFor("pipeline.txt");
        var pipeline = new PipelineDefinition
        {
            Name = "test",
            Steps =
            [
                new PipelineStep { Type = PipelineStepType.Clean, Clean = new CleanOptions(LineEnding: "\n") },
                new PipelineStep { Type = PipelineStepType.Filter, FilterRules = [new FilterRule(FilterCondition.Contains, "apple")] },
                new PipelineStep { Type = PipelineStepType.Dedupe, Dedupe = new DedupeOptions() }
            ]
        };

        await new PipelineRunner().RunAsync(input, output, pipeline, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["apple"], File.ReadAllLines(output));
    }

    [Fact]
    public async Task Cancellation_removes_partial_output()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", string.Join('\n', Enumerable.Range(0, 10_000)));
        var output = temp.PathFor("cancelled.txt");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new CleanOperation().RunAsync(input, output, new CleanOptions(), cancellationToken: cancellation.Token));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Operations_refuse_to_overwrite_existing_output()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("input.txt", "safe\n");
        var output = temp.Write("output.txt", "keep me");

        await Assert.ThrowsAsync<IOException>(() => new CleanOperation().RunAsync(input, output, new CleanOptions(), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("keep me", File.ReadAllText(output));
    }

    [Fact]
    public async Task External_sort_and_compare_produce_correct_sets()
    {
        using var temp = new TestWorkspace();
        var a = temp.Write("a.txt", "z\na\n10\n2\n");
        var sorted = temp.PathFor("sorted.txt");
        await new SortOperation().RunAsync(a, sorted, new SortOptions(SortMode.Natural, ChunkMemoryBytes: 64), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["2", "10", "a", "z"], File.ReadAllLines(sorted));

        var b = temp.Write("b.txt", "a\nb\n2\n");
        var different = temp.PathFor("different.txt");
        var result = await new CompareOperation().RunAsync(a, b, different, CompareMode.Different, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, result.OnlyInA);
        Assert.Equal(1, result.OnlyInB);
        Assert.Equal(2, result.InBoth);
        Assert.Equal(["10", "b", "z"], File.ReadAllLines(different));
    }

    [Fact]
    public async Task Delimiter_operation_streams_selected_column_and_counts_malformed_rows()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("columns.csv", "a,b,c\n1,2,3\nmalformed\n");
        var output = temp.PathFor("column.txt");

        var result = await new DelimiterOperation().RunAsync(input, output,
            new DelimiterOptions(",", DelimiterOperationKind.ExtractColumn, Column: 1),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["b", "2"], File.ReadAllLines(output));
        Assert.Equal(1, result.AffectedLines);
    }

    [Fact]
    public async Task Safe_editor_save_replaces_existing_file_without_partial_content()
    {
        using var temp = new TestWorkspace();
        var destination = temp.Write("document.txt", "old");
        var encoding = new GalaXako.Editor.Core.Models.TextEncodingInfo(new UTF8Encoding(false, true), false, 0, "UTF-8");

        await new TextFileService().SaveSafeAsync(destination, "new content", encoding, TestContext.Current.CancellationToken);

        Assert.Equal("new content", File.ReadAllText(destination));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(destination)!, "*.tmp"));
    }
}

internal sealed class TestWorkspace : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GalaXakoEditor.Tests", Guid.NewGuid().ToString("N"));
    public TestWorkspace() => Directory.CreateDirectory(_root);
    public string PathFor(string relative) => Path.Combine(_root, relative);
    public string Write(string relative, string content, Encoding? encoding = null)
    {
        var path = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, encoding ?? new UTF8Encoding(false));
        return path;
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
