using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionStateMachineTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        return new IgnitionCoordinator(signals, optionsWrapper, logger);
    }

    #region State Tests

    [Fact]
    public void InitialState_IsNotStarted()
    {
        // arrange
        var coord = CreateCoordinator([]);

        // act
        var state = coord.State;

        // assert
        state.Should().Be(IgnitionState.NotStarted);
    }

    [Fact]
    public async Task AfterWaitAll_WithSuccessfulSignals_StateIsCompleted()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();

        // assert
        coord.State.Should().Be(IgnitionState.Completed);
    }

    [Fact]
    public async Task AfterWaitAll_WithFailedSignal_StateIsFailed()
    {
        // arrange
        var signal = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Policy = IgnitionPolicy.BestEffort;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        coord.State.Should().Be(IgnitionState.Failed);
    }

    [Fact]
    public async Task AfterWaitAll_WithGlobalTimeout_StateIsTimedOut()
    {
        // arrange
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        coord.State.Should().Be(IgnitionState.TimedOut);
    }

    [Fact]
    public async Task AfterWaitAll_WithZeroSignals_StateIsCompleted()
    {
        // arrange
        var coord = CreateCoordinator([], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();

        // assert
        coord.State.Should().Be(IgnitionState.Completed);
    }

    #endregion

    #region SignalStarted Event Tests

    [Fact]
    public async Task SignalStarted_IsRaisedForEachSignal_Parallel()
    {
        // arrange
        var startedSignals = new ConcurrentBag<string>();
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var coord = CreateCoordinator([s1, s2], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalStarted += (sender, e) => startedSignals.Add(e.SignalName);

        // act
        await coord.WaitAllAsync();

        // assert
        startedSignals.Should().HaveCount(2);
        startedSignals.Should().Contain("s1");
        startedSignals.Should().Contain("s2");
    }

    [Fact]
    public async Task SignalStarted_IsRaisedForEachSignal_Sequential()
    {
        // arrange
        var startedSignals = new List<string>();
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var coord = CreateCoordinator([s1, s2], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalStarted += (sender, e) => startedSignals.Add(e.SignalName);

        // act
        await coord.WaitAllAsync();

        // assert
        startedSignals.Should().HaveCount(2);
        startedSignals[0].Should().Be("s1");
        startedSignals[1].Should().Be("s2");
    }

    [Fact]
    public async Task SignalStarted_IncludesTimestamp()
    {
        // arrange
        DateTimeOffset timestamp = default;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalStarted += (sender, e) => timestamp = e.Timestamp;
        var before = DateTimeOffset.UtcNow;

        // act
        await coord.WaitAllAsync();
        var after = DateTimeOffset.UtcNow;

        // assert
        timestamp.Should().BeOnOrAfter(before);
        timestamp.Should().BeOnOrBefore(after);
    }

    #endregion

    #region SignalCompleted Event Tests

    [Fact]
    public async Task SignalCompleted_IsRaisedForEachSignal_Parallel()
    {
        // arrange
        var completedSignals = new ConcurrentBag<string>();
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var coord = CreateCoordinator([s1, s2], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalCompleted += (sender, e) => completedSignals.Add(e.SignalName);

        // act
        await coord.WaitAllAsync();

        // assert
        completedSignals.Should().HaveCount(2);
        completedSignals.Should().Contain("s1");
        completedSignals.Should().Contain("s2");
    }

    [Fact]
    public async Task SignalCompleted_IncludesCorrectStatus_Success()
    {
        // arrange
        IgnitionSignalStatus? status = null;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalCompleted += (sender, e) => status = e.Status;

        // act
        await coord.WaitAllAsync();

        // assert
        status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task SignalCompleted_IncludesCorrectStatus_Failed()
    {
        // arrange
        IgnitionSignalStatus? status = null;
        Exception? exception = null;
        var signal = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Policy = IgnitionPolicy.BestEffort;
        });

        coord.SignalCompleted += (sender, e) =>
        {
            status = e.Status;
            exception = e.Exception;
        };

        // act
        await coord.WaitAllAsync();

        // assert
        status.Should().Be(IgnitionSignalStatus.Failed);
        exception.Should().NotBeNull();
        exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task SignalCompleted_IncludesCorrectStatus_TimedOut()
    {
        // arrange
        IgnitionSignalStatus? status = null;
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct), timeout: TimeSpan.FromMilliseconds(50));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.CancelIndividualOnTimeout = true;
        });

        coord.SignalCompleted += (sender, e) => status = e.Status;

        // act
        await coord.WaitAllAsync();

        // assert
        status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task SignalCompleted_IncludesDuration()
    {
        // arrange
        TimeSpan duration = TimeSpan.Zero;
        var signal = new FakeSignal("test", async _ => await Task.Delay(30));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalCompleted += (sender, e) => duration = e.Duration;

        // act
        await coord.WaitAllAsync();

        // assert
        duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(20));
    }

    #endregion

    #region GlobalTimeoutReached Event Tests

    [Fact]
    public async Task GlobalTimeoutReached_IsRaisedOnTimeout()
    {
        // arrange
        bool eventRaised = false;
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        coord.GlobalTimeoutReached += (sender, e) => eventRaised = true;

        // act
        await coord.WaitAllAsync();

        // assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task GlobalTimeoutReached_IsNotRaisedWhenNoTimeout()
    {
        // arrange
        bool eventRaised = false;
        var signal = new FakeSignal("fast", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.GlobalTimeoutReached += (sender, e) => eventRaised = true;

        // act
        await coord.WaitAllAsync();

        // assert
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public async Task GlobalTimeoutReached_IncludesPendingSignals()
    {
        // arrange
        IReadOnlyList<string>? pendingSignals = null;
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        coord.GlobalTimeoutReached += (sender, e) => pendingSignals = e.PendingSignals;

        // act
        await coord.WaitAllAsync();

        // assert
        pendingSignals.Should().NotBeNull();
        pendingSignals.Should().Contain("slow");
    }

    [Fact]
    public async Task GlobalTimeoutReached_IncludesTimeoutConfiguration()
    {
        // arrange
        TimeSpan globalTimeout = TimeSpan.Zero;
        TimeSpan elapsed = TimeSpan.Zero;
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        coord.GlobalTimeoutReached += (sender, e) =>
        {
            globalTimeout = e.GlobalTimeout;
            elapsed = e.Elapsed;
        };

        // act
        await coord.WaitAllAsync();

        // assert
        globalTimeout.Should().Be(TimeSpan.FromMilliseconds(50));
        elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
    }

    #endregion

    #region CoordinatorCompleted Event Tests

    [Fact]
    public async Task CoordinatorCompleted_IsRaisedOnCompletion()
    {
        // arrange
        bool eventRaised = false;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => eventRaised = true;

        // act
        await coord.WaitAllAsync();

        // assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task CoordinatorCompleted_IncludesCorrectFinalState_Completed()
    {
        // arrange
        IgnitionState? finalState = null;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => finalState = e.FinalState;

        // act
        await coord.WaitAllAsync();

        // assert
        finalState.Should().Be(IgnitionState.Completed);
    }

    [Fact]
    public async Task CoordinatorCompleted_IncludesCorrectFinalState_Failed()
    {
        // arrange
        IgnitionState? finalState = null;
        var signal = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Policy = IgnitionPolicy.BestEffort;
        });

        coord.CoordinatorCompleted += (sender, e) => finalState = e.FinalState;

        // act
        await coord.WaitAllAsync();

        // assert
        finalState.Should().Be(IgnitionState.Failed);
    }

    [Fact]
    public async Task CoordinatorCompleted_IncludesCorrectFinalState_TimedOut()
    {
        // arrange
        IgnitionState? finalState = null;
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.CancelOnGlobalTimeout = true;
        });

        coord.CoordinatorCompleted += (sender, e) => finalState = e.FinalState;

        // act
        await coord.WaitAllAsync();

        // assert
        finalState.Should().Be(IgnitionState.TimedOut);
    }

    [Fact]
    public async Task CoordinatorCompleted_IncludesResult()
    {
        // arrange
        IgnitionResult? result = null;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => result = e.Result;

        // act
        await coord.WaitAllAsync();

        // assert
        result.Should().NotBeNull();
        result!.Results.Should().HaveCount(1);
        result.TimedOut.Should().BeFalse();
    }

    [Fact]
    public async Task CoordinatorCompleted_IncludesTotalDuration()
    {
        // arrange
        TimeSpan totalDuration = TimeSpan.Zero;
        var signal = new FakeSignal("test", async _ => await Task.Delay(30));
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => totalDuration = e.TotalDuration;

        // act
        await coord.WaitAllAsync();

        // assert
        totalDuration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(20));
    }

    #endregion

    #region Event Order Tests

    [Fact]
    public async Task Events_AreRaisedInCorrectOrder()
    {
        // arrange
        var events = new List<string>();
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalStarted += (sender, e) => events.Add($"started:{e.SignalName}");
        coord.SignalCompleted += (sender, e) => events.Add($"completed:{e.SignalName}");
        coord.CoordinatorCompleted += (sender, e) => events.Add($"coordinator:{e.FinalState}");

        // act
        await coord.WaitAllAsync();

        // assert
        events.Should().HaveCount(3);
        events[0].Should().Be("started:test");
        events[1].Should().Be("completed:test");
        events[2].Should().Be("coordinator:Completed");
    }

    [Fact]
    public async Task Events_WithMultipleSignals_AllRaised()
    {
        // arrange
        var startedCount = 0;
        var completedCount = 0;
        var coordinatorCompletedCount = 0;
        
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var s3 = new FakeSignal("s3", _ => Task.CompletedTask);
        var coord = CreateCoordinator([s1, s2, s3], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalStarted += (sender, e) => Interlocked.Increment(ref startedCount);
        coord.SignalCompleted += (sender, e) => Interlocked.Increment(ref completedCount);
        coord.CoordinatorCompleted += (sender, e) => Interlocked.Increment(ref coordinatorCompletedCount);

        // act
        await coord.WaitAllAsync();

        // assert
        startedCount.Should().Be(3);
        completedCount.Should().Be(3);
        coordinatorCompletedCount.Should().Be(1);
    }

    #endregion

    #region Event Handler Exception Tests

    [Fact]
    public async Task EventHandlerException_DoesNotBreakExecution()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.SignalStarted += (sender, e) => throw new InvalidOperationException("Handler error");
        coord.SignalCompleted += (sender, e) => throw new InvalidOperationException("Handler error");
        coord.CoordinatorCompleted += (sender, e) => throw new InvalidOperationException("Handler error");

        // act
        var act = async () => await coord.WaitAllAsync();

        // assert
        await act.Should().NotThrowAsync();
        coord.State.Should().Be(IgnitionState.Completed);
    }

    #endregion

    #region Idempotency Tests

    [Fact]
    public async Task MultipleWaitAllCalls_CoordinatorCompletedRaisedOnce()
    {
        // arrange
        var completedCount = 0;
        var signal = new CountingSignal("test");
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => Interlocked.Increment(ref completedCount);
        signal.Complete();

        // act
        await coord.WaitAllAsync();
        await coord.WaitAllAsync();
        await coord.WaitAllAsync();

        // assert
        completedCount.Should().Be(1);
    }

    [Fact]
    public async Task MultipleWaitAllCalls_StateRemainsTerminal()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var stateAfterFirst = coord.State;
        await coord.WaitAllAsync();
        var stateAfterSecond = coord.State;

        // assert
        stateAfterFirst.Should().Be(IgnitionState.Completed);
        stateAfterSecond.Should().Be(IgnitionState.Completed);
    }

    #endregion

    #region FailFast Event Tests

    [Fact]
    public async Task FailFast_Sequential_RaisesCoordinatorCompletedBeforeThrowing()
    {
        // arrange
        IgnitionState? finalState = null;
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var coord = CreateCoordinator([failing], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.FailFast;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => finalState = e.FinalState;

        // act
        try { await coord.WaitAllAsync(); } catch (AggregateException) { }

        // assert
        finalState.Should().Be(IgnitionState.Failed);
        coord.State.Should().Be(IgnitionState.Failed);
    }

    [Fact]
    public async Task FailFast_Parallel_RaisesCoordinatorCompletedBeforeThrowing()
    {
        // arrange
        IgnitionState? finalState = null;
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var coord = CreateCoordinator([failing], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Policy = IgnitionPolicy.FailFast;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        coord.CoordinatorCompleted += (sender, e) => finalState = e.FinalState;

        // act
        try { await coord.WaitAllAsync(); } catch (AggregateException) { }

        // assert
        finalState.Should().Be(IgnitionState.Failed);
        coord.State.Should().Be(IgnitionState.Failed);
    }

    #endregion

    #region GlobalTimeout Sequential Tests

    [Fact]
    public async Task GlobalTimeoutReached_Sequential_IncludesNotYetStartedSignals()
    {
        // arrange
        IReadOnlyList<string>? pendingSignals = null;
        var fast = new FakeSignal("fast", _ => Task.Delay(10));
        var slow = new FakeSignal("slow", async ct => await Task.Delay(500, ct));
        var never = new FakeSignal("never", _ => Task.CompletedTask);

        var coord = CreateCoordinator([fast, slow, never], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.GlobalTimeout = TimeSpan.FromMilliseconds(100);
            o.CancelOnGlobalTimeout = true;
        });

        coord.GlobalTimeoutReached += (sender, e) => pendingSignals = e.PendingSignals;

        // act
        await coord.WaitAllAsync();

        // assert
        pendingSignals.Should().NotBeNull();
        pendingSignals.Should().Contain("slow");
        pendingSignals.Should().Contain("never"); // This signal never started
    }

    #endregion

    #region Parallel Timeout Event Ordering Tests

    [Fact]
    public async Task Events_ParallelHardTimeout_CoordinatorCompletedAfterTimeout()
    {
        // arrange
        var coordinatorCompleted = false;
        var timeoutReached = false;
        var slow1 = new FakeSignal("slow1", async ct => await Task.Delay(500, ct));
        var slow2 = new FakeSignal("slow2", async ct => await Task.Delay(500, ct));

        var coord = CreateCoordinator([slow1, slow2], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromMilliseconds(100);
            o.CancelOnGlobalTimeout = true;
        });

        coord.GlobalTimeoutReached += (sender, e) => timeoutReached = true;
        coord.CoordinatorCompleted += (sender, e) =>
        {
            coordinatorCompleted = true;
            e.FinalState.Should().Be(IgnitionState.TimedOut);
        };

        // act
        await coord.WaitAllAsync();

        // assert
        timeoutReached.Should().BeTrue();
        coordinatorCompleted.Should().BeTrue();
        coord.State.Should().Be(IgnitionState.TimedOut);
    }

    #endregion
}
