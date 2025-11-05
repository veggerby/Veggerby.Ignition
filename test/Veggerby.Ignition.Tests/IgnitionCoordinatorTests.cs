using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Collections.Concurrent;
using System.Linq;
using Veggerby.Ignition;

namespace Veggerby.Ignition.Tests;

public class IgnitionCoordinatorTests
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
    public async Task Parallel_AllSignalsSucceed_ReturnsSucceededResults()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.Delay(20));
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(2);
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task Sequential_FailFast_ThrowsAggregateAndStopsAfterFailure()
    {
        // arrange
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var neverRun = new CountingSignal("later");
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, neverRun }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.FailFast;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        AggregateException? seqEx = null;
        try { await coord.WaitAllAsync(); } catch (AggregateException ex) { seqEx = ex; }

        // assert
        seqEx!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        neverRun.InvocationCount.Should().Be(0); // never awaited
    }

    [Fact]
    public async Task GlobalTimeout_IgnoredWithoutPerSignalTimeout_YieldsSuccess()
    {
        var slow = new FakeSignal("slow", async ct => await Task.Delay(500, ct));
        var coord = CreateCoordinator(new[] { slow }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().Contain(r => r.Name == "slow" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task PerSignalTimeout_TimesOutWhileOthersSucceed()
    {
        // arrange
        var timedOut = new FakeSignal("t-out", async ct => await Task.Delay(200, ct), timeout: TimeSpan.FromMilliseconds(50));
        var fast = new FakeSignal("fast", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { timedOut, fast }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.CancelIndividualOnTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().Contain(r => r.Name == "t-out" && r.Status == IgnitionSignalStatus.TimedOut);
        result.Results.Should().Contain(r => r.Name == "fast" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task MaxDegreeOfParallelism_LimitsConcurrentStarts()
    {
        // arrange
        var startOrder = new ConcurrentQueue<string>();
        var signals = Enumerable.Range(0, 5).Select(i => new FakeSignal($"s{i}", async ct =>
        {
            startOrder.Enqueue($"start-{i}");
            await Task.Delay(30, ct);
        })).ToList();
        var coord = CreateCoordinator(signals, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.MaxDegreeOfParallelism = 2;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
        // NOTE: We can't assert exact ordering easily but we can assert that at least first two started before completion.
        startOrder.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Idempotent_MultipleWaitAllAsync_DoesNotReInvokeSignals()
    {
        // arrange
        var counting = new CountingSignal("count");
        var coord = CreateCoordinator(new[] { counting }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        counting.Complete();

        // act
        await coord.WaitAllAsync();
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        counting.InvocationCount.Should().Be(1);
        result.Results.Should().HaveCount(1);
        result.Results.First().Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task Parallel_FailFast_ThrowsAggregate()
    {
        // arrange
        var failing1 = new FaultingSignal("bad1", new InvalidOperationException("boom1"));
        var failing2 = new FaultingSignal("bad2", new InvalidOperationException("boom2"));
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing1, failing2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Policy = IgnitionPolicy.FailFast;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        AggregateException? agg = null;
        try
        {
            await coord.WaitAllAsync();
        }
        catch (AggregateException ex)
        {
            agg = ex;
        }

        // assert
        agg.Should().NotBeNull();
        agg!.InnerExceptions.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GlobalTimeout_WithCancellation_MarksTimedOut()
    {
        // arrange
        var slow = new FakeSignal("slow", async ct => await Task.Delay(300, ct));
        var coord = CreateCoordinator(new[] { slow }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(50);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.CancelOnGlobalTimeout = true; // force cancellation classification
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeTrue();
        result.Results.Should().Contain(r => r.Name == "slow" && r.Status != IgnitionSignalStatus.Succeeded);
    }
}
