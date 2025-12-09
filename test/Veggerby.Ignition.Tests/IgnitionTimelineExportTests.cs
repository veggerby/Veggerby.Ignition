using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Veggerby.Ignition.Diagnostics;

namespace Veggerby.Ignition.Tests;

public class IgnitionTimelineExportTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        return new IgnitionCoordinator(signals, optionsWrapper, logger);
    }

    [Fact]
    public async Task ExportTimeline_ContainsSignalEvents()
    {
        // arrange
        var s1 = new FakeSignal("db-init", _ => Task.CompletedTask);
        var s2 = new FakeSignal("cache-warmup", _ => Task.Delay(20));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline(executionMode: "Parallel", globalTimeout: TimeSpan.FromSeconds(5));

        // assert
        timeline.Events.Should().HaveCount(2);
        timeline.Events.Should().Contain(e => e.SignalName == "db-init");
        timeline.Events.Should().Contain(e => e.SignalName == "cache-warmup");
        timeline.ExecutionMode.Should().Be("Parallel");
        timeline.GlobalTimeoutMs.Should().Be(5000);
    }

    [Fact]
    public async Task ExportTimeline_HasTimelineData_WithStartAndEndTimes()
    {
        // arrange
        var s1 = new FakeSignal("signal1", _ => Task.Delay(30));
        var coord = CreateCoordinator(new[] { s1 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.HasTimelineData.Should().BeTrue();
        result.Results.Should().AllSatisfy(r =>
        {
            r.StartedAt.Should().NotBeNull();
            r.CompletedAt.Should().NotBeNull();
            r.CompletedAt!.Value.Should().BeGreaterThanOrEqualTo(r.StartedAt!.Value);
        });
    }

    [Fact]
    public async Task ExportTimeline_EventStartEndTimes_AreRelativeToIgnitionStart()
    {
        // arrange
        var s1 = new FakeSignal("fast", _ => Task.CompletedTask);
        var s2 = new FakeSignal("slow", _ => Task.Delay(50));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        var fastEvent = timeline.Events.Single(e => e.SignalName == "fast");
        var slowEvent = timeline.Events.Single(e => e.SignalName == "slow");

        fastEvent.StartMs.Should().BeGreaterThanOrEqualTo(0);
        fastEvent.EndMs.Should().BeGreaterThan(fastEvent.StartMs);
        slowEvent.EndMs.Should().BeGreaterThan(fastEvent.EndMs); // slow finishes after fast
    }

    [Fact]
    public async Task ExportTimeline_SequentialExecution_ShowsSequentialTiming()
    {
        // arrange
        var s1 = new FakeSignal("first", _ => Task.Delay(20));
        var s2 = new FakeSignal("second", _ => Task.Delay(20));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline(executionMode: "Sequential");

        // assert
        var firstEvent = timeline.Events.Single(e => e.SignalName == "first");
        var secondEvent = timeline.Events.Single(e => e.SignalName == "second");

        // In sequential mode, second should start after first ends
        secondEvent.StartMs.Should().BeGreaterThanOrEqualTo(firstEvent.EndMs);
    }

    [Fact]
    public async Task ExportTimeline_Summary_ContainsCorrectStatistics()
    {
        // arrange
        var success = new FakeSignal("success", _ => Task.CompletedTask);
        var failed = new FaultingSignal("failed", new InvalidOperationException("boom"));
        var timedOut = new FakeSignal("timeout", async ct => await Task.Delay(100, ct), timeout: TimeSpan.FromMilliseconds(20));
        var coord = CreateCoordinator(new IIgnitionSignal[] { success, failed, timedOut }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.CancelIndividualOnTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        timeline.Summary.Should().NotBeNull();
        timeline.Summary!.TotalSignals.Should().Be(3);
        timeline.Summary.SucceededCount.Should().Be(1);
        timeline.Summary.FailedCount.Should().Be(1);
        timeline.Summary.TimedOutCount.Should().Be(1);
    }

    [Fact]
    public async Task ExportTimeline_Summary_IdentifiesSlowestAndFastestSignals()
    {
        // arrange
        var fast = new FakeSignal("fast", _ => Task.CompletedTask);
        var slow = new FakeSignal("slow", _ => Task.Delay(50));
        var coord = CreateCoordinator(new[] { fast, slow }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        timeline.Summary!.SlowestSignal.Should().Be("slow");
        timeline.Summary.SlowestDurationMs.Should().BeGreaterThan(0);
        timeline.Summary.FastestSignal.Should().Be("fast");
        timeline.Summary.FastestDurationMs.Should().NotBeNull();
    }

    [Fact]
    public async Task ExportTimeline_ConcurrentGroups_IdentifiesParallelExecution()
    {
        // arrange - signals that should execute concurrently
        var s1 = new FakeSignal("signal1", _ => Task.Delay(30));
        var s2 = new FakeSignal("signal2", _ => Task.Delay(30));
        var s3 = new FakeSignal("signal3", _ => Task.Delay(30));
        var coord = CreateCoordinator(new[] { s1, s2, s3 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        timeline.Summary!.MaxConcurrency.Should().BeGreaterThan(1);
        // All signals should have concurrent group assigned
        timeline.Events.Should().AllSatisfy(e => e.ConcurrentGroup.Should().NotBeNull());
    }

    [Fact]
    public async Task ExportTimeline_GlobalTimeout_AddsBoundaryMarker()
    {
        // arrange
        var slow = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var coord = CreateCoordinator(new[] { slow }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline(globalTimeout: TimeSpan.FromMilliseconds(50));

        // assert
        timeline.TimedOut.Should().BeTrue();
        timeline.Boundaries.Should().Contain(b => b.Type == "GlobalTimeoutConfigured");
        timeline.Boundaries.Single(b => b.Type == "GlobalTimeoutConfigured").TimeMs.Should().Be(50);
    }

    [Fact]
    public async Task ExportTimeline_ToJson_ProducesValidJson()
    {
        // arrange
        var s1 = new FakeSignal("signal1", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { s1 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var json = result.ExportTimelineJson(indented: true, executionMode: "Parallel", globalTimeout: TimeSpan.FromSeconds(1));

        // assert
        json.Should().NotBeNullOrEmpty();
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("events").GetArrayLength().Should().Be(1);
        parsed.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
    }

    [Fact]
    public async Task ExportTimeline_FromJson_RoundTripsCorrectly()
    {
        // arrange
        var s1 = new FakeSignal("db-init", _ => Task.CompletedTask);
        var s2 = new FakeSignal("cache-warmup", _ => Task.Delay(10));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var original = result.ExportTimeline(executionMode: "Parallel");
        var json = original.ToJson();
        var restored = IgnitionTimeline.FromJson(json);

        // assert
        restored.Should().NotBeNull();
        restored!.Events.Should().HaveCount(original.Events.Count);
        restored.TotalDurationMs.Should().Be(original.TotalDurationMs);
        restored.SchemaVersion.Should().Be(original.SchemaVersion);
    }

    [Fact]
    public async Task ExportTimeline_WithTimestamps_IncludesIso8601Strings()
    {
        // arrange
        var s1 = new FakeSignal("signal1", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { s1 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });
        var startTime = DateTimeOffset.UtcNow;

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var endTime = DateTimeOffset.UtcNow;
        var timeline = result.ExportTimeline(startedAt: startTime, completedAt: endTime);

        // assert
        timeline.StartedAt.Should().NotBeNull();
        timeline.CompletedAt.Should().NotBeNull();
        // ISO 8601 format contains a time zone indicator
        timeline.StartedAt.Should().MatchRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}");
    }

    [Fact]
    public async Task ExportTimeline_ZeroSignals_ReturnsEmptyTimeline()
    {
        // arrange
        var coord = CreateCoordinator([], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        timeline.Events.Should().BeEmpty();
        timeline.Summary!.TotalSignals.Should().Be(0);
        timeline.Summary.MaxConcurrency.Should().Be(0);
    }

    [Fact]
    public async Task ExportTimeline_FailedSignals_IncludesStatusInEvents()
    {
        // arrange
        var failed = new FaultingSignal("failed-signal", new InvalidOperationException("boom"));
        var coord = CreateCoordinator(new[] { failed }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Policy = IgnitionPolicy.BestEffort;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        var failedEvent = timeline.Events.Single(e => e.SignalName == "failed-signal");
        failedEvent.Status.Should().Be("Failed");
    }

    [Fact]
    public void ExportTimeline_SkippedSignals_IncludesFailedDependencies()
    {
        // This test requires dependency-aware mode to produce skipped signals
        // For now, we test that the property is properly handled

        // arrange - create a result with a skipped signal manually
        var result = new IgnitionResult(
            TimeSpan.FromMilliseconds(100),
            new[]
            {
                new IgnitionSignalResult("parent", IgnitionSignalStatus.Failed, TimeSpan.FromMilliseconds(50),
                    new InvalidOperationException("failed"),
                    StartedAt: TimeSpan.Zero,
                    CompletedAt: TimeSpan.FromMilliseconds(50)),
                new IgnitionSignalResult("child", IgnitionSignalStatus.Skipped, TimeSpan.Zero,
                    FailedDependencies: new[] { "parent" },
                    StartedAt: TimeSpan.FromMilliseconds(50),
                    CompletedAt: TimeSpan.FromMilliseconds(50))
            },
            TimedOut: false);

        // act
        var timeline = result.ExportTimeline();

        // assert
        var childEvent = timeline.Events.Single(e => e.SignalName == "child");
        childEvent.Status.Should().Be("Skipped");
        childEvent.FailedDependencies.Should().Contain("parent");
    }

    [Fact]
    public void IgnitionSignalResult_HasTimelineData_WhenBothTimestampsPresent()
    {
        // arrange
        var resultWithTimeline = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.FromMilliseconds(100),
            StartedAt: TimeSpan.Zero,
            CompletedAt: TimeSpan.FromMilliseconds(100));

        var resultWithoutTimeline = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.FromMilliseconds(100));

        // assert
        resultWithTimeline.HasTimelineData.Should().BeTrue();
        resultWithoutTimeline.HasTimelineData.Should().BeFalse();
    }

    [Fact]
    public void IgnitionResult_HasTimelineData_WhenAllResultsHaveTimestamps()
    {
        // arrange
        var resultWithTimeline = new IgnitionResult(
            TimeSpan.FromMilliseconds(200),
            new[]
            {
                new IgnitionSignalResult("s1", IgnitionSignalStatus.Succeeded, TimeSpan.FromMilliseconds(100),
                    StartedAt: TimeSpan.Zero, CompletedAt: TimeSpan.FromMilliseconds(100)),
                new IgnitionSignalResult("s2", IgnitionSignalStatus.Succeeded, TimeSpan.FromMilliseconds(100),
                    StartedAt: TimeSpan.FromMilliseconds(100), CompletedAt: TimeSpan.FromMilliseconds(200))
            },
            TimedOut: false);

        var mixedResult = new IgnitionResult(
            TimeSpan.FromMilliseconds(200),
            new[]
            {
                new IgnitionSignalResult("s1", IgnitionSignalStatus.Succeeded, TimeSpan.FromMilliseconds(100),
                    StartedAt: TimeSpan.Zero, CompletedAt: TimeSpan.FromMilliseconds(100)),
                new IgnitionSignalResult("s2", IgnitionSignalStatus.Succeeded, TimeSpan.FromMilliseconds(100)) // No timeline data
            },
            TimedOut: false);

        // assert
        resultWithTimeline.HasTimelineData.Should().BeTrue();
        mixedResult.HasTimelineData.Should().BeFalse();
    }

    [Fact]
    public void ToConsoleString_ProducesValidOutput()
    {
        // arrange
        var result = new IgnitionResult(
            TimeSpan.FromMilliseconds(1000),
            new[]
            {
                new IgnitionSignalResult("fast-signal", IgnitionSignalStatus.Succeeded, TimeSpan.FromMilliseconds(200),
                    StartedAt: TimeSpan.Zero, CompletedAt: TimeSpan.FromMilliseconds(200)),
                new IgnitionSignalResult("slow-signal", IgnitionSignalStatus.Succeeded, TimeSpan.FromMilliseconds(800),
                    StartedAt: TimeSpan.Zero, CompletedAt: TimeSpan.FromMilliseconds(800)),
                new IgnitionSignalResult("timeout-signal", IgnitionSignalStatus.TimedOut, TimeSpan.FromMilliseconds(500),
                    StartedAt: TimeSpan.Zero, CompletedAt: TimeSpan.FromMilliseconds(500))
            },
            TimedOut: false);

        var timeline = result.ExportTimeline(executionMode: "Parallel", globalTimeout: TimeSpan.FromSeconds(10));

        // act
        var consoleOutput = timeline.ToConsoleString();

        // assert
        consoleOutput.Should().NotBeNullOrEmpty();
        consoleOutput.Should().Contain("IGNITION TIMELINE");
        consoleOutput.Should().Contain("fast-signal");
        consoleOutput.Should().Contain("slow-signal");
        consoleOutput.Should().Contain("timeout-signal");
        consoleOutput.Should().Contain("SUMMARY");
        consoleOutput.Should().Contain("Parallel");
        consoleOutput.Should().Contain("✅");
        consoleOutput.Should().Contain("⏰");
    }

    [Fact]
    public void ToConsoleString_HandlesEmptyTimeline()
    {
        // arrange
        var result = new IgnitionResult(
            TimeSpan.FromMilliseconds(0),
            Array.Empty<IgnitionSignalResult>(),
            TimedOut: false);

        var timeline = result.ExportTimeline();

        // act
        var consoleOutput = timeline.ToConsoleString();

        // assert
        consoleOutput.Should().NotBeNullOrEmpty();
        consoleOutput.Should().Contain("IGNITION TIMELINE");
        consoleOutput.Should().Contain("Total Signals:");
    }
}
