using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;
using Veggerby.Ignition.Redis;

namespace Veggerby.Ignition.Redis.Tests;

public class RedisIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _redisContainer.StartAsync();

        var configOptions = ConfigurationOptions.Parse(_redisContainer.GetConnectionString());
        configOptions.AbortOnConnectFail = false;
        configOptions.ConnectTimeout = 1000;
        _connectionString = configOptions.ToString();
    }

    public async Task DisposeAsync()
    {
        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString!);
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.ConnectionOnly,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(connectionMultiplexer, options, logger);

        try
        {
            // act & assert
            await signal.WaitAsync();
        }
        finally
        {
            await connectionMultiplexer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Ping_Succeeds()
    {
        // arrange
        var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString!);
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.Ping,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(connectionMultiplexer, options, logger);

        try
        {
            // act & assert
            await signal.WaitAsync();
        }
        finally
        {
            await connectionMultiplexer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PingAndTestKey_Succeeds()
    {
        // arrange
        var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString!);
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.PingAndTestKey,
            TestKeyPrefix = "integration:test:",
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(connectionMultiplexer, options, logger);

        try
        {
            // act
            await signal.WaitAsync();

            // assert - verify test key was cleaned up
            var db = connectionMultiplexer.GetDatabase();
            var keys = await GetKeysAsync(db, "integration:test:*");
            keys.Should().BeEmpty();
        }
        finally
        {
            await connectionMultiplexer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PingAndTestKey_TestKeyCleanedUp_EvenOnRepeatedCalls()
    {
        // arrange
        var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(_connectionString!);
        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.PingAndTestKey,
            TestKeyPrefix = "cleanup:test:",
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(connectionMultiplexer, options, logger);

        try
        {
            // act - call multiple times (should be idempotent, only execute once)
            await signal.WaitAsync();
            await signal.WaitAsync();
            await signal.WaitAsync();

            // assert
            var db = connectionMultiplexer.GetDatabase();
            var keys = await GetKeysAsync(db, "cleanup:test:*");
            keys.Should().BeEmpty();
        }
        finally
        {
            await connectionMultiplexer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DI_ConnectionString_CreatesSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddRedisReadiness(_connectionString!, options =>
        {
            options.VerificationStrategy = RedisVerificationStrategy.Ping;
            options.MaxRetries = 10;
            options.RetryDelay = TimeSpan.FromMilliseconds(500);
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
    [Trait("Category", "Integration")]
    public async Task DI_ExistingMultiplexer_CreatesSignal()
    {
        // arrange
        var configOptions = ConfigurationOptions.Parse(_connectionString!);
        configOptions.AbortOnConnectFail = false;
        var connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(configOptions);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition();
        services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);
        services.AddRedisReadiness(options =>
        {
            options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
            options.MaxRetries = 10;
            options.RetryDelay = TimeSpan.FromMilliseconds(500);
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            // act
            await coordinator.WaitAllAsync();

            // assert
            var result = await coordinator.GetResultAsync();
            result.TimedOut.Should().BeFalse();
            result.Results.Should().ContainSingle(r => r.Name == "redis-readiness" && r.Status == IgnitionSignalStatus.Succeeded);
        }
        finally
        {
            await connectionMultiplexer.DisposeAsync();
        }
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
