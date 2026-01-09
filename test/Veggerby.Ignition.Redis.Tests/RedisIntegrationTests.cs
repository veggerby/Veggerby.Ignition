using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using Veggerby.Ignition.Redis;

namespace Veggerby.Ignition.Redis.Tests;

public class RedisIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _connectionMultiplexer;

    public async Task InitializeAsync()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();

        _connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (_connectionMultiplexer != null)
        {
            await _connectionMultiplexer.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.ConnectionOnly
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(_connectionMultiplexer!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    public async Task Ping_Succeeds()
    {
        // arrange
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.Ping
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(_connectionMultiplexer!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    public async Task PingAndTestKey_Succeeds()
    {
        // arrange
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.PingAndTestKey,
            TestKeyPrefix = "integration:test:"
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(_connectionMultiplexer!, options, logger);

        // act
        await signal.WaitAsync();

        // assert - verify test key was cleaned up
        var db = _connectionMultiplexer!.GetDatabase();
        var keys = await GetKeysAsync(db, "integration:test:*");
        keys.Should().BeEmpty();
    }

    [Fact]
    public async Task PingAndTestKey_TestKeyCleanedUp_EvenOnRepeatedCalls()
    {
        // arrange
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.PingAndTestKey,
            TestKeyPrefix = "cleanup:test:"
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(_connectionMultiplexer!, options, logger);

        // act - call multiple times (should be idempotent, only execute once)
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert
        var db = _connectionMultiplexer!.GetDatabase();
        var keys = await GetKeysAsync(db, "cleanup:test:*");
        keys.Should().BeEmpty();
    }

    [Fact]
    public async Task DI_ConnectionString_CreatesSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddRedisReadiness(_redisContainer!.GetConnectionString(), options =>
        {
            options.VerificationStrategy = RedisVerificationStrategy.Ping;
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.TimedOut.Should().BeFalse();
        result.Results.Should().ContainSingle(r => r.Name == "redis-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task DI_ExistingMultiplexer_CreatesSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddSingleton<IConnectionMultiplexer>(_connectionMultiplexer!);
        services.AddRedisReadiness(options =>
        {
            options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.TimedOut.Should().BeFalse();
        result.Results.Should().ContainSingle(r => r.Name == "redis-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    private static async Task<List<RedisKey>> GetKeysAsync(IDatabase db, string pattern)
    {
        var server = db.Multiplexer.GetServer(db.Multiplexer.GetEndPoints().First());
        var keys = new List<RedisKey>();

        await foreach (var key in server.KeysAsync(pattern: pattern))
        {
            keys.Add(key);
        }

        return keys;
    }
}
