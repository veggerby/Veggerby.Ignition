using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Veggerby.Ignition.Diagnostics;

namespace Veggerby.Ignition.Tests;

/// <summary>
/// Tests validating determinism guarantees and serialization format stability.
/// </summary>
public class DeterminismAndStabilityTests
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
    public async Task DeterministicClassification_SameSignalsAndOptions_ProducesSameClassification()
    {
        // arrange
        var signals1 = new[]
        {
            new FakeSignal("signal1", _ => Task.Delay(10)),
            new FakeSignal("signal2", _ => Task.Delay(20)),
            new FakeSignal("signal3", _ => Task.Delay(5))
        };

        var signals2 = new[]
        {
            new FakeSignal("signal1", _ => Task.Delay(10)),
            new FakeSignal("signal2", _ => Task.Delay(20)),
            new FakeSignal("signal3", _ => Task.Delay(5))
        };

        var configure = (IgnitionOptions o) =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Policy = IgnitionPolicy.BestEffort;
        };

        var coord1 = CreateCoordinator(signals1, configure);
        var coord2 = CreateCoordinator(signals2, configure);

        // act
        await coord1.WaitAllAsync();
        await coord2.WaitAllAsync();

        var result1 = await coord1.GetResultAsync();
        var result2 = await coord2.GetResultAsync();

        // assert
        result1.TimedOut.Should().Be(result2.TimedOut);
        result1.Results.Count.Should().Be(result2.Results.Count);

        foreach (var r1 in result1.Results)
        {
            var r2 = result2.Results.First(x => x.Name == r1.Name);
            r1.Status.Should().Be(r2.Status);
        }
    }

    [Fact]
    public async Task RecordingSchemaVersion_IsStable()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var options = new IgnitionOptions
        {
            ExecutionMode = IgnitionExecutionMode.Parallel,
            GlobalTimeout = TimeSpan.FromSeconds(5)
        };
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = options.ExecutionMode;
            o.GlobalTimeout = options.GlobalTimeout;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);

        // assert
        recording.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public async Task RecordingSerializationFormat_IsStableAndBackwardCompatible()
    {
        // arrange
        var signal = new FakeSignal("test-signal", _ => Task.Delay(10));
        var options = new IgnitionOptions
        {
            ExecutionMode = IgnitionExecutionMode.Parallel,
            GlobalTimeout = TimeSpan.FromSeconds(5),
            Policy = IgnitionPolicy.BestEffort
        };
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = options.ExecutionMode;
            o.GlobalTimeout = options.GlobalTimeout;
            o.Policy = options.Policy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);
        var json = recording.ToJson();

        // assert - verify JSON structure contains expected fields
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        root.GetProperty("recordingId").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("recordedAt").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("totalDurationMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("timedOut").GetBoolean().Should().BeFalse();

        var config = root.GetProperty("configuration");
        config.GetProperty("executionMode").GetString().Should().Be("Parallel");
        config.GetProperty("policy").GetString().Should().Be("BestEffort");
        config.GetProperty("globalTimeoutMs").GetDouble().Should().Be(5000);

        var signals = root.GetProperty("signals");
        signals.GetArrayLength().Should().Be(1);

        var firstSignal = signals[0];
        firstSignal.GetProperty("signalName").GetString().Should().Be("test-signal");
        firstSignal.GetProperty("status").GetString().Should().Be("Succeeded");
        firstSignal.GetProperty("startMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        firstSignal.GetProperty("endMs").GetDouble().Should().BeGreaterThan(0);
        firstSignal.GetProperty("durationMs").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RecordingDeserialization_CanRoundTrip()
    {
        // arrange
        var signal = new FakeSignal("roundtrip-signal", _ => Task.Delay(15));
        var options = new IgnitionOptions
        {
            ExecutionMode = IgnitionExecutionMode.Sequential,
            GlobalTimeout = TimeSpan.FromSeconds(10),
            Policy = IgnitionPolicy.FailFast
        };
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = options.ExecutionMode;
            o.GlobalTimeout = options.GlobalTimeout;
            o.Policy = options.Policy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var originalRecording = result.ExportRecording(options);
        var json = originalRecording.ToJson();
        var deserializedRecording = IgnitionRecording.FromJson(json);

        // assert
        deserializedRecording.Should().NotBeNull();
        deserializedRecording!.SchemaVersion.Should().Be(originalRecording.SchemaVersion);
        deserializedRecording.RecordingId.Should().Be(originalRecording.RecordingId);
        deserializedRecording.TotalDurationMs.Should().Be(originalRecording.TotalDurationMs);
        deserializedRecording.TimedOut.Should().Be(originalRecording.TimedOut);
        deserializedRecording.Signals.Count.Should().Be(originalRecording.Signals.Count);
        deserializedRecording.Configuration!.ExecutionMode.Should().Be(originalRecording.Configuration!.ExecutionMode);
        deserializedRecording.Configuration.Policy.Should().Be(originalRecording.Configuration.Policy);
    }

    [Fact]
    public async Task TimelineSchemaVersion_IsStable()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { signal });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();

        // assert
        timeline.SchemaVersion.Should().Be("1.0");
    }

    [Fact]
    public async Task TimelineSerializationFormat_IsStableAndBackwardCompatible()
    {
        // arrange
        var signal = new FakeSignal("timeline-signal", _ => Task.Delay(10));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var timeline = result.ExportTimeline();
        var json = timeline.ToJson();

        // assert - verify JSON structure contains expected fields
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.GetProperty("schemaVersion").GetString().Should().Be("1.0");
        root.GetProperty("totalDurationMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        root.GetProperty("timedOut").GetBoolean().Should().BeFalse();

        // executionMode may be present or may be null
        if (root.TryGetProperty("executionMode", out var executionMode) && executionMode.ValueKind != JsonValueKind.Null)
        {
            executionMode.GetString().Should().Be("Parallel");
        }

        var events = root.GetProperty("events");
        events.GetArrayLength().Should().Be(1);

        var firstEvent = events[0];
        firstEvent.GetProperty("signalName").GetString().Should().Be("timeline-signal");
        firstEvent.GetProperty("status").GetString().Should().Be("Succeeded");
        firstEvent.GetProperty("startMs").GetDouble().Should().BeGreaterThanOrEqualTo(0);
        firstEvent.GetProperty("endMs").GetDouble().Should().BeGreaterThan(0);
        firstEvent.GetProperty("durationMs").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TimelineDeserialization_CanRoundTrip()
    {
        // arrange
        var signal = new FakeSignal("timeline-roundtrip", _ => Task.Delay(10));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var originalTimeline = result.ExportTimeline();
        var json = originalTimeline.ToJson();
        var deserializedTimeline = IgnitionTimeline.FromJson(json);

        // assert
        deserializedTimeline.Should().NotBeNull();
        deserializedTimeline!.SchemaVersion.Should().Be(originalTimeline.SchemaVersion);
        deserializedTimeline.TotalDurationMs.Should().Be(originalTimeline.TotalDurationMs);
        deserializedTimeline.TimedOut.Should().Be(originalTimeline.TimedOut);
        deserializedTimeline.ExecutionMode.Should().Be(originalTimeline.ExecutionMode);
        deserializedTimeline.Events.Count.Should().Be(originalTimeline.Events.Count);
    }

    [Fact]
    public async Task DeterministicClassification_FailedSignal_ConsistentStatus()
    {
        // arrange
        var signals1 = new IIgnitionSignal[]
        {
            new FakeSignal("success", _ => Task.CompletedTask),
            new FaultingSignal("failure", new InvalidOperationException("test error"))
        };

        var signals2 = new IIgnitionSignal[]
        {
            new FakeSignal("success", _ => Task.CompletedTask),
            new FaultingSignal("failure", new InvalidOperationException("test error"))
        };

        var configure = (IgnitionOptions o) =>
        {
            o.Policy = IgnitionPolicy.BestEffort;
        };

        var coord1 = CreateCoordinator(signals1, configure);
        var coord2 = CreateCoordinator(signals2, configure);

        // act
        await coord1.WaitAllAsync();
        await coord2.WaitAllAsync();

        var result1 = await coord1.GetResultAsync();
        var result2 = await coord2.GetResultAsync();

        // assert
        var failure1 = result1.Results.First(r => r.Name == "failure");
        var failure2 = result2.Results.First(r => r.Name == "failure");

        failure1.Status.Should().Be(IgnitionSignalStatus.Failed);
        failure2.Status.Should().Be(IgnitionSignalStatus.Failed);
    }

    [Fact]
    public async Task RecordingSchemaVersion_MaintainsBackwardCompatibility()
    {
        // arrange - simulate a future version that might add optional fields
        var json = """
        {
          "schemaVersion": "1.0",
          "recordingId": "test123",
          "recordedAt": "2025-12-09T18:00:00Z",
          "totalDurationMs": 100.5,
          "timedOut": false,
          "finalState": "Completed",
          "configuration": {
            "executionMode": "Parallel",
            "policy": "BestEffort",
            "globalTimeoutMs": 5000,
            "cancelOnGlobalTimeout": false,
            "cancelIndividualOnTimeout": false
          },
          "signals": [],
          "futureField": "this should be ignored"
        }
        """;

        // act
        var recording = IgnitionRecording.FromJson(json);

        // assert
        recording.Should().NotBeNull();
        recording!.SchemaVersion.Should().Be("1.0");
        recording.RecordingId.Should().Be("test123");
    }
}
