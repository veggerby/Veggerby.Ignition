using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Tests;

public class DefaultIgnitionTimeoutStrategyTests
{
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
        var options = new IgnitionOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => strategy.GetTimeout(null!, options));
    }

    [Fact]
    public void GetTimeout_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        // act & assert
        Assert.Throws<ArgumentNullException>(() => strategy.GetTimeout(signal, null!));
    }

    [Fact]
    public void GetTimeout_ReturnsSignalTimeout()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signalTimeout = TimeSpan.FromSeconds(5);
        var signal = new FakeSignal("test", _ => Task.CompletedTask, signalTimeout);
        var options = new IgnitionOptions();

        // act
        var (timeout, _) = strategy.GetTimeout(signal, options);

        // assert
        timeout.Should().Be(signalTimeout);
    }

    [Fact]
    public void GetTimeout_WhenSignalHasNoTimeout_ReturnsNull()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var options = new IgnitionOptions();

        // act
        var (timeout, _) = strategy.GetTimeout(signal, options);

        // assert
        timeout.Should().BeNull();
    }

    [Fact]
    public void GetTimeout_ReturnsCancelImmediatelyFromOptions()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var options = new IgnitionOptions
        {
            CancelIndividualOnTimeout = true
        };

        // act
        var (_, cancelImmediately) = strategy.GetTimeout(signal, options);

        // assert
        cancelImmediately.Should().BeTrue();
    }

    [Fact]
    public void GetTimeout_WhenCancelIndividualOnTimeoutFalse_ReturnsFalse()
    {
        // arrange
        var strategy = DefaultIgnitionTimeoutStrategy.Instance;
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var options = new IgnitionOptions
        {
            CancelIndividualOnTimeout = false
        };

        // act
        var (_, cancelImmediately) = strategy.GetTimeout(signal, options);

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
        var options = new IgnitionOptions();

        // act
        var (timeout1, _) = strategy.GetTimeout(signal1, options);
        var (timeout2, _) = strategy.GetTimeout(signal2, options);
        var (timeout3, _) = strategy.GetTimeout(signal3, options);

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
        var options = new IgnitionOptions
        {
            CancelIndividualOnTimeout = true
        };

        // act
        var result1 = strategy.GetTimeout(signal, options);
        var result2 = strategy.GetTimeout(signal, options);
        var result3 = strategy.GetTimeout(signal, options);

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
        var options = new IgnitionOptions();

        // act
        var (timeout, _) = strategy.GetTimeout(signal, options);

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
        var options = new IgnitionOptions();

        // act
        var (timeout, _) = strategy.GetTimeout(signal, options);

        // assert
        timeout.Should().Be(largeTimeout);
    }
}
