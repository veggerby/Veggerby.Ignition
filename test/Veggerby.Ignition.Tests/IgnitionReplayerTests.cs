using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionReplayerTests
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

    #region Validation Tests

    [Fact]
    public async Task Validate_ValidRecording_ReturnsNoErrors()
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

        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();
        var recording = result.ExportRecording(options);
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.IsValid.Should().BeTrue();
        validation.ErrorCount.Should().Be(0);
    }

    [Fact]
    public void Validate_NegativeDuration_ReturnsError()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "bad-signal",
                    Status: "Succeeded",
                    StartMs: 100,
                    EndMs: 200,
                    DurationMs: -50) // Invalid negative duration
            },
            TotalDurationMs = 200
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().Contain(i =>
            i.Code == "NEGATIVE_DURATION" &&
            i.SignalName == "bad-signal");
    }

    [Fact]
    public void Validate_EndBeforeStart_ReturnsError()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "bad-signal",
                    Status: "Succeeded",
                    StartMs: 200,
                    EndMs: 100, // Invalid: end before start
                    DurationMs: 100)
            },
            TotalDurationMs = 200
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().Contain(i =>
            i.Code == "INVALID_TIME_RANGE" &&
            i.SignalName == "bad-signal");
    }

    [Fact]
    public void Validate_DurationDrift_ReturnsWarning()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "drift-signal",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 110) // Duration doesn't match end-start
            },
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.Issues.Should().Contain(i =>
            i.Code == "DURATION_DRIFT" &&
            i.SignalName == "drift-signal");
    }

    [Fact]
    public void Validate_MissingConfiguration_ReturnsWarning()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "signal1",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 100)
            },
            Configuration = null, // Missing configuration
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.Issues.Should().Contain(i => i.Code == "MISSING_CONFIGURATION");
    }

    [Fact]
    public void Validate_DependencyOrderViolation_ReturnsError()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "parent",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 100),
                new IgnitionRecordedSignal(
                    SignalName: "child",
                    Status: "Succeeded",
                    StartMs: 50, // Started before parent finished
                    EndMs: 150,
                    DurationMs: 100,
                    Dependencies: new List<string> { "parent" })
            },
            TotalDurationMs = 150
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.IsValid.Should().BeFalse();
        validation.Issues.Should().Contain(i =>
            i.Code == "DEPENDENCY_ORDER_VIOLATION" &&
            i.SignalName == "child");
    }

    [Fact]
    public void Validate_CountMismatch_ReturnsWarning()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "signal1",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 100)
            },
            Summary = new IgnitionRecordingSummary(
                TotalSignals: 5, // Mismatch
                SucceededCount: 5,
                FailedCount: 0,
                TimedOutCount: 0,
                SkippedCount: 0,
                CancelledCount: 0,
                MaxConcurrency: 1,
                SlowestSignalName: null,
                SlowestDurationMs: null,
                FastestSignalName: null,
                FastestDurationMs: null,
                AverageDurationMs: null),
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var validation = replayer.Validate();

        // assert
        validation.Issues.Should().Contain(i => i.Code == "TOTAL_MISMATCH");
    }

    #endregion

    #region What-If Simulation Tests

    [Fact]
    public void SimulateEarlierTimeout_MarksSignalAsTimedOut()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "slow-signal",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 1000,
                    DurationMs: 1000)
            },
            TotalDurationMs = 1000
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var simulation = replayer.SimulateEarlierTimeout("slow-signal", 500);

        // assert
        simulation.AffectedSignals.Should().Contain("slow-signal");
        simulation.SimulatedSignals.Single().Status.Should().Be("TimedOut");
        simulation.SimulatedSignals.Single().DurationMs.Should().Be(500);
    }

    [Fact]
    public void SimulateEarlierTimeout_DoesNotAffectFastSignal()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "fast-signal",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 100)
            },
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var simulation = replayer.SimulateEarlierTimeout("fast-signal", 500);

        // assert
        simulation.AffectedSignals.Should().BeEmpty();
        simulation.SimulatedSignals.Single().Status.Should().Be("Succeeded");
    }

    [Fact]
    public void SimulateEarlierTimeout_PropagesToDependents()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "parent",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 1000,
                    DurationMs: 1000),
                new IgnitionRecordedSignal(
                    SignalName: "child",
                    Status: "Succeeded",
                    StartMs: 1000,
                    EndMs: 1100,
                    DurationMs: 100,
                    Dependencies: new List<string> { "parent" })
            },
            TotalDurationMs = 1100
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var simulation = replayer.SimulateEarlierTimeout("parent", 500);

        // assert
        simulation.AffectedSignals.Should().Contain("parent");
        simulation.AffectedSignals.Should().Contain("child");
        var childSignal = simulation.SimulatedSignals.Single(s => s.SignalName == "child");
        childSignal.Status.Should().Be("Skipped");
    }

    [Fact]
    public void SimulateEarlierTimeout_ThrowsForUnknownSignal()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>()
        };
        var replayer = new IgnitionReplayer(recording);

        // act & assert
        var action = () => replayer.SimulateEarlierTimeout("unknown-signal", 100);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*unknown-signal*");
    }

    [Fact]
    public void SimulateFailure_MarksSignalAsFailed()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "success-signal",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 100)
            },
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var simulation = replayer.SimulateFailure("success-signal");

        // assert
        simulation.AffectedSignals.Should().Contain("success-signal");
        simulation.SimulatedSignals.Single().Status.Should().Be("Failed");
    }

    [Fact]
    public void SimulateFailure_PropagesToDependents()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "parent",
                    Status: "Succeeded",
                    StartMs: 0,
                    EndMs: 100,
                    DurationMs: 100),
                new IgnitionRecordedSignal(
                    SignalName: "child",
                    Status: "Succeeded",
                    StartMs: 100,
                    EndMs: 150,
                    DurationMs: 50,
                    Dependencies: new List<string> { "parent" })
            },
            TotalDurationMs = 150
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var simulation = replayer.SimulateFailure("parent");

        // assert
        simulation.AffectedSignals.Should().Contain("parent");
        simulation.AffectedSignals.Should().Contain("child");
        var childSignal = simulation.SimulatedSignals.Single(s => s.SignalName == "child");
        childSignal.Status.Should().Be("Skipped");
        childSignal.FailedDependencies.Should().Contain("parent");
    }

    #endregion

    #region Comparison Tests

    [Fact]
    public void CompareTo_CalculatesDurationDifference()
    {
        // arrange
        var recording1 = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("signal1", "Succeeded", 0, 100, 100)
            },
            TotalDurationMs = 100
        };
        var recording2 = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("signal1", "Succeeded", 0, 150, 150)
            },
            TotalDurationMs = 150
        };
        var replayer = new IgnitionReplayer(recording1);

        // act
        var comparison = replayer.CompareTo(recording2);

        // assert
        comparison.DurationDifferenceMs.Should().Be(50);
        comparison.DurationChangePercent.Should().Be(50);
    }

    [Fact]
    public void CompareTo_IdentifiesAddedAndRemovedSignals()
    {
        // arrange
        var recording1 = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("signal1", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("signal2", "Succeeded", 0, 100, 100)
            },
            TotalDurationMs = 100
        };
        var recording2 = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("signal1", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("signal3", "Succeeded", 0, 100, 100)
            },
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording1);

        // act
        var comparison = replayer.CompareTo(recording2);

        // assert
        comparison.AddedSignals.Should().Contain("signal3");
        comparison.RemovedSignals.Should().Contain("signal2");
    }

    [Fact]
    public void CompareTo_DetectsStatusChanges()
    {
        // arrange
        var recording1 = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("signal1", "Succeeded", 0, 100, 100)
            },
            TotalDurationMs = 100
        };
        var recording2 = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("signal1", "Failed", 0, 100, 100)
            },
            TotalDurationMs = 100
        };
        var replayer = new IgnitionReplayer(recording1);

        // act
        var comparison = replayer.CompareTo(recording2);

        // assert
        var signalComparison = comparison.SignalComparisons.Single(c => c.SignalName == "signal1");
        signalComparison.StatusChanged.Should().BeTrue();
        signalComparison.Status1.Should().Be("Succeeded");
        signalComparison.Status2.Should().Be("Failed");
    }

    #endregion

    #region Analysis Tests

    [Fact]
    public void IdentifySlowSignals_ReturnsSignalsAboveThreshold()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("fast", "Succeeded", 0, 50, 50),
                new IgnitionRecordedSignal("medium", "Succeeded", 0, 150, 150),
                new IgnitionRecordedSignal("slow", "Succeeded", 0, 500, 500)
            },
            TotalDurationMs = 500
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var slowSignals = replayer.IdentifySlowSignals(100);

        // assert
        slowSignals.Should().HaveCount(2);
        slowSignals.First().SignalName.Should().Be("slow");
        slowSignals.Last().SignalName.Should().Be("medium");
    }

    [Fact]
    public void IdentifySlowSignals_ExcludesFailedSignals()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("failed-slow", "Failed", 0, 500, 500),
                new IgnitionRecordedSignal("success-slow", "Succeeded", 0, 400, 400)
            },
            TotalDurationMs = 500
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var slowSignals = replayer.IdentifySlowSignals(100);

        // assert
        slowSignals.Should().HaveCount(1);
        slowSignals.Single().SignalName.Should().Be("success-slow");
    }

    [Fact]
    public void IdentifyCriticalPath_ReturnsSignalsEndingAtTotalDuration()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("fast", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("critical", "Succeeded", 0, 500, 500)
            },
            TotalDurationMs = 500,
            Configuration = new IgnitionRecordingConfiguration(
                ExecutionMode: "Parallel",
                Policy: "BestEffort",
                GlobalTimeoutMs: 5000,
                CancelOnGlobalTimeout: false,
                CancelIndividualOnTimeout: false)
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var criticalPath = replayer.IdentifyCriticalPath();

        // assert
        criticalPath.Should().HaveCount(1);
        criticalPath.Single().SignalName.Should().Be("critical");
    }

    [Fact]
    public void GetExecutionOrder_ReturnsSignalsInStartOrder()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("third", "Succeeded", 200, 300, 100),
                new IgnitionRecordedSignal("first", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("second", "Succeeded", 100, 200, 100)
            },
            TotalDurationMs = 300
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var order = replayer.GetExecutionOrder();

        // assert
        order.Should().BeEquivalentTo(new[] { "first", "second", "third" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void GetConcurrentGroups_IdentifiesOverlappingSignals()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("parallel1", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("parallel2", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("sequential", "Succeeded", 100, 200, 100)
            },
            TotalDurationMs = 200
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var groups = replayer.GetConcurrentGroups();

        // assert
        groups.Should().HaveCount(2);
        groups.First().Should().Contain("parallel1").And.Contain("parallel2");
        groups.Last().Should().Contain("sequential");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Replayer_Constructor_ThrowsForNullRecording()
    {
        // act & assert
        var action = () => new IgnitionReplayer(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IdentifyCriticalPath_EmptyRecording_ReturnsEmpty()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>()
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var criticalPath = replayer.IdentifyCriticalPath();

        // assert
        criticalPath.Should().BeEmpty();
    }

    [Fact]
    public void GetConcurrentGroups_EmptyRecording_ReturnsEmpty()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>()
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var groups = replayer.GetConcurrentGroups();

        // assert
        groups.Should().BeEmpty();
    }

    [Fact]
    public void IdentifyCriticalPath_SequentialExecution_ReturnsAllSignals()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal("first", "Succeeded", 0, 100, 100),
                new IgnitionRecordedSignal("second", "Succeeded", 100, 200, 100)
            },
            TotalDurationMs = 200,
            Configuration = new IgnitionRecordingConfiguration(
                ExecutionMode: "Sequential",
                Policy: "BestEffort",
                GlobalTimeoutMs: 5000,
                CancelOnGlobalTimeout: false,
                CancelIndividualOnTimeout: false)
        };
        var replayer = new IgnitionReplayer(recording);

        // act
        var criticalPath = replayer.IdentifyCriticalPath();

        // assert
        criticalPath.Should().HaveCount(2);
    }

    [Fact]
    public void CompareTo_ThrowsForNullRecording()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>()
        };
        var replayer = new IgnitionReplayer(recording);

        // act & assert
        var action = () => replayer.CompareTo(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetIssuesBySeverity_FiltersCorrectly()
    {
        // arrange
        var recording = new IgnitionRecording
        {
            Signals = new List<IgnitionRecordedSignal>
            {
                new IgnitionRecordedSignal(
                    SignalName: "bad-signal",
                    Status: "Succeeded",
                    StartMs: 100,
                    EndMs: 200,
                    DurationMs: -50) // Error: negative duration
            },
            Configuration = null, // Warning: missing configuration
            TotalDurationMs = 200
        };
        var replayer = new IgnitionReplayer(recording);
        var validation = replayer.Validate();

        // act
        var errors = validation.GetIssuesBySeverity(ReplayValidationSeverity.Error);
        var warnings = validation.GetIssuesBySeverity(ReplayValidationSeverity.Warning);

        // assert
        errors.Should().NotBeEmpty();
        warnings.Should().NotBeEmpty();
    }

    #endregion
}
