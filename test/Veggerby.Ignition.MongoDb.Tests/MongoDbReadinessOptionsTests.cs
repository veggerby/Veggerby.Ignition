namespace Veggerby.Ignition.MongoDb.Tests;

public class MongoDbReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new MongoDbReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new MongoDbReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void DatabaseName_DefaultsToNull()
    {
        // arrange & act
        var options = new MongoDbReadinessOptions();

        // assert
        options.DatabaseName.Should().BeNull();
    }

    [Fact]
    public void DatabaseName_CanBeSet()
    {
        // arrange
        var options = new MongoDbReadinessOptions();

        // act
        options.DatabaseName = "testdb";

        // assert
        options.DatabaseName.Should().Be("testdb");
    }

    [Fact]
    public void VerifyCollection_DefaultsToNull()
    {
        // arrange & act
        var options = new MongoDbReadinessOptions();

        // assert
        options.VerifyCollection.Should().BeNull();
    }

    [Fact]
    public void VerifyCollection_CanBeSet()
    {
        // arrange
        var options = new MongoDbReadinessOptions();

        // act
        options.VerifyCollection = "users";

        // assert
        options.VerifyCollection.Should().Be("users");
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new MongoDbReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new MongoDbReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new MongoDbReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new MongoDbReadinessOptions();
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
        var options = new MongoDbReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new MongoDbReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
