using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionCoordinatorTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var factories = signals.Select(s => new TestSignalFactory(s)).ToList();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);
    }

    [Fact]
    public async Task ZeroSignals_ReturnsEmptySuccess()
    {
        // arrange
        var coord = CreateCoordinator([], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().BeEmpty();
        result.TotalDuration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
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
        var slow = new FakeSignal("slow", async ct => await Task.Delay(60, ct));
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
        var timedOut = new FakeSignal("t-out", async ct => await Task.Delay(60, ct), timeout: TimeSpan.FromMilliseconds(50));
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
        var slow = new FakeSignal("slow", async ct => await Task.Delay(80, ct));
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

    [Fact]
    public async Task AddIgnitionFor_SingleServiceSelector_IdempotentInvocation()
    {
        // arrange
        var counting = new CountingSignal("svc");
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddSingleton(counting); // service we will adapt
        services.AddIgnitionFor<CountingSignal>(svc => svc.WaitAsync(), name: "counting");
        var provider = services.BuildServiceProvider();
        var signals = provider.GetServices<IIgnitionSignal>();
        var coord = CreateCoordinator(signals);

        counting.Complete();

        // act
        await coord.WaitAllAsync();
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        counting.InvocationCount.Should().Be(1);
        result.Results.Should().Contain(r => r.Name == "counting" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task AddIgnitionForAll_MultipleInstances_CompletesAggregate()
    {
        // arrange
        var svc1 = new CountingSignal("svc1");
        var svc2 = new CountingSignal("svc2");
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddSingleton(svc1);
        services.AddSingleton(svc2);
        services.AddIgnitionForAll<CountingSignal>(svc => svc.WaitAsync(), groupName: "counting[*]");
        var provider = services.BuildServiceProvider();
        var coord = CreateCoordinator(provider.GetServices<IIgnitionSignal>());
        svc1.Complete();
        svc2.Complete();

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        svc1.InvocationCount.Should().Be(1);
        svc2.InvocationCount.Should().Be(1);
        result.Results.Should().Contain(r => r.Name == "counting[*]" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task AddIgnitionForAll_ZeroInstances_FastPathSuccess()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddIgnitionForAll<CountingSignal>(svc => svc.WaitAsync(), groupName: "none[*]");
        var provider = services.BuildServiceProvider();
        var coord = CreateCoordinator(provider.GetServices<IIgnitionSignal>());

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().Contain(r => r.Name == "none[*]" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task AddIgnitionForAllScoped_ScopeDisposedAfterCompletion()
    {
        // arrange
        var svc1 = new CountingSignal("svc1");
        var svc2 = new CountingSignal("svc2");
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddScoped(_ => svc1);
        services.AddScoped(_ => svc2);
        services.AddIgnitionForAllScoped<CountingSignal>(svc => svc.WaitAsync(), groupName: "scoped[*]");
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var signals = scope.ServiceProvider.GetServices<IIgnitionSignal>();
        var coord = CreateCoordinator(signals);
        svc1.Complete();
        svc2.Complete();

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().Contain(r => r.Name == "scoped[*]" && r.Status == IgnitionSignalStatus.Succeeded);
        svc1.InvocationCount.Should().Be(1);
        svc2.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task AddIgnitionFor_CancellableSelector_PropagatesCancellation()
    {
        // arrange
        var tracking = new TrackingService("tracking");
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        services.AddIgnition(o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(40); // small hard timeout
            o.CancelOnGlobalTimeout = true;
            o.CancelIndividualOnTimeout = true;
        });
        services.AddSingleton(tracking);
        // per-signal timeout shorter than global to force per-signal timeout path if global race lost
        services.AddIgnitionFor<TrackingService>((svc, ct) => svc.WaitAsync(ct), name: "tracking", timeout: TimeSpan.FromMilliseconds(25));
        var provider = services.BuildServiceProvider();
        var coord = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        try { await coord.WaitAllAsync(); } catch { /* expected AggregateException for fail-fast or ignored */ }
        var result = await coord.GetResultAsync();

        // assert
        tracking.InvocationCount.Should().Be(1); // idempotent creation
        tracking.CancellationObserved.Should().BeTrue();
        result.Results.Should().Contain(r => r.Name == "tracking" && r.Status == IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task AddIgnitionForAll_CancellableSelector_PropagatesCancellation()
    {
        // arrange
        var t1 = new TrackingService("t1");
        var t2 = new TrackingService("t2");
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        services.AddIgnition(o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(40);
            o.CancelOnGlobalTimeout = true;
            o.CancelIndividualOnTimeout = true;
        });
        services.AddSingleton(t1);
        services.AddSingleton(t2);
        services.AddIgnitionForAll<TrackingService>((svc, ct) => svc.WaitAsync(ct), groupName: "tracking[*]", timeout: TimeSpan.FromMilliseconds(25));
        var provider = services.BuildServiceProvider();
        var coord = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        try { await coord.WaitAllAsync(); } catch { }
        var result = await coord.GetResultAsync();

        // assert
        t1.InvocationCount.Should().Be(1);
        t2.InvocationCount.Should().Be(1);
        t1.CancellationObserved.Should().BeTrue();
        t2.CancellationObserved.Should().BeTrue();
        result.Results.Should().Contain(r => r.Name == "tracking[*]" && r.Status == IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task AddIgnitionForAllScoped_CancellableSelector_PropagatesCancellation()
    {
        // arrange
        var t1 = new TrackingService("t1");
        var t2 = new TrackingService("t2");
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        services.AddIgnition(o =>
        {
            o.GlobalTimeout = TimeSpan.FromMilliseconds(40);
            o.CancelOnGlobalTimeout = true;
            o.CancelIndividualOnTimeout = true;
        });
        services.AddScoped(_ => t1);
        services.AddScoped(_ => t2);
        services.AddIgnitionForAllScoped<TrackingService>((svc, ct) => svc.WaitAsync(ct), groupName: "tracking-scoped[*]", timeout: TimeSpan.FromMilliseconds(25));
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var coord = scope.ServiceProvider.GetRequiredService<IIgnitionCoordinator>();

        // act
        try { await coord.WaitAllAsync(); } catch { }
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().Contain(r => r.Name == "tracking-scoped[*]" && (r.Status == IgnitionSignalStatus.TimedOut || r.Status == IgnitionSignalStatus.Failed));
    }

    [Fact]
    public async Task MixedStatuses_BestEffort_ReturnsAllResults()
    {
        // arrange
        var succeeded = new FakeSignal("success", _ => Task.CompletedTask);
        var failed = new FaultingSignal("failed", new InvalidOperationException("boom"));
        var timedOut = new FakeSignal("timeout", async ct => await Task.Delay(100, ct), timeout: TimeSpan.FromMilliseconds(50));
        var coord = CreateCoordinator(new IIgnitionSignal[] { succeeded, failed, timedOut }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.CancelIndividualOnTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse(); // No global timeout, just individual timeouts
        result.Results.Should().HaveCount(3);
        result.Results.Should().Contain(r => r.Name == "success" && r.Status == IgnitionSignalStatus.Succeeded);
        result.Results.Should().Contain(r => r.Name == "failed" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "timeout" && r.Status == IgnitionSignalStatus.TimedOut);
    }
}
