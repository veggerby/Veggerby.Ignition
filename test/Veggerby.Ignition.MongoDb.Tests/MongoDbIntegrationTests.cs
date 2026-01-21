using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Veggerby.Ignition.MongoDb;

namespace Veggerby.Ignition.MongoDb.Tests;

public class MongoDbIntegrationTests : IAsyncLifetime
{
    private MongoDbContainer? _mongoDbContainer;
    private IMongoClient? _mongoClient;

    public async Task InitializeAsync()
    {
        _mongoDbContainer = new MongoDbBuilder()
            .WithImage("mongo:8")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _mongoDbContainer.StartAsync();

        _mongoClient = new MongoClient(_mongoDbContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_mongoDbContainer is not null)
        {
            await _mongoDbContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(_mongoClient!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DatabaseVerification_Succeeds()
    {
        // arrange
        var databaseName = "test_db";
        
        // Create the database
        var database = _mongoClient!.GetDatabase(databaseName);
        await database.CreateCollectionAsync("test_collection");

        var options = new MongoDbReadinessOptions
        {
            DatabaseName = databaseName
        };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(_mongoClient!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CollectionVerification_Succeeds()
    {
        // arrange
        var databaseName = "test_db";
        var collectionName = "test_collection";
        
        // Create the collection
        var database = _mongoClient!.GetDatabase(databaseName);
        await database.CreateCollectionAsync(collectionName);

        var options = new MongoDbReadinessOptions
        {
            DatabaseName = databaseName,
            VerifyCollection = collectionName
        };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(_mongoClient!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CollectionVerification_NonExistentCollection_ThrowsException()
    {
        // arrange
        var databaseName = "test_db";
        var collectionName = "nonexistent_collection";
        
        var options = new MongoDbReadinessOptions
        {
            DatabaseName = databaseName,
            VerifyCollection = collectionName,
            Timeout = TimeSpan.FromSeconds(5)
        };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(_mongoClient!, options, logger);

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new MongoDbReadinessOptions();
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(_mongoClient!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InvalidConnectionString_ThrowsException()
    {
        // arrange
        var invalidClient = new MongoClient("mongodb://invalid-host:27017");
        var options = new MongoDbReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var logger = Substitute.For<ILogger<MongoDbReadinessSignal>>();
        var signal = new MongoDbReadinessSignal(invalidClient, options, logger);

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await signal.WaitAsync());
    }
}
