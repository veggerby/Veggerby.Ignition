using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionTimeoutStrategyTests
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
    public void DefaultIgnitionTimeoutStrategy_ReturnsSignalTimeoutAndGlobalCancelSetting()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask, timeout: TimeSpan.FromSeconds(5));
        var options = new IgnitionOptions { CancelIndividualOnTimeout = true };
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;

        // act
        var (timeout, cancelImmediately) = strategy.GetTimeout(signal, options);

        // assert
        timeout.Should().Be(TimeSpan.FromSeconds(5));
        cancelImmediately.Should().BeTrue();
    }

    [Fact]
    public void DefaultIgnitionTimeoutStrategy_ReturnsNullTimeoutWhenSignalHasNoTimeout()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask); // no timeout
        var options = new IgnitionOptions { CancelIndividualOnTimeout = false };
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;

        // act
        var (timeout, cancelImmediately) = strategy.GetTimeout(signal, options);

        // assert
        timeout.Should().BeNull();
        cancelImmediately.Should().BeFalse();
    }

    [Fact]
    public void DefaultIgnitionTimeoutStrategy_IsSingleton()
    {
        // arrange & act
        var instance1 = DefaultIgnitionTimeoutStrategy.Instance;
        var instance2 = DefaultIgnitionTimeoutStrategy.Instance;

        // assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public async Task CustomTimeoutStrategy_AppliesCustomTimeout()
    {
        // arrange
        var signal = new FakeSignal("slow", async ct => await Task.Delay(200, ct), timeout: TimeSpan.FromSeconds(10));
        var customStrategy = new FixedTimeoutStrategy(TimeSpan.FromMilliseconds(50), cancelImmediately: true);
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(1);
        result.Results[0].Name.Should().Be("slow");
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task CustomTimeoutStrategy_OverridesSignalTimeout()
    {
        // arrange
        // Signal has short timeout but strategy overrides with longer timeout
        var signal = new FakeSignal("fast", async ct => await Task.Delay(30, ct), timeout: TimeSpan.FromMilliseconds(10));
        var customStrategy = new FixedTimeoutStrategy(TimeSpan.FromSeconds(1), cancelImmediately: false);
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task CustomTimeoutStrategy_CanDisableTimeout()
    {
        // arrange
        // Signal has timeout but strategy disables it
        var signal = new FakeSignal("no-timeout", async ct => await Task.Delay(30, ct), timeout: TimeSpan.FromMilliseconds(10));
        var customStrategy = new NoTimeoutStrategy();
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task CustomTimeoutStrategy_CancelImmediatelyControlsTaskCancellation()
    {
        // arrange
        var tracking = new TrackingService("tracking");
        var customStrategy = new FixedTimeoutStrategy(TimeSpan.FromMilliseconds(30), cancelImmediately: true);
        var coord = CreateCoordinator(new[] { tracking }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        tracking.CancellationObserved.Should().BeTrue();
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task CustomTimeoutStrategy_NoCancellation_TaskContinues()
    {
        // arrange
        // Use a signal that tracks whether cancellation was requested
        var tracking = new TrackingService("tracking");
        var customStrategy = new FixedTimeoutStrategy(TimeSpan.FromMilliseconds(30), cancelImmediately: false);
        var coord = CreateCoordinator(new[] { tracking }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        // When cancelImmediately is false, the task isn't cancelled via the per-handle CTS
        // The signal times out but cancellation is not explicitly triggered by strategy
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task CustomTimeoutStrategy_PerSignalBehavior()
    {
        // arrange
        var fast = new FakeSignal("fast", _ => Task.CompletedTask);
        var slow = new FakeSignal("slow", async ct => await Task.Delay(200, ct));
        var customStrategy = new ConditionalTimeoutStrategy(
            s => s.Name == "slow" ? TimeSpan.FromMilliseconds(50) : null,
            cancelImmediately: true);
        var coord = CreateCoordinator(new[] { fast, slow }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "fast" && r.Status == IgnitionSignalStatus.Succeeded);
        result.Results.Should().Contain(r => r.Name == "slow" && r.Status == IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task NoTimeoutStrategy_FallsBackToDefaultBehavior()
    {
        // arrange
        // No strategy configured - should use signal timeout and CancelIndividualOnTimeout
        var signal = new FakeSignal("timeout-signal", async ct => await Task.Delay(100, ct), timeout: TimeSpan.FromMilliseconds(30));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.CancelIndividualOnTimeout = true;
            // No TimeoutStrategy set
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task TimeoutStrategy_WorksWithSequentialMode()
    {
        // arrange
        var s1 = new FakeSignal("s1", async ct => await Task.Delay(100, ct));
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var customStrategy = new FixedTimeoutStrategy(TimeSpan.FromMilliseconds(30), cancelImmediately: true);
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Policy = IgnitionPolicy.BestEffort;
            o.TimeoutStrategy = customStrategy;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results[0].Name.Should().Be("s1");
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
        result.Results[1].Name.Should().Be("s2");
        result.Results[1].Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_RegistersStrategy()
    {
        // arrange
        var services = new ServiceCollection();
        var strategy = new FixedTimeoutStrategy(TimeSpan.FromSeconds(5), cancelImmediately: false);
        services.AddIgnition();
        services.AddIgnitionTimeoutStrategy(strategy);
        var provider = services.BuildServiceProvider();

        // act
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.TimeoutStrategy.Should().BeSameAs(strategy);
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_Generic_RegistersStrategyViaDI()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddIgnitionTimeoutStrategy<FixedTimeoutStrategy>();
        services.AddSingleton(new FixedTimeoutStrategyConfig(TimeSpan.FromSeconds(10), true));
        var provider = services.BuildServiceProvider();

        // act
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.TimeoutStrategy.Should().NotBeNull();
        options.TimeoutStrategy.Should().BeOfType<FixedTimeoutStrategy>();
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_Factory_RegistersStrategyViaFactory()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddIgnitionTimeoutStrategy(sp => new FixedTimeoutStrategy(TimeSpan.FromSeconds(7), false));
        var provider = services.BuildServiceProvider();

        // act
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        var (timeout, cancel) = options.TimeoutStrategy!.GetTimeout(
            new FakeSignal("test", _ => Task.CompletedTask),
            options);

        // assert
        options.TimeoutStrategy.Should().NotBeNull();
        timeout.Should().Be(TimeSpan.FromSeconds(7));
        cancel.Should().BeFalse();
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_ThrowsOnNullStrategy()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        var act = () => services.AddIgnitionTimeoutStrategy((IIgnitionTimeoutStrategy)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_Factory_ThrowsOnNullFactory()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        var act = () => services.AddIgnitionTimeoutStrategy((Func<IServiceProvider, IIgnitionTimeoutStrategy>)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // Test helper strategies

    private sealed class FixedTimeoutStrategy : IIgnitionTimeoutStrategy
    {
        private readonly TimeSpan? _timeout;
        private readonly bool _cancelImmediately;

        public FixedTimeoutStrategy(TimeSpan? timeout, bool cancelImmediately)
        {
            _timeout = timeout;
            _cancelImmediately = cancelImmediately;
        }

        // Constructor for DI registration
        public FixedTimeoutStrategy(FixedTimeoutStrategyConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _timeout = config.Timeout;
            _cancelImmediately = config.CancelImmediately;
        }

        public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
        {
            return (_timeout, _cancelImmediately);
        }
    }

    private sealed record FixedTimeoutStrategyConfig(TimeSpan? Timeout, bool CancelImmediately);

    private sealed class NoTimeoutStrategy : IIgnitionTimeoutStrategy
    {
        public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
        {
            return (null, false);
        }
    }

    private sealed class ConditionalTimeoutStrategy : IIgnitionTimeoutStrategy
    {
        private readonly Func<IIgnitionSignal, TimeSpan?> _timeoutSelector;
        private readonly bool _cancelImmediately;

        public ConditionalTimeoutStrategy(Func<IIgnitionSignal, TimeSpan?> timeoutSelector, bool cancelImmediately)
        {
            _timeoutSelector = timeoutSelector;
            _cancelImmediately = cancelImmediately;
        }

        public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
        {
            return (_timeoutSelector(signal), _cancelImmediately);
        }
    }
}
