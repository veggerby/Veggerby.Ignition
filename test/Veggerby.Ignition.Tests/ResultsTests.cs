namespace Veggerby.Ignition.Tests;

public class ResultsTests
{
    [Fact]
    public void IgnitionSignalStatus_HasExpectedValues()
    {
        // assert - verify all enum values exist
        Enum.IsDefined(typeof(IgnitionSignalStatus), IgnitionSignalStatus.Succeeded).Should().BeTrue();
        Enum.IsDefined(typeof(IgnitionSignalStatus), IgnitionSignalStatus.Failed).Should().BeTrue();
        Enum.IsDefined(typeof(IgnitionSignalStatus), IgnitionSignalStatus.TimedOut).Should().BeTrue();
        Enum.IsDefined(typeof(IgnitionSignalStatus), IgnitionSignalStatus.Skipped).Should().BeTrue();
        Enum.IsDefined(typeof(IgnitionSignalStatus), IgnitionSignalStatus.Cancelled).Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_WithSuccessStatus_SetsCorrectProperties()
    {
        // arrange
        var name = "test-signal";
        var duration = TimeSpan.FromSeconds(2);

        // act
        var result = new IgnitionSignalResult(name, IgnitionSignalStatus.Succeeded, duration);

        // assert
        result.Name.Should().Be(name);
        result.Status.Should().Be(IgnitionSignalStatus.Succeeded);
        result.Duration.Should().Be(duration);
        result.Exception.Should().BeNull();
        result.FailedDependencies.Should().BeNull();
        result.CancellationReason.Should().Be(CancellationReason.None);
        result.CancelledBySignal.Should().BeNull();
        result.StartedAt.Should().BeNull();
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void IgnitionSignalResult_WithFailedStatus_IncludesException()
    {
        // arrange
        var name = "test-signal";
        var exception = new InvalidOperationException("Test error");
        var duration = TimeSpan.FromSeconds(1);

        // act
        var result = new IgnitionSignalResult(name, IgnitionSignalStatus.Failed, duration, exception);

        // assert
        result.Name.Should().Be(name);
        result.Status.Should().Be(IgnitionSignalStatus.Failed);
        result.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void IgnitionSignalResult_WithSkippedStatus_IncludesFailedDependencies()
    {
        // arrange
        var name = "test-signal";
        var failedDeps = new List<string> { "dep1", "dep2" };
        var duration = TimeSpan.Zero;

        // act
        var result = new IgnitionSignalResult(name, IgnitionSignalStatus.Skipped, duration, FailedDependencies: failedDeps);

        // assert
        result.Status.Should().Be(IgnitionSignalStatus.Skipped);
        result.FailedDependencies.Should().BeEquivalentTo(failedDeps);
    }

    [Fact]
    public void IgnitionSignalResult_WithCancelledStatus_IncludesCancellationReason()
    {
        // arrange
        var name = "test-signal";
        var duration = TimeSpan.FromMilliseconds(500);

        // act
        var result = new IgnitionSignalResult(
            name,
            IgnitionSignalStatus.Cancelled,
            duration,
            CancellationReason: CancellationReason.ScopeCancelled);

        // assert
        result.Status.Should().Be(IgnitionSignalStatus.Cancelled);
        result.CancellationReason.Should().Be(CancellationReason.ScopeCancelled);
    }

    [Fact]
    public void IgnitionSignalResult_SkippedDueToDependencies_ReturnsTrueWhenSkippedWithDependencies()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Skipped,
            TimeSpan.Zero,
            FailedDependencies: new List<string> { "dep1" });

        // act & assert
        result.SkippedDueToDependencies.Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_SkippedDueToDependencies_ReturnsFalseWhenNotSkipped()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.Zero,
            FailedDependencies: new List<string> { "dep1" });

        // act & assert
        result.SkippedDueToDependencies.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_SkippedDueToDependencies_ReturnsFalseWhenNoDependencies()
    {
        // arrange
        var result = new IgnitionSignalResult("test", IgnitionSignalStatus.Skipped, TimeSpan.Zero);

        // act & assert
        result.SkippedDueToDependencies.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_HasFailedDependencies_ReturnsTrueWhenDependenciesExist()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.Zero,
            FailedDependencies: new List<string> { "dep1", "dep2" });

        // act & assert
        result.HasFailedDependencies.Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_HasFailedDependencies_ReturnsFalseWhenNoDependencies()
    {
        // arrange
        var result = new IgnitionSignalResult("test", IgnitionSignalStatus.Succeeded, TimeSpan.Zero);

        // act & assert
        result.HasFailedDependencies.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_WasCancelledByScope_ReturnsTrueForScopeCancelled()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Cancelled,
            TimeSpan.Zero,
            CancellationReason: CancellationReason.ScopeCancelled);

        // act & assert
        result.WasCancelledByScope.Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_WasCancelledByScope_ReturnsTrueForBundleCancelled()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Cancelled,
            TimeSpan.Zero,
            CancellationReason: CancellationReason.BundleCancelled);

