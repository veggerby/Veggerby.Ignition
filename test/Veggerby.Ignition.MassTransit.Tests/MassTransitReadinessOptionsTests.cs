namespace Veggerby.Ignition.MassTransit.Tests;

public class MassTransitReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new MassTransitReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new MassTransitReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void BusReadyTimeout_DefaultsTo30Seconds()
    {
        // arrange & act
        var options = new MassTransitReadinessOptions();

        // assert
        options.BusReadyTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void BusReadyTimeout_CanBeSet()
    {
        // arrange
        var options = new MassTransitReadinessOptions();
        var timeout = TimeSpan.FromSeconds(60);

        // act
        options.BusReadyTimeout = timeout;

        // assert
        options.BusReadyTimeout.Should().Be(timeout);
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new MassTransitReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new MassTransitReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new MassTransitReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new MassTransitReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new MassTransitReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new MassTransitReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
