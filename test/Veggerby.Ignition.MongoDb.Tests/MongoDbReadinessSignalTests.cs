using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Veggerby.Ignition.MongoDb;

namespace Veggerby.Ignition.MongoDb.Tests;

public class MongoDbReadinessSignalTests
{
    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbReadinessSignal(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var options = new MongoDbReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbReadinessSignal(client, options, null!));
    }

    [Fact]
    public void Constructor_CollectionWithoutDatabase_ThrowsInvalidOperationException()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var options = new MongoDbReadinessOptions
        {
            VerifyCollection = "users"
        };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();

        // act & assert
        Assert.Throws<InvalidOperationException>(() => new MongoDbReadinessSignal(client, options, logger));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);

        // act & assert
        signal.Name.Should().Be("mongodb-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var timeout = TimeSpan.FromSeconds(10);
        var options = new MongoDbReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var options = new MongoDbReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }
}