        // act & assert
        result.WasCancelledByScope.Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_WasCancelledByScope_ReturnsTrueForDependencyFailed()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Cancelled,
            TimeSpan.Zero,
            CancellationReason: CancellationReason.DependencyFailed);

        // act & assert
        result.WasCancelledByScope.Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_WasCancelledByScope_ReturnsFalseForNone()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.Zero,
            CancellationReason: CancellationReason.None);

        // act & assert
        result.WasCancelledByScope.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_WasCancelledByScope_ReturnsFalseForPerSignalTimeout()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.TimedOut,
            TimeSpan.Zero,
            CancellationReason: CancellationReason.PerSignalTimeout);

        // act & assert
        result.WasCancelledByScope.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_HasTimelineData_ReturnsTrueWhenBothTimestampsPresent()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.FromSeconds(1),
            StartedAt: TimeSpan.FromMilliseconds(100),
            CompletedAt: TimeSpan.FromMilliseconds(1100));

        // act & assert
        result.HasTimelineData.Should().BeTrue();
    }

    [Fact]
    public void IgnitionSignalResult_HasTimelineData_ReturnsFalseWhenStartedAtMissing()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.FromSeconds(1),
            CompletedAt: TimeSpan.FromMilliseconds(1100));

        // act & assert
        result.HasTimelineData.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_HasTimelineData_ReturnsFalseWhenCompletedAtMissing()
    {
        // arrange
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Succeeded,
            TimeSpan.FromSeconds(1),
            StartedAt: TimeSpan.FromMilliseconds(100));

        // act & assert
        result.HasTimelineData.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_WithCancelledBySignal_StoresSignalName()
    {
        // arrange
        var cancelledBy = "parent-signal";
        var result = new IgnitionSignalResult(
            "test",
            IgnitionSignalStatus.Cancelled,
            TimeSpan.Zero,
            CancelledBySignal: cancelledBy);

        // act & assert
        result.CancelledBySignal.Should().Be(cancelledBy);
    }

    [Fact]
    public void IgnitionResult_EmptySuccess_ReturnsEmptyResult()
    {
        // act
        var result = IgnitionResult.EmptySuccess;

        // assert
        result.TotalDuration.Should().Be(TimeSpan.Zero);
        result.Results.Should().BeEmpty();
        result.TimedOut.Should().BeFalse();
        result.StageResults.Should().BeNull();
    }

    [Fact]
    public void IgnitionResult_FromResults_CreatesSuccessResult()
    {
        // arrange
        var signalResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1)),
            new("signal2", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(2))
        };
        var totalDuration = TimeSpan.FromSeconds(3);

        // act
        var result = IgnitionResult.FromResults(signalResults, totalDuration);

        // assert
        result.TotalDuration.Should().Be(totalDuration);
        result.Results.Should().BeEquivalentTo(signalResults);
        result.TimedOut.Should().BeFalse();
        result.StageResults.Should().BeNull();
    }

    [Fact]
    public void IgnitionResult_FromTimeout_CreatesTimeoutResult()
    {
        // arrange
        var partialResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1)),
            new("signal2", IgnitionSignalStatus.TimedOut, TimeSpan.FromSeconds(10))
        };
        var totalDuration = TimeSpan.FromSeconds(10);

        // act
        var result = IgnitionResult.FromTimeout(partialResults, totalDuration);

        // assert
        result.TotalDuration.Should().Be(totalDuration);
        result.Results.Should().BeEquivalentTo(partialResults);
        result.TimedOut.Should().BeTrue();
        result.StageResults.Should().BeNull();
    }

    [Fact]
    public void IgnitionResult_FromStaged_CreatesStagedResult()
    {
        // arrange
        var signalResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1)),
            new("signal2", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(2))
        };
        var stageResults = new List<Stages.IgnitionStageResult>
        {
            new(1, TimeSpan.FromSeconds(2), new List<IgnitionSignalResult> { signalResults[0] }, 1, 0, 0, true, false)
        };
        var totalDuration = TimeSpan.FromSeconds(3);

        // act
        var result = IgnitionResult.FromStaged(signalResults, stageResults, totalDuration);

        // assert
        result.TotalDuration.Should().Be(totalDuration);
        result.Results.Should().BeEquivalentTo(signalResults);
        result.TimedOut.Should().BeFalse();
        result.StageResults.Should().BeEquivalentTo(stageResults);
    }

    [Fact]
    public void IgnitionResult_FromStaged_WithTimeout_SetsFlagCorrectly()
    {
        // arrange
        var signalResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1))
        };
        var stageResults = new List<Stages.IgnitionStageResult>();
        var totalDuration = TimeSpan.FromSeconds(10);

        // act
        var result = IgnitionResult.FromStaged(signalResults, stageResults, totalDuration, timedOut: true);

        // assert
        result.TimedOut.Should().BeTrue();
    }

    [Fact]
    public void IgnitionResult_HasStageResults_ReturnsTrueWhenStageResultsPresent()
    {
        // arrange
        var stageResults = new List<Stages.IgnitionStageResult>
        {
            new(1, TimeSpan.FromSeconds(1), new List<IgnitionSignalResult>(), 0, 0, 0, true, false)
        };
        var result = new IgnitionResult(TimeSpan.FromSeconds(1), new List<IgnitionSignalResult>(), false, stageResults);

        // act & assert
        result.HasStageResults.Should().BeTrue();
    }

    [Fact]
    public void IgnitionResult_HasStageResults_ReturnsFalseWhenStageResultsNull()
    {
        // arrange
        var result = new IgnitionResult(TimeSpan.FromSeconds(1), new List<IgnitionSignalResult>(), false);

        // act & assert
        result.HasStageResults.Should().BeFalse();
    }

    [Fact]
    public void IgnitionResult_HasStageResults_ReturnsFalseWhenStageResultsEmpty()
    {
        // arrange
        var result = new IgnitionResult(
            TimeSpan.FromSeconds(1),
            new List<IgnitionSignalResult>(),
            false,
            new List<Stages.IgnitionStageResult>());

        // act & assert
        result.HasStageResults.Should().BeFalse();
    }

    [Fact]
    public void IgnitionResult_HasTimelineData_ReturnsTrueWhenAllResultsHaveTimeline()
    {
        // arrange
        var signalResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1),
                StartedAt: TimeSpan.FromMilliseconds(0), CompletedAt: TimeSpan.FromMilliseconds(1000)),
            new("signal2", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1),
                StartedAt: TimeSpan.FromMilliseconds(1000), CompletedAt: TimeSpan.FromMilliseconds(2000))
        };
        var result = new IgnitionResult(TimeSpan.FromSeconds(2), signalResults, false);

        // act & assert
        result.HasTimelineData.Should().BeTrue();
    }

    [Fact]
    public void IgnitionResult_HasTimelineData_ReturnsFalseWhenAnyResultLacksTimeline()
    {
        // arrange
        var signalResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1),
                StartedAt: TimeSpan.FromMilliseconds(0), CompletedAt: TimeSpan.FromMilliseconds(1000)),
            new("signal2", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1)) // No timeline
        };
        var result = new IgnitionResult(TimeSpan.FromSeconds(2), signalResults, false);

        // act & assert
        result.HasTimelineData.Should().BeFalse();
    }

    [Fact]
    public void IgnitionResult_HasTimelineData_ReturnsFalseWhenNoResults()
    {
        // arrange
        var result = new IgnitionResult(TimeSpan.FromSeconds(0), new List<IgnitionSignalResult>(), false);

        // act & assert
        result.HasTimelineData.Should().BeFalse();
    }

    [Fact]
    public void IgnitionResult_RecordEquality_SameValues_AreEqual()
    {
        // arrange
        var signalResults = new List<IgnitionSignalResult>
        {
            new("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1))
        };
        var result1 = new IgnitionResult(TimeSpan.FromSeconds(1), signalResults, false);
        var result2 = new IgnitionResult(TimeSpan.FromSeconds(1), signalResults, false);

        // act & assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void IgnitionSignalResult_RecordEquality_SameValues_AreEqual()
    {
        // arrange
        var result1 = new IgnitionSignalResult("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1));
        var result2 = new IgnitionSignalResult("signal1", IgnitionSignalStatus.Succeeded, TimeSpan.FromSeconds(1));

        // act & assert
        result1.Should().Be(result2);
    }
}
