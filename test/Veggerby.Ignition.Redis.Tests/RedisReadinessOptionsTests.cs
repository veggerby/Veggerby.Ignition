namespace Veggerby.Ignition.Redis.Tests;

public class RedisReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void ConnectTimeout_DefaultsTo10Seconds()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.ConnectTimeout.Should().Be(10000); // milliseconds
    }

    [Fact]
    public void VerificationStrategy_DefaultsToConnectionOnly()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.VerificationStrategy.Should().Be(RedisVerificationStrategy.ConnectionOnly);
    }

    [Fact]
    public void VerificationStrategy_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();

        // act
        options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;

        // assert
        options.VerificationStrategy.Should().Be(RedisVerificationStrategy.PingAndTestKey);
    }

    [Fact]
    public void TestKeyPrefix_DefaultsToIgnitionReadiness()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.TestKeyPrefix.Should().Be("ignition:readiness:");
    }

    [Fact]
    public void TestKeyPrefix_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();

        // act
        options.TestKeyPrefix = "custom:prefix:";

        // assert
        options.TestKeyPrefix.Should().Be("custom:prefix:");
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void ConnectTimeout_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();

        // act
        options.ConnectTimeout = 10000;

        // assert
        options.ConnectTimeout.Should().Be(10000);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new RedisReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new RedisReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
