using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

using Veggerby.Ignition.RabbitMq;

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
}
