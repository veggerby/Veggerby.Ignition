using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Veggerby.Ignition.RabbitMq;

namespace Veggerby.Ignition.RabbitMq.Tests;

public class RabbitMqIntegrationTests : IAsyncLifetime
{
    private RabbitMqContainer? _rabbitMqContainer;
    private IConnectionFactory? _connectionFactory;

    public async Task InitializeAsync()
    {
        _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:4.0-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _rabbitMqContainer.StartAsync();

        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_rabbitMqContainer.GetConnectionString())
        };
    }

    public async Task DisposeAsync()
    {
        if (_rabbitMqContainer != null)
        {
            await _rabbitMqContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(_connectionFactory!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChannelCreation_Succeeds()
    {
        // arrange
        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(_connectionFactory!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task QueueVerification_WithTestQueue_Succeeds()
    {
        // arrange
        // First verify RabbitMQ is ready, then create a test queue
        var warmupSignal = new RabbitMqReadinessSignal(
            _connectionFactory!, 
            new RabbitMqReadinessOptions { Timeout = TimeSpan.FromSeconds(30) },
            Substitute.For<ILogger<RabbitMqReadinessSignal>>());
        await warmupSignal.WaitAsync();
        
        using var connection = await _connectionFactory!.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var queueName = "integration_test_queue";
        await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: true);

        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyQueues.Add(queueName);
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(_connectionFactory!, options, logger);

        // act
        await signal.WaitAsync();

        // assert - queue should exist
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExchangeVerification_WithTestExchange_Succeeds()
    {
        // arrange
        // First verify RabbitMQ is ready, then create a test exchange
        var warmupSignal = new RabbitMqReadinessSignal(
            _connectionFactory!,
            new RabbitMqReadinessOptions { Timeout = TimeSpan.FromSeconds(30) },
            Substitute.For<ILogger<RabbitMqReadinessSignal>>());
        await warmupSignal.WaitAsync();
        
        using var connection = await _connectionFactory!.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        var exchangeName = "integration_test_exchange";
        await channel.ExchangeDeclareAsync(exchangeName, "fanout", durable: false, autoDelete: true);

        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyExchanges.Add(exchangeName);
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(_connectionFactory!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RoundTripTest_Succeeds()
    {
        // arrange
        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30),
            PerformRoundTripTest = true,
            RoundTripTestTimeout = TimeSpan.FromSeconds(5)
        };
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(_connectionFactory!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(_connectionFactory!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }
}
