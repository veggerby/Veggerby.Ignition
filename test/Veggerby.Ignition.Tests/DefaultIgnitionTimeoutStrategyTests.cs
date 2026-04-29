using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Tests;

public class DefaultIgnitionTimeoutStrategyTests
{
    private static IgnitionTimeoutContext DefaultContext(
        bool cancelIndividualOnTimeout = false,
        TimeSpan? globalTimeout = null,
        TimeSpan? elapsed = null,
        int pendingCount = 1)
    {
        return new IgnitionTimeoutContext
        {
            GlobalTimeout = globalTimeout ?? TimeSpan.FromSeconds(30),
            CancelIndividualOnTimeout = cancelIndividualOnTimeout,
            ElapsedTime = elapsed ?? TimeSpan.Zero,
            PendingSignalCount = pendingCount
        };
    }

    [Fact]
    public void Instance_ReturnsSingletonInstance()
    {
        // act
        var instance1 = DefaultIgnitionTimeoutStrategy.Instance;
        var instance2 = DefaultIgnitionTimeoutStrategy.Instance;

        // assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void GetTimeout_WithNullSignal_ThrowsArgumentNullException()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;

        // act & assert
        Assert.Throws<ArgumentNullException>(() => strategy.GetTimeout(null!, DefaultContext()));
    }

    [Fact]
    public void GetTimeout_ReturnsSignalTimeout()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signalTimeout = TimeSpan.FromSeconds(5);
        var signal = new FakeSignal("test", _ => Task.CompletedTask, signalTimeout);

        // act
        var (timeout, _) = strategy.GetTimeout(signal, DefaultContext());

        // assert
        timeout.Should().Be(signalTimeout);
    }

    [Fact]
    public void GetTimeout_WhenSignalHasNoTimeout_ReturnsNull()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        // act
        var (timeout, _) = strategy.GetTimeout(signal, DefaultContext());

        // assert
        timeout.Should().BeNull();
    }

    [Fact]
    public void GetTimeout_ReturnsCancelImmediatelyFromContext()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        // act
        var (_, cancelImmediately) = strategy.GetTimeout(signal, DefaultContext(cancelIndividualOnTimeout: true));

        // assert
        cancelImmediately.Should().BeTrue();
    }

    [Fact]
    public void GetTimeout_WhenCancelIndividualOnTimeoutFalse_ReturnsFalse()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        // act
        var (_, cancelImmediately) = strategy.GetTimeout(signal, DefaultContext(cancelIndividualOnTimeout: false));

        // assert
        cancelImmediately.Should().BeFalse();
    }

    [Fact]
    public void GetTimeout_WithDifferentSignals_ReturnsCorrectTimeouts()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal1 = new FakeSignal("signal1", _ => Task.CompletedTask, TimeSpan.FromSeconds(3));
        var signal2 = new FakeSignal("signal2", _ => Task.CompletedTask, TimeSpan.FromSeconds(10));
        var signal3 = new FakeSignal("signal3", _ => Task.CompletedTask);
        var context = DefaultContext();

        // act
        var (timeout1, _) = strategy.GetTimeout(signal1, context);
        var (timeout2, _) = strategy.GetTimeout(signal2, context);
        var (timeout3, _) = strategy.GetTimeout(signal3, context);

        // assert
        timeout1.Should().Be(TimeSpan.FromSeconds(3));
        timeout2.Should().Be(TimeSpan.FromSeconds(10));
        timeout3.Should().BeNull();
    }

    [Fact]
    public void GetTimeout_CalledMultipleTimes_ReturnsConsistentResults()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signalTimeout = TimeSpan.FromSeconds(7);
        var signal = new FakeSignal("test", _ => Task.CompletedTask, signalTimeout);
        var context = DefaultContext(cancelIndividualOnTimeout: true);

        // act
        var result1 = strategy.GetTimeout(signal, context);
        var result2 = strategy.GetTimeout(signal, context);
        var result3 = strategy.GetTimeout(signal, context);

        // assert
        result1.Should().Be(result2);
        result2.Should().Be(result3);
    }

    [Fact]
    public void GetTimeout_WithZeroTimeout_ReturnsZero()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask, TimeSpan.Zero);

        // act
        var (timeout, _) = strategy.GetTimeout(signal, DefaultContext());

        // assert
        timeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetTimeout_WithVeryLargeTimeout_ReturnsCorrectValue()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var largeTimeout = TimeSpan.FromDays(365);
        var signal = new FakeSignal("test", _ => Task.CompletedTask, largeTimeout);

        // act
        var (timeout, _) = strategy.GetTimeout(signal, DefaultContext());

        // assert
        timeout.Should().Be(largeTimeout);
    }

    [Fact]
    public void IgnitionTimeoutContext_RemainingGlobalBudget_ComputedCorrectly()
    {
        // arrange
        var context = new IgnitionTimeoutContext
        {
            GlobalTimeout = TimeSpan.FromSeconds(30),
            ElapsedTime = TimeSpan.FromSeconds(10),
            CancelIndividualOnTimeout = false,
            PendingSignalCount = 2
        };

        // act & assert
        context.RemainingGlobalBudget.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void IgnitionTimeoutContext_RemainingGlobalBudget_NegativeWhenExpired()
    {
        // arrange
        var context = new IgnitionTimeoutContext
        {
            GlobalTimeout = TimeSpan.FromSeconds(10),
            ElapsedTime = TimeSpan.FromSeconds(15),
            CancelIndividualOnTimeout = false,
            PendingSignalCount = 1
        };

        // act & assert
        context.RemainingGlobalBudget.Should().BeLessThan(TimeSpan.Zero);
    }
}

