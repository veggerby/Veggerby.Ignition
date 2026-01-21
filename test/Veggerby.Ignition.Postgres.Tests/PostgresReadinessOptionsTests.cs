namespace Veggerby.Ignition.Postgres.Tests;

public class PostgresReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new PostgresReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void ValidationQuery_DefaultsToNull()
    {
        // arrange & act
        var options = new PostgresReadinessOptions();

        // assert
        options.ValidationQuery.Should().BeNull();
    }

    [Fact]
    public void ValidationQuery_CanBeSet()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act
        options.ValidationQuery = "SELECT 1";

        // assert
        options.ValidationQuery.Should().Be("SELECT 1");
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new PostgresReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new PostgresReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new PostgresReadinessOptions();
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
        var options = new PostgresReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
