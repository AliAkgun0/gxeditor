using GalaXako.Editor.Core.Models;
using GalaXako.Editor.Core.Operations;
using GalaXako.Editor.Core.Pipeline;
using GalaXako.Editor.Infrastructure.Storage;

namespace GalaXako.Editor.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Settings_history_and_pipeline_round_trip_as_json()
    {
        using var temp = new TestWorkspace();
        var settingsStore = new JsonSettingsStore(temp.PathFor("state"));
        var historyStore = new JsonHistoryStore(temp.PathFor("state"));
        var pipelineStore = new JsonPipelineStore(temp.PathFor("state"));
        var settings = new AppSettings { NormalFileThresholdBytes = 64L * 1024 * 1024, MaxConcurrentJobs = 4 };
        var history = new AppHistory { LastOperation = "Clean", RecentFiles = [new RecentFile("C:\\data.txt", 42, DateTime.UnixEpoch, "TXT")] };
        var pipelines = new[] { new PipelineDefinition { Name = "Basic", Steps = [new PipelineStep { Type = PipelineStepType.Clean, Clean = new CleanOptions() }] } };

        await settingsStore.SaveAsync(settings, TestContext.Current.CancellationToken);
        await historyStore.SaveAsync(history, TestContext.Current.CancellationToken);
        await pipelineStore.SaveAsync(pipelines, TestContext.Current.CancellationToken);

        Assert.Equal(64L * 1024 * 1024, (await settingsStore.LoadAsync(TestContext.Current.CancellationToken)).NormalFileThresholdBytes);
        Assert.Equal("Clean", (await historyStore.LoadAsync(TestContext.Current.CancellationToken)).LastOperation);
        Assert.Equal("Basic", Assert.Single(await pipelineStore.LoadAsync(TestContext.Current.CancellationToken)).Name);
    }

    [Fact]
    public async Task Empty_file_analysis_is_well_defined()
    {
        using var temp = new TestWorkspace();
        var input = temp.Write("empty.txt", string.Empty);
        var file = await new GalaXako.Editor.Core.IO.FileInspectionService().InspectAsync(input, 1, TestContext.Current.CancellationToken);

        var statistics = await new GalaXako.Editor.Core.IO.FileStatisticsService().AnalyzeAsync(file, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, statistics.LineCount);
        Assert.Equal(0, statistics.ShortestLine);
        Assert.Equal(0, statistics.LongestLine);
    }
}
