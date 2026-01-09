using Microsoft.Extensions.Logging;
using MongoDB.Bson;
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

    [Fact]
    public async Task WaitAsync_SuccessfulPing_CompletesSuccessfully()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var database = Substitute.For<IMongoDatabase>();
        
        client.GetDatabase("admin").Returns(database);
        database.RunCommandAsync<BsonDocument>(Arg.Any<Command<BsonDocument>>(), Arg.Any<ReadPreference>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BsonDocument()));

        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await database.Received(1).RunCommandAsync<BsonDocument>(Arg.Any<Command<BsonDocument>>(), Arg.Any<ReadPreference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_PingFailure_ThrowsException()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var database = Substitute.For<IMongoDatabase>();
        
        client.GetDatabase("admin").Returns(database);
        database.RunCommandAsync<BsonDocument>(Arg.Any<Command<BsonDocument>>(), Arg.Any<ReadPreference>(), Arg.Any<CancellationToken>())
            .Returns<Task<BsonDocument>>(x => throw new MongoException("Connection failed"));

        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);

        // act & assert
        await Assert.ThrowsAsync<MongoException>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var database = Substitute.For<IMongoDatabase>();
        
        client.GetDatabase("admin").Returns(database);
        database.RunCommandAsync<BsonDocument>(Arg.Any<Command<BsonDocument>>(), Arg.Any<ReadPreference>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new BsonDocument()));

        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - ping called only once
        await database.Received(1).RunCommandAsync<BsonDocument>(Arg.Any<Command<BsonDocument>>(), Arg.Any<ReadPreference>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_WithCancellationToken_RespectsCancellation()
    {
        // arrange
        var client = Substitute.For<IMongoClient>();
        var database = Substitute.For<IMongoDatabase>();
        
        client.GetDatabase("admin").Returns(database);
        
        // Make the RunCommandAsync throw OperationCanceledException when cancellation token is used
        database.RunCommandAsync<BsonDocument>(Arg.Any<Command<BsonDocument>>(), Arg.Any<ReadPreference>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.ArgAt<CancellationToken>(2);
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new BsonDocument());
            });

        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(client, options, logger);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }
}
