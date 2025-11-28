using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionStagedExecutionTests
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
    public async Task Staged_ZeroSignals_ReturnsEmptySuccess()
    {
        // arrange
        var coord = CreateCoordinator([], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Staged;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().BeEmpty();
        result.StageResults.Should().BeNull(); // Empty case uses fast path
    }

    [Fact]
    public async Task Staged_SingleStage_AllSignalsSucceed()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", async ct => await Task.Delay(10, ct));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(2);
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
        result.HasStageResults.Should().BeTrue();
        result.StageResults.Should().HaveCount(1);
        result.StageResults![0].StageNumber.Should().Be(0);
        result.StageResults[0].AllSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Staged_MultipleStages_ExecuteInOrder()
    {
        // arrange
        var executionOrder = new List<string>();
        var stage0Signal = new StagedFakeSignal("stage0", 0, async ct =>
        {
            executionOrder.Add("stage0-start");
            await Task.Delay(20, ct);
            executionOrder.Add("stage0-end");
        });
        var stage1Signal = new StagedFakeSignal("stage1", 1, async ct =>
        {
            executionOrder.Add("stage1-start");
            await Task.Delay(10, ct);
            executionOrder.Add("stage1-end");
        });
        var stage2Signal = new StagedFakeSignal("stage2", 2, ct =>
        {
            executionOrder.Add("stage2-start");
            executionOrder.Add("stage2-end");
            return Task.CompletedTask;
        });

        var coord = CreateCoordinator(new IIgnitionSignal[] { stage2Signal, stage0Signal, stage1Signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(3);
        result.HasStageResults.Should().BeTrue();
        result.StageResults.Should().HaveCount(3);

        // Verify stage order
        result.StageResults![0].StageNumber.Should().Be(0);
        result.StageResults[1].StageNumber.Should().Be(1);
        result.StageResults[2].StageNumber.Should().Be(2);

        // Verify execution order
        var stage0EndIndex = executionOrder.IndexOf("stage0-end");
        var stage1StartIndex = executionOrder.IndexOf("stage1-start");
        var stage1EndIndex = executionOrder.IndexOf("stage1-end");
        var stage2StartIndex = executionOrder.IndexOf("stage2-start");

        stage0EndIndex.Should().BeLessThan(stage1StartIndex, "Stage 0 should complete before Stage 1 starts");
        stage1EndIndex.Should().BeLessThan(stage2StartIndex, "Stage 1 should complete before Stage 2 starts");
    }

    [Fact]
    public async Task Staged_ParallelWithinStage_SignalsRunConcurrently()
    {
        // arrange
        var concurrencyCount = 0;
        var maxConcurrency = 0;
        var lockObj = new object();

        var signals = Enumerable.Range(0, 5).Select(i => new StagedFakeSignal($"s{i}", 0, async ct =>
        {
            lock (lockObj)
            {
                concurrencyCount++;
                if (concurrencyCount > maxConcurrency)
                {
                    maxConcurrency = concurrencyCount;
                }
            }
            await Task.Delay(50, ct);
            lock (lockObj)
            {
                concurrencyCount--;
            }
        })).ToArray();

        var coord = CreateCoordinator(signals, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(5);
        maxConcurrency.Should().BeGreaterThan(1, "Signals within a stage should run concurrently");
    }

    [Fact]
    public async Task Staged_AllMustSucceed_StopsOnFailure()
    {
        // arrange
        var stage0Success = new StagedFakeSignal("stage0-ok", 0, _ => Task.CompletedTask);
        var stage0Fail = new StagedFakeSignal("stage0-fail", 0, _ => Task.FromException(new InvalidOperationException("boom")));
        var stage1Never = new StagedFakeSignal("stage1-never", 1, _ => Task.CompletedTask);

        var coord = CreateCoordinator(new IIgnitionSignal[] { stage0Success, stage0Fail, stage1Never }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.StagePolicy = IgnitionStagePolicy.AllMustSucceed;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(3);
        result.Results.Should().Contain(r => r.Name == "stage0-ok" && r.Status == IgnitionSignalStatus.Succeeded);
        result.Results.Should().Contain(r => r.Name == "stage0-fail" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "stage1-never" && r.Status == IgnitionSignalStatus.Skipped);

        result.StageResults.Should().HaveCount(2);
        result.StageResults![0].HasFailures.Should().BeTrue();
        result.StageResults[1].Completed.Should().BeFalse();
    }

    [Fact]
    public async Task Staged_BestEffort_ContinuesOnFailure()
    {
        // arrange
        var stage0Fail = new StagedFakeSignal("stage0-fail", 0, _ => Task.FromException(new InvalidOperationException("boom")));
        var stage1Success = new StagedFakeSignal("stage1-ok", 1, _ => Task.CompletedTask);

        var coord = CreateCoordinator(new IIgnitionSignal[] { stage0Fail, stage1Success }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.StagePolicy = IgnitionStagePolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "stage0-fail" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "stage1-ok" && r.Status == IgnitionSignalStatus.Succeeded);

        result.StageResults.Should().HaveCount(2);
        result.StageResults![0].HasFailures.Should().BeTrue();
        result.StageResults[1].AllSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Staged_FailFast_StopsOnFailure()
    {
        // arrange
        var stage0Fail = new StagedFakeSignal("stage0-fail", 0, _ => Task.FromException(new InvalidOperationException("boom")));
        var stage1Never = new StagedFakeSignal("stage1-never", 1, _ => Task.CompletedTask);

        var coord = CreateCoordinator(new IIgnitionSignal[] { stage0Fail, stage1Never }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.StagePolicy = IgnitionStagePolicy.FailFast;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "stage0-fail" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "stage1-never" && r.Status == IgnitionSignalStatus.Skipped);
    }

    [Fact]
    public async Task Staged_EarlyPromotion_ProceedsWhenThresholdMet()
    {
        // arrange
        var stage0Started = new TaskCompletionSource();
        var stage1Started = new TaskCompletionSource();

        var stage0Fast1 = new StagedFakeSignal("stage0-fast1", 0, _ =>
        {
            stage0Started.TrySetResult();
            return Task.CompletedTask;
        });
        var stage0Fast2 = new StagedFakeSignal("stage0-fast2", 0, _ => Task.CompletedTask);
        var stage0Slow = new StagedFakeSignal("stage0-slow", 0, async ct =>
        {
            await Task.Delay(200, ct); // Still running when stage 1 starts
        });
        var stage1Signal = new StagedFakeSignal("stage1", 1, _ =>
        {
            stage1Started.TrySetResult();
            return Task.CompletedTask;
        });

        var coord = CreateCoordinator(new IIgnitionSignal[] { stage0Fast1, stage0Fast2, stage0Slow, stage1Signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.StagePolicy = IgnitionStagePolicy.EarlyPromotion;
            o.EarlyPromotionThreshold = 0.66; // 2/3 = 66% required
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(4);
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);

        // Verify early promotion occurred
        result.StageResults.Should().HaveCount(2);
        result.StageResults![0].Promoted.Should().BeTrue("Stage 0 should be marked as promoted");
    }

    [Fact]
    public async Task Staged_GlobalTimeout_CancelsRemainingSignals()
    {
        // arrange
        var slowSignal = new StagedFakeSignal("slow", 0, async ct => await Task.Delay(500, ct));

        var coord = CreateCoordinator(new[] { slowSignal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeTrue();
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task Staged_MaxDegreeOfParallelism_LimitsConcurrency()
    {
        // arrange
        var concurrencyCount = 0;
        var maxConcurrency = 0;
        var lockObj = new object();

        var signals = Enumerable.Range(0, 6).Select(i => new StagedFakeSignal($"s{i}", 0, async ct =>
        {
            lock (lockObj)
            {
                concurrencyCount++;
                if (concurrencyCount > maxConcurrency)
                {
                    maxConcurrency = concurrencyCount;
                }
            }
            await Task.Delay(30, ct);
            lock (lockObj)
            {
                concurrencyCount--;
            }
        })).ToArray();

        var coord = CreateCoordinator(signals, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.MaxDegreeOfParallelism = 2;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(6);
        maxConcurrency.Should().BeLessThanOrEqualTo(2, "Concurrency should be limited by MaxDegreeOfParallelism");
    }

    [Fact]
    public async Task Staged_MixedStagedAndUnstagedSignals_UnstagedDefaultToStageZero()
    {
        // arrange
        var unstagedSignal = new FakeSignal("unstaged", _ => Task.CompletedTask);
        var stagedSignal = new StagedFakeSignal("staged-1", 1, _ => Task.CompletedTask);

        var coord = CreateCoordinator(new IIgnitionSignal[] { unstagedSignal, stagedSignal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.StageResults.Should().HaveCount(2);
        result.StageResults![0].StageNumber.Should().Be(0);
        result.StageResults[0].Results.Should().Contain(r => r.Name == "unstaged");
        result.StageResults[1].StageNumber.Should().Be(1);
        result.StageResults[1].Results.Should().Contain(r => r.Name == "staged-1");
    }

    [Fact]
    public async Task Staged_StageResult_ContainsCorrectTiming()
    {
        // arrange
        var stage0Signal = new StagedFakeSignal("stage0", 0, async ct => await Task.Delay(50, ct));

        var coord = CreateCoordinator(new[] { stage0Signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.StageResults.Should().HaveCount(1);
        result.StageResults![0].Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(45));
    }

    [Fact]
    public async Task AddIgnitionSignalWithStage_RegistersSignalWithStage()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        services.AddIgnition(o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        var signal = new FakeSignal("my-signal", _ => Task.CompletedTask);
        services.AddIgnitionSignalWithStage(signal, stage: 2);

        var provider = services.BuildServiceProvider();

        // act
        var coord = provider.GetRequiredService<IIgnitionCoordinator>();
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.StageResults.Should().HaveCount(1);
        result.StageResults![0].StageNumber.Should().Be(2);
    }

    [Fact]
    public async Task AddIgnitionFromTaskWithStage_RegistersSignalWithStage()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        services.AddIgnition(o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Staged;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        services.AddIgnitionFromTaskWithStage("my-task-signal", _ => Task.CompletedTask, stage: 1);

        var provider = services.BuildServiceProvider();

        // act
        var coord = provider.GetRequiredService<IIgnitionCoordinator>();
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Name.Should().Be("my-task-signal");
        result.StageResults.Should().HaveCount(1);
        result.StageResults![0].StageNumber.Should().Be(1);
    }

    [Fact]
    public void AddIgnitionSignalWithStage_ThrowsOnNegativeStage()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        // act & assert
        var act = () => services.AddIgnitionSignalWithStage(signal, stage: -1);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("stage");
    }

    [Fact]
    public void EarlyPromotionThreshold_ThrowsOnInvalidValue()
    {
        // arrange
        var options = new IgnitionOptions();

        // act & assert
        var act1 = () => options.EarlyPromotionThreshold = -0.1;
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => options.EarlyPromotionThreshold = 1.1;
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StageResult_SuccessRatio_CalculatesCorrectly()
    {
        // arrange
        var results = new List<IgnitionSignalResult>
        {
            new("s1", IgnitionSignalStatus.Succeeded, TimeSpan.Zero),
            new("s2", IgnitionSignalStatus.Succeeded, TimeSpan.Zero),
            new("s3", IgnitionSignalStatus.Failed, TimeSpan.Zero),
            new("s4", IgnitionSignalStatus.TimedOut, TimeSpan.Zero),
        };

        var stageResult = new IgnitionStageResult(
            StageNumber: 0,
            Duration: TimeSpan.FromMilliseconds(100),
            Results: results,
            SucceededCount: 2,
            FailedCount: 1,
            TimedOutCount: 1,
            Completed: true);

        // act & assert
        stageResult.TotalSignals.Should().Be(4);
        stageResult.SuccessRatio.Should().Be(0.5);
        stageResult.AllSucceeded.Should().BeFalse();
        stageResult.HasFailures.Should().BeTrue();
        stageResult.HasTimeouts.Should().BeTrue();
    }

    [Fact]
    public void StageResult_EmptyStage_HasSuccessRatioOfOne()
    {
        // arrange
        var stageResult = new IgnitionStageResult(
            StageNumber: 0,
            Duration: TimeSpan.Zero,
            Results: new List<IgnitionSignalResult>(),
            SucceededCount: 0,
            FailedCount: 0,
            TimedOutCount: 0,
            Completed: true);

        // act & assert
        stageResult.SuccessRatio.Should().Be(1.0);
    }
}

/// <summary>
/// Test helper signal that implements <see cref="IStagedIgnitionSignal"/>.
/// </summary>
internal sealed class StagedFakeSignal : IStagedIgnitionSignal
{
    private readonly Func<CancellationToken, Task> _action;

    public StagedFakeSignal(string name, int stage, Func<CancellationToken, Task> action, TimeSpan? timeout = null)
    {
        Name = name;
        Stage = stage;
        _action = action;
        Timeout = timeout;
    }

    public string Name { get; }
    public int Stage { get; }
    public TimeSpan? Timeout { get; }

    public Task WaitAsync(CancellationToken cancellationToken = default) => _action(cancellationToken);
}
