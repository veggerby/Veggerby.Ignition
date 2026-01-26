namespace Veggerby.Ignition.RabbitMq.Tests;

public class RabbitMqReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void VerifyQueues_DefaultsToEmpty()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.VerifyQueues.Should().BeEmpty();
    }

    [Fact]
    public void VerifyQueues_CanBePopulated()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        options.VerifyQueues.Add("queue1");
        options.VerifyQueues.Add("queue2");

        // assert
        options.VerifyQueues.Should().Contain("queue1");
        options.VerifyQueues.Should().Contain("queue2");
    }

    [Fact]
    public void VerifyExchanges_DefaultsToEmpty()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.VerifyExchanges.Should().BeEmpty();
    }

    [Fact]
    public void VerifyExchanges_CanBePopulated()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        options.VerifyExchanges.Add("exchange1");
        options.VerifyExchanges.Add("exchange2");

        // assert
        options.VerifyExchanges.Should().Contain("exchange1");
        options.VerifyExchanges.Should().Contain("exchange2");
    }

    [Fact]
    public void FailOnMissingTopology_DefaultsToTrue()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.FailOnMissingTopology.Should().BeTrue();
    }

    [Fact]
    public void FailOnMissingTopology_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        options.FailOnMissingTopology = false;

        // assert
        options.FailOnMissingTopology.Should().BeFalse();
    }

    [Fact]
    public void PerformRoundTripTest_DefaultsToFalse()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.PerformRoundTripTest.Should().BeFalse();
    }

    [Fact]
    public void PerformRoundTripTest_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        options.PerformRoundTripTest = true;

        // assert
        options.PerformRoundTripTest.Should().BeTrue();
    }

    [Fact]
    public void RoundTripTestTimeout_DefaultsTo5Seconds()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.RoundTripTestTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RoundTripTestTimeout_CanBeSet()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var timeout = TimeSpan.FromSeconds(10);

        // act
        options.RoundTripTestTimeout = timeout;

        // assert
        options.RoundTripTestTimeout.Should().Be(timeout);
    }
}
