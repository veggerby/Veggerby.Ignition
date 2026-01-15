using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class CustomPolicyTests
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
    public async Task CustomPolicy_StopsOnFirstFailure_Sequential()
    {
        // arrange
        var policy = new TestPolicy(shouldContinue: false);
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var neverRun = new CountingSignal("later");
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, neverRun }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        AggregateException? ex = null;
        try { await coord.WaitAllAsync(); } catch (AggregateException e) { ex = e; }

        // assert
        ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        neverRun.InvocationCount.Should().Be(0);
        policy.InvocationCount.Should().Be(1);
        policy.LastContext.Should().NotBeNull();
        policy.LastContext!.SignalResult.Status.Should().Be(IgnitionSignalStatus.Failed);
    }

    [Fact]
    public async Task CustomPolicy_ContinuesOnFailure_Sequential()
    {
        // arrange
        var policy = new TestPolicy(shouldContinue: true);
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var success = new FakeSignal("good", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, success }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "bad" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "good" && r.Status == IgnitionSignalStatus.Succeeded);
        policy.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task CustomPolicy_StopsOnFirstFailure_Parallel()
    {
        // arrange
        var policy = new TestPolicy(shouldContinue: false);
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var success = new FakeSignal("good", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, success }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        AggregateException? ex = null;
        try { await coord.WaitAllAsync(); } catch (AggregateException e) { ex = e; }

        // assert
        ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        policy.InvocationCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CustomPolicy_ContextPopulatedCorrectly()
    {
        // arrange
        var policy = new TestPolicy(shouldContinue: true);
        var s1 = new FakeSignal("first", _ => Task.CompletedTask);
        var s2 = new FakeSignal("second", _ => Task.Delay(10));
        var s3 = new FakeSignal("third", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new IIgnitionSignal[] { s1, s2, s3 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
        });

        // act
        await coord.WaitAllAsync();

        // assert
        policy.InvocationCount.Should().Be(3);
        var lastContext = policy.LastContext!;
        lastContext.TotalSignalCount.Should().Be(3);
        lastContext.CompletedSignals.Should().HaveCount(3);
        lastContext.ExecutionMode.Should().Be(IgnitionExecutionMode.Sequential);
        lastContext.ElapsedTime.Should().BeGreaterThan(TimeSpan.Zero);
        lastContext.GlobalTimeoutElapsed.Should().BeFalse();
    }

    [Fact]
    public async Task FailFastPolicy_BackwardCompatibility_StopsOnFailure()
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
        AggregateException? ex = null;
        try { await coord.WaitAllAsync(); } catch (AggregateException e) { ex = e; }

        // assert
        ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        neverRun.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task BestEffortPolicy_BackwardCompatibility_ContinuesOnFailure()
    {
        // arrange
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var success = new FakeSignal("good", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, success }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "bad" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "good" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task ContinueOnTimeoutPolicy_BackwardCompatibility_StopsOnFailure()
    {
        // arrange
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var neverRun = new CountingSignal("later");
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, neverRun }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.ContinueOnTimeout;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        AggregateException? ex = null;
        try { await coord.WaitAllAsync(); } catch (AggregateException e) { ex = e; }

        // assert
        ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        neverRun.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task ContinueOnTimeoutPolicy_BackwardCompatibility_ContinuesOnTimeout()
    {
        // arrange
        var timedOut = new FakeSignal("t-out", async ct => await Task.Delay(100, ct), timeout: TimeSpan.FromMilliseconds(10));
        var success = new FakeSignal("good", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new IIgnitionSignal[] { timedOut, success }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.ContinueOnTimeout;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.CancelIndividualOnTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "t-out" && r.Status == IgnitionSignalStatus.TimedOut);
        result.Results.Should().Contain(r => r.Name == "good" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task CustomPolicy_PercentageThreshold_StopsWhen50PercentFail()
    {
        // arrange
        var policy = new PercentageThresholdPolicy(minimumSuccessRate: 0.5);
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FaultingSignal("s2", new InvalidOperationException("fail"));
        var s3 = new FaultingSignal("s3", new InvalidOperationException("fail"));
        var neverRun = new CountingSignal("s4");
        var coord = CreateCoordinator(new IIgnitionSignal[] { s1, s2, s3, neverRun }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        AggregateException? ex = null;
        try { await coord.WaitAllAsync(); } catch (AggregateException e) { ex = e; }

        // assert
        // After s3 fails, we have 1 success and 2 failures (33% success rate), so policy stops
        ex.Should().NotBeNull();
        ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
        // Note: Cannot call GetResultAsync() after an exception because the task is faulted
        neverRun.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task CustomPolicy_OverridesEnumPolicy()
    {
        // arrange
        var policy = new TestPolicy(shouldContinue: true);
        var failing = new FaultingSignal("bad", new InvalidOperationException("boom"));
        var success = new FakeSignal("good", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new IIgnitionSignal[] { failing, success }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.FailFast; // This should be ignored
            o.CustomPolicy = policy; // This should take precedence
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        // Custom policy allows continuation, so both signals complete
        result.Results.Should().HaveCount(2);
        policy.InvocationCount.Should().Be(2);
    }

    // Test helper: custom policy that tracks invocations
    private sealed class TestPolicy : IIgnitionPolicy
    {
        private readonly bool _shouldContinue;
        private int _invocationCount;
        private IgnitionPolicyContext? _lastContext;

        public TestPolicy(bool shouldContinue)
        {
            _shouldContinue = shouldContinue;
        }

        public int InvocationCount => _invocationCount;
        public IgnitionPolicyContext? LastContext => _lastContext;

        public bool ShouldContinue(IgnitionPolicyContext context)
        {
            Interlocked.Increment(ref _invocationCount);
            _lastContext = context;
            return _shouldContinue;
        }
    }

    // Test helper: percentage threshold policy
    private sealed class PercentageThresholdPolicy : IIgnitionPolicy
    {
        private readonly double _minimumSuccessRate;

        public PercentageThresholdPolicy(double minimumSuccessRate)
        {
            _minimumSuccessRate = minimumSuccessRate;
        }

        public bool ShouldContinue(IgnitionPolicyContext context)
        {
            var succeededCount = context.CompletedSignals.Count(s => s.Status == IgnitionSignalStatus.Succeeded);
            var completedCount = context.CompletedSignals.Count;

            if (completedCount == 0)
            {
                return true;
            }

            var successRate = (double)succeededCount / completedCount;
            return successRate >= _minimumSuccessRate;
        }
    }
}
