using System.Text;
using GalaXako.Editor.Core.IO;

namespace GalaXako.Editor.Tests;

public sealed class LargeFileTests
{
    [Fact]
    public async Task Sparse_index_navigates_from_nearest_checkpoint()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("large.txt", string.Join('\n', Enumerable.Range(1, 25_000).Select(index => $"line-{index}")));
        var encoding = await EncodingDetector.DetectAsync(input, TestContext.Current.CancellationToken);
        var index = new SparseLineIndex(input, encoding, 1_000);
        await index.BuildAsync(cancellationToken: TestContext.Current.CancellationToken);

        var nearest = index.FindNearestLine(20_500);

        Assert.InRange(nearest.LineNumber, 19_001, 20_500);
        Assert.True(nearest.ByteOffset > 0);
        Assert.Equal(25_000, index.LineCount);
    }

    [Fact]
    public async Task Large_document_reads_only_requested_chunk()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("large.txt", string.Join('\n', Enumerable.Range(1, 5_000).Select(index => $"line-{index}")), Encoding.Unicode);
        var inspection = await new FileInspectionService().InspectAsync(input, 1, TestContext.Current.CancellationToken);
        var document = new LargeFileDocument(inspection, 500);
        await document.BuildIndexAsync(cancellationToken: TestContext.Current.CancellationToken);

        var chunk = await document.ReadChunkByLineAsync(4_250, 25, TestContext.Current.CancellationToken);

        Assert.Equal("line-4250", chunk.Lines[0]);
        Assert.Equal("line-4274", chunk.Lines[^1]);
        Assert.Equal(25, chunk.Lines.Count);
    }

    [Fact]
    public async Task Single_huge_line_preview_is_memory_bounded()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("single-line.txt", new string('x', 2_000_000));
        var inspection = await new FileInspectionService().InspectAsync(input, 1, TestContext.Current.CancellationToken);
        var document = new LargeFileDocument(inspection, 500);
        await document.BuildIndexAsync(cancellationToken: TestContext.Current.CancellationToken);

        var chunk = await document.ReadChunkByLineAsync(1, 10, TestContext.Current.CancellationToken);

        Assert.Single(chunk.Lines);
        Assert.True(chunk.Lines[0].Length < 150_000);
        Assert.Contains("kısaltıldı", chunk.Lines[0]);
    }

    [Fact]
    public async Task Streaming_search_reports_incremental_results_and_statistics_are_exact()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("search.txt", "alpha\n\nbeta alpha\ngamma\n");
        var file = await new FileInspectionService().InspectAsync(input, 1, TestContext.Current.CancellationToken);
        var found = new List<LargeFileSearchResult>();
        var progress = new InlineProgress<LargeFileSearchResult>(found.Add);

        var summary = await new LargeFileSearchService().SearchAsync(file, new SearchOptions("alpha"), progress,
            cancellationToken: TestContext.Current.CancellationToken);
        var stats = await new FileStatisticsService().AnalyzeAsync(file, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.MatchCount);
        Assert.Equal([1L, 3L], found.Select(result => result.LineNumber));
        Assert.Equal(4, stats.LineCount);
        Assert.Equal(1, stats.EmptyLines);
        Assert.Equal(10, stats.LongestLine);
    }
}

internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
