namespace Veggerby.Ignition.Tests;

public class IgnitionEventArgsTests
{
    [Fact]
    public void IgnitionSignalStartedEventArgs_Constructor_SetsProperties()
    {
        // arrange
        var signalName = "test-signal";
        var timestamp = DateTimeOffset.UtcNow;

        // act
        var eventArgs = new IgnitionSignalStartedEventArgs(signalName, timestamp);

        // assert
        eventArgs.SignalName.Should().Be(signalName);
        eventArgs.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void IgnitionSignalStartedEventArgs_WithNullSignalName_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() => new IgnitionSignalStartedEventArgs(null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IgnitionSignalCompletedEventArgs_Constructor_SetsProperties()
    {
        // arrange
        var signalName = "test-signal";
        var status = IgnitionSignalStatus.Succeeded;
        var duration = TimeSpan.FromSeconds(2);
        var timestamp = DateTimeOffset.UtcNow;

        // act
        var eventArgs = new IgnitionSignalCompletedEventArgs(signalName, status, duration, timestamp);

        // assert
        eventArgs.SignalName.Should().Be(signalName);
        eventArgs.Status.Should().Be(status);
        eventArgs.Duration.Should().Be(duration);
        eventArgs.Timestamp.Should().Be(timestamp);
        eventArgs.Exception.Should().BeNull();
    }

    [Fact]
    public void IgnitionSignalCompletedEventArgs_WithException_SetsException()
    {
        // arrange
        var signalName = "test-signal";
        var status = IgnitionSignalStatus.Failed;
        var duration = TimeSpan.FromSeconds(1);
        var timestamp = DateTimeOffset.UtcNow;
        var exception = new InvalidOperationException("Test error");

        // act
        var eventArgs = new IgnitionSignalCompletedEventArgs(signalName, status, duration, timestamp, exception);

        // assert
        eventArgs.Exception.Should().BeSameAs(exception);
    }

    [Fact]
    public void IgnitionSignalCompletedEventArgs_WithNullSignalName_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new IgnitionSignalCompletedEventArgs(null!, IgnitionSignalStatus.Succeeded, TimeSpan.Zero, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IgnitionGlobalTimeoutEventArgs_Constructor_SetsProperties()
    {
        // arrange
        var globalTimeout = TimeSpan.FromSeconds(30);
        var elapsed = TimeSpan.FromSeconds(31);
        var timestamp = DateTimeOffset.UtcNow;
        var pendingSignals = new List<string> { "signal1", "signal2" };

        // act
        var eventArgs = new IgnitionGlobalTimeoutEventArgs(globalTimeout, elapsed, timestamp, pendingSignals);

        // assert
        eventArgs.GlobalTimeout.Should().Be(globalTimeout);
        eventArgs.Elapsed.Should().Be(elapsed);
        eventArgs.Timestamp.Should().Be(timestamp);
        eventArgs.PendingSignals.Should().BeEquivalentTo(pendingSignals);
    }

    [Fact]
    public void IgnitionGlobalTimeoutEventArgs_WithNullPendingSignals_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new IgnitionGlobalTimeoutEventArgs(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(31), DateTimeOffset.UtcNow, null!));
    }

    [Fact]
    public void IgnitionGlobalTimeoutEventArgs_WithEmptyPendingSignals_Succeeds()
    {
        // arrange
        var emptyList = new List<string>();

        // act
        var eventArgs = new IgnitionGlobalTimeoutEventArgs(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(31), DateTimeOffset.UtcNow, emptyList);

        // assert
        eventArgs.PendingSignals.Should().BeEmpty();
    }

    [Fact]
    public void IgnitionCoordinatorCompletedEventArgs_Constructor_SetsProperties()
    {
        // arrange
        var finalState = IgnitionState.Completed;
        var totalDuration = TimeSpan.FromSeconds(5);
        var timestamp = DateTimeOffset.UtcNow;
        var result = IgnitionResult.EmptySuccess;

        // act
        var eventArgs = new IgnitionCoordinatorCompletedEventArgs(finalState, totalDuration, timestamp, result);

        // assert
        eventArgs.FinalState.Should().Be(finalState);
        eventArgs.TotalDuration.Should().Be(totalDuration);
        eventArgs.Timestamp.Should().Be(timestamp);
        eventArgs.Result.Should().BeSameAs(result);
    }

    [Fact]
    public void IgnitionCoordinatorCompletedEventArgs_WithNullResult_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new IgnitionCoordinatorCompletedEventArgs(IgnitionState.Completed, TimeSpan.FromSeconds(5), DateTimeOffset.UtcNow, null!));
    }

    [Fact]
    public void EventArgs_InheritFromSystemEventArgs()
    {
        // act & assert
        typeof(IgnitionSignalStartedEventArgs).Should().BeDerivedFrom<EventArgs>();
        typeof(IgnitionSignalCompletedEventArgs).Should().BeDerivedFrom<EventArgs>();
        typeof(IgnitionGlobalTimeoutEventArgs).Should().BeDerivedFrom<EventArgs>();
        typeof(IgnitionCoordinatorCompletedEventArgs).Should().BeDerivedFrom<EventArgs>();
    }
}
