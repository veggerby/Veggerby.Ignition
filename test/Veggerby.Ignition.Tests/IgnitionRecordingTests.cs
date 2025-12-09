using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Veggerby.Ignition.Diagnostics;

namespace Veggerby.Ignition.Tests;

public class IgnitionRecordingTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        return new IgnitionCoordinator(signals, optionsWrapper, logger);
    }

    private static IgnitionOptions CreateOptions(Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        return opts;
    }

    [Fact]
    public async Task ExportRecording_ContainsSignalData()
    {
        // arrange
        var s1 = new FakeSignal("db-init", _ => Task.CompletedTask);
        var s2 = new FakeSignal("cache-warmup", _ => Task.Delay(20));
        var options = CreateOptions(o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);

        // assert
        recording.Signals.Should().HaveCount(2);
        recording.Signals.Should().Contain(s => s.SignalName == "db-init");
        recording.Signals.Should().Contain(s => s.SignalName == "cache-warmup");
        recording.SchemaVersion.Should().Be("1.0");
        recording.RecordingId.Should().NotBeNullOrEmpty();
        recording.RecordedAt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportRecording_CapturesConfiguration()
    {
        // arrange
        var s1 = new FakeSignal("signal1", _ => Task.CompletedTask);
        var options = CreateOptions(o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Policy = IgnitionPolicy.BestEffort;
            o.CancelOnGlobalTimeout = true;
        });
        var coord = CreateCoordinator(new[] { s1 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Policy = IgnitionPolicy.BestEffort;
            o.CancelOnGlobalTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);

        // assert
        recording.Configuration.Should().NotBeNull();
        recording.Configuration!.ExecutionMode.Should().Be("Parallel");
        recording.Configuration.Policy.Should().Be("BestEffort");
        recording.Configuration.GlobalTimeoutMs.Should().Be(5000);
        recording.Configuration.CancelOnGlobalTimeout.Should().BeTrue();
    }

    [Fact]
    public async Task ExportRecording_CapturesTimingData()
    {
        // arrange
        var s1 = new FakeSignal("fast", _ => Task.CompletedTask);
        var s2 = new FakeSignal("slow", _ => Task.Delay(50));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording();

        // assert
        var fastSignal = recording.Signals.Single(s => s.SignalName == "fast");
        var slowSignal = recording.Signals.Single(s => s.SignalName == "slow");

        fastSignal.StartMs.Should().BeGreaterThanOrEqualTo(0);
        fastSignal.EndMs.Should().BeGreaterThan(fastSignal.StartMs);
        slowSignal.DurationMs.Should().BeGreaterThan(fastSignal.DurationMs);
    }

    [Fact]
    public async Task ExportRecording_CapturesSummary()
    {
        // arrange
        var success = new FakeSignal("success", _ => Task.CompletedTask);
        var failed = new FaultingSignal("failed", new InvalidOperationException("boom"));
        var options = CreateOptions(o =>
        {
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });
        var coord = CreateCoordinator(new IIgnitionSignal[] { success, failed }, o =>
        {
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);

        // assert
        recording.Summary.Should().NotBeNull();
        recording.Summary!.TotalSignals.Should().Be(2);
        recording.Summary.SucceededCount.Should().Be(1);
        recording.Summary.FailedCount.Should().Be(1);
    }

    [Fact]
    public async Task ExportRecording_ToJson_ProducesValidJson()
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
        var json = result.ExportRecordingJson();

        // assert
        json.Should().NotBeNullOrEmpty();
        var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("signals").GetArrayLength().Should().Be(1);
        parsed.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        parsed.RootElement.GetProperty("recordingId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExportRecording_FromJson_RoundTripsCorrectly()
    {
        // arrange
        var s1 = new FakeSignal("db-init", _ => Task.CompletedTask);
        var s2 = new FakeSignal("cache-warmup", _ => Task.Delay(10));
        var options = CreateOptions(o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var original = result.ExportRecording(options);
        var json = original.ToJson();
        var restored = IgnitionRecording.FromJson(json);

        // assert
        restored.Should().NotBeNull();
        restored!.Signals.Should().HaveCount(original.Signals.Count);
        restored.TotalDurationMs.Should().Be(original.TotalDurationMs);
        restored.SchemaVersion.Should().Be(original.SchemaVersion);
        restored.Configuration!.ExecutionMode.Should().Be(original.Configuration!.ExecutionMode);
    }

    [Fact]
    public async Task ExportRecording_FailedSignal_CapturesExceptionInfo()
    {
        // arrange
        var failed = new FaultingSignal("failed-signal", new InvalidOperationException("test error"));
        var coord = CreateCoordinator(new[] { failed }, o =>
        {
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording();

        // assert
        var failedSignal = recording.Signals.Single(s => s.SignalName == "failed-signal");
        failedSignal.Status.Should().Be("Failed");
        failedSignal.ExceptionType.Should().Contain("InvalidOperationException");
        failedSignal.ExceptionMessage.Should().Be("test error");
    }

    [Fact]
    public async Task ExportRecording_WithMetadata_IncludesMetadata()
    {
        // arrange
        var s1 = new FakeSignal("signal1", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { s1 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });
        var metadata = new Dictionary<string, string>
        {
            ["environment"] = "production",
            ["version"] = "1.2.3"
        };

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(metadata: metadata);

        // assert
        recording.Metadata.Should().NotBeNull();
        recording.Metadata!["environment"].Should().Be("production");
        recording.Metadata["version"].Should().Be("1.2.3");
    }

    [Fact]
    public async Task ExportRecording_ToTimeline_ProducesEquivalentTimeline()
    {
        // arrange
        var s1 = new FakeSignal("signal1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("signal2", _ => Task.Delay(20));
        var options = CreateOptions(o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);
        var timeline = recording.ToTimeline();

        // assert
        timeline.Events.Should().HaveCount(2);
        timeline.TotalDurationMs.Should().Be(recording.TotalDurationMs);
        timeline.ExecutionMode.Should().Be("Parallel");
    }

    [Fact]
    public void Recording_FromJson_ReturnsNullForInvalidJson()
    {
        // act
        var result = IgnitionRecording.FromJson("invalid json {{{");

        // assert
        result.Should().BeNull();
    }

    [Fact]
    public void Recording_FromJson_ReturnsNullForEmptyString()
    {
        // act
        var result = IgnitionRecording.FromJson("");

        // assert
        result.Should().BeNull();
    }

    [Fact]
    public void Recording_FromJson_ReturnsNullForWhitespace()
    {
        // act
        var result = IgnitionRecording.FromJson("   ");

        // assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ToReplayer_CreatesReplayerFromResult()
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
        var replayer = result.ToReplayer();

        // assert
        replayer.Should().NotBeNull();
        replayer.Recording.Signals.Should().HaveCount(1);
    }
}
