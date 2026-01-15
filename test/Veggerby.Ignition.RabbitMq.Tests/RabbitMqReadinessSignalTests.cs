using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;
using Veggerby.Ignition.RabbitMq;
using Xunit;

namespace Veggerby.Ignition.RabbitMq.Tests;

public class RabbitMqReadinessSignalTests
{
    [Fact]
    public async Task WaitAsync_SuccessfulConnection_CompletesSuccessfully()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions();
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await connectionFactory.Received(1).CreateConnectionAsync(Arg.Any<CancellationToken>());
        await connection.Received(1).CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns<IConnection>(x => throw new InvalidOperationException("Connection failed"));

        var options = new RabbitMqReadinessOptions();
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UseCachedResult()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions();
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - connection created only once
        await connectionFactory.Received(1).CreateConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var options = new RabbitMqReadinessOptions();
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act & assert
        Assert.Equal("rabbitmq-readiness", signal.Name);
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var options = new RabbitMqReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act & assert
        Assert.Equal(TimeSpan.FromSeconds(10), signal.Timeout);
    }

    [Fact]
    public async Task WaitAsync_QueueVerificationSuccess_CompletesSuccessfully()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        channel.QueueDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueueDeclareOk("test-queue", 0, 0)));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions();
        options.WithQueue("test-queue");
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await channel.Received(1).QueueDeclarePassiveAsync("test-queue", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_QueueNotFound_FailOnMissingTopologyTrue_ThrowsException()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        channel.QueueDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<QueueDeclareOk>(x => throw new InvalidOperationException("Queue not found"));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions { FailOnMissingTopology = true };
        options.WithQueue("missing-queue");
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_QueueNotFound_FailOnMissingTopologyFalse_LogsWarningAndContinues()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        channel.QueueDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<QueueDeclareOk>(x => throw new InvalidOperationException("Queue not found"));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions { FailOnMissingTopology = false };
        options.WithQueue("missing-queue");
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act
        await signal.WaitAsync();

        // assert - should complete without throwing
        await connectionFactory.Received(1).CreateConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_ExchangeVerificationSuccess_CompletesSuccessfully()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        channel.ExchangeDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions();
        options.WithExchange("test-exchange");
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await channel.Received(1).ExchangeDeclarePassiveAsync("test-exchange", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_ExchangeNotFound_FailOnMissingTopologyTrue_ThrowsException()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        channel.ExchangeDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new InvalidOperationException("Exchange not found"));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions { FailOnMissingTopology = true };
        options.WithExchange("missing-exchange");
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_ExchangeNotFound_FailOnMissingTopologyFalse_LogsWarningAndContinues()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var connection = Substitute.For<IConnection>();
        var channel = Substitute.For<IChannel>();

        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        connection.CreateChannelAsync(cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channel));

        channel.ExchangeDeclarePassiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(x => throw new InvalidOperationException("Exchange not found"));

        connection.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        channel.CloseAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var options = new RabbitMqReadinessOptions { FailOnMissingTopology = false };
        options.WithExchange("missing-exchange");
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();
        var signal = new RabbitMqReadinessSignal(connectionFactory, options, logger);

        // act
        await signal.WaitAsync();

        // assert - should complete without throwing
        await connectionFactory.Received(1).CreateConnectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RabbitMqReadinessOptions_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new RabbitMqReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        options.VerifyQueues.Should().BeEmpty();
        options.VerifyExchanges.Should().BeEmpty();
        options.FailOnMissingTopology.Should().BeTrue();
        options.PerformRoundTripTest.Should().BeFalse();
        options.RoundTripTestTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void RabbitMqReadinessOptions_WithQueue_AddsQueueToCollection()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        var result = options.WithQueue("queue1");

        // assert
        result.Should().BeSameAs(options);
        options.VerifyQueues.Should().Contain("queue1");
    }

    [Fact]
    public void RabbitMqReadinessOptions_WithExchange_AddsExchangeToCollection()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act
        var result = options.WithExchange("exchange1");

        // assert
        result.Should().BeSameAs(options);
        options.VerifyExchanges.Should().Contain("exchange1");
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_NullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RabbitMqReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "amqp://localhost";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RabbitMqReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var factory = new RabbitMqReadinessSignalFactory(_ => "amqp://localhost", options);

        // act & assert
        factory.Name.Should().Be("rabbitmq-readiness");
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(20);
        var options = new RabbitMqReadinessOptions { Timeout = timeout };
        var factory = new RabbitMqReadinessSignalFactory(_ => "amqp://localhost", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new RabbitMqReadinessOptions { Stage = 3 };
        var factory = new RabbitMqReadinessSignalFactory(_ => "amqp://localhost", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_CreateSignal_ReturnsSignalWithCorrectConnectionFactory()
    {
        // arrange
        var connectionString = "amqp://testhost:5672/vhost";
        var options = new RabbitMqReadinessOptions();
        var factory = new RabbitMqReadinessSignalFactory(_ => connectionString, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("rabbitmq-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_CreateSignal_ResolvesConnectionStringFromServiceProvider()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var expectedConnectionString = "amqp://dynamic-host";
        
        var factory = new RabbitMqReadinessSignalFactory(sp =>
        {
            // Simulate resolving from configuration
            return sp.GetService<string>() ?? expectedConnectionString;
        }, options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton("amqp://from-di");
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
    }

    [Fact]
    public void RabbitMqReadinessSignalFactory_MultipleCreateSignal_ReturnsNewInstances()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var factory = new RabbitMqReadinessSignalFactory(_ => "amqp://localhost", options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal1 = factory.CreateSignal(sp);
        var signal2 = factory.CreateSignal(sp);

        // assert
        signal1.Should().NotBeNull();
        signal2.Should().NotBeNull();
        signal1.Should().NotBeSameAs(signal2);
    }

    [Fact]
    public void Constructor_NullConnectionFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RabbitMqReadinessSignal((IConnectionFactory)null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var logger = Substitute.For<ILogger<RabbitMqReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RabbitMqReadinessSignal(connectionFactory, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var options = new RabbitMqReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RabbitMqReadinessSignal(connectionFactory, options, null!));
    }
}
