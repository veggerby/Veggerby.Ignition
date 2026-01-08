using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.RabbitMq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying RabbitMQ broker readiness.
/// Validates connection establishment and optionally verifies queue/exchange topology.
/// </summary>
public sealed class RabbitMqReadinessSignal : IIgnitionSignal
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqReadinessOptions _options;
    private readonly ILogger<RabbitMqReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqReadinessSignal"/> class.
    /// </summary>
    /// <param name="connectionFactory">RabbitMQ connection factory for creating connections.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RabbitMqReadinessSignal(
        IConnectionFactory connectionFactory,
        RabbitMqReadinessOptions options,
        ILogger<RabbitMqReadinessSignal> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "rabbitmq-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTask is null)
        {
            lock (_sync)
            {
                _cachedTask ??= ExecuteAsync(cancellationToken);
            }
        }

        return cancellationToken.CanBeCanceled && !_cachedTask.IsCompleted
            ? _cachedTask.WaitAsync(cancellationToken)
            : _cachedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        
        // Note: IConnectionFactory endpoint properties vary by implementation
        // Only log what we can safely access
        if (_connectionFactory is ConnectionFactory factory)
        {
            activity?.SetTag("rabbitmq.host", factory.HostName);
            activity?.SetTag("rabbitmq.port", factory.Port);
            activity?.SetTag("rabbitmq.virtualhost", factory.VirtualHost);

            _logger.LogInformation(
                "RabbitMQ readiness check starting for {Host}:{Port}/{VirtualHost}",
                factory.HostName,
                factory.Port,
                factory.VirtualHost);
        }
        else
        {
            _logger.LogInformation("RabbitMQ readiness check starting");
        }

        IConnection? connection = null;
        IChannel? channel = null;

        try
        {
            connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _logger.LogDebug("RabbitMQ connection established");

            channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
            _logger.LogDebug("RabbitMQ channel created");

            await VerifyTopologyAsync(channel, cancellationToken);

            if (_options.PerformRoundTripTest)
            {
                await PerformRoundTripTestAsync(channel, cancellationToken);
            }

            _logger.LogInformation("RabbitMQ readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "RabbitMQ readiness check failed");
            throw;
        }
        finally
        {
            if (channel is not null)
            {
                await channel.CloseAsync(CancellationToken.None);
                await channel.DisposeAsync();
            }

            if (connection is not null)
            {
                await connection.CloseAsync(CancellationToken.None);
                await connection.DisposeAsync();
            }
        }
    }

    private async Task VerifyTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        if (_options.VerifyQueues.Count > 0)
        {
            _logger.LogDebug("Verifying {Count} queues", _options.VerifyQueues.Count);

            foreach (var queueName in _options.VerifyQueues)
            {
                try
                {
                    await channel.QueueDeclarePassiveAsync(queueName, cancellationToken);
                    _logger.LogDebug("Queue '{QueueName}' verified", queueName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (_options.FailOnMissingTopology)
                    {
                        _logger.LogError(ex, "Queue '{QueueName}' verification failed", queueName);
                        throw;
                    }

                    _logger.LogWarning(ex, "Queue '{QueueName}' not found (continuing due to FailOnMissingTopology=false)", queueName);
                }
            }
        }

        if (_options.VerifyExchanges.Count > 0)
        {
            _logger.LogDebug("Verifying {Count} exchanges", _options.VerifyExchanges.Count);

            foreach (var exchangeName in _options.VerifyExchanges)
            {
                try
                {
                    await channel.ExchangeDeclarePassiveAsync(exchangeName, cancellationToken);
                    _logger.LogDebug("Exchange '{ExchangeName}' verified", exchangeName);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    if (_options.FailOnMissingTopology)
                    {
                        _logger.LogError(ex, "Exchange '{ExchangeName}' verification failed", exchangeName);
                        throw;
                    }

                    _logger.LogWarning(ex, "Exchange '{ExchangeName}' not found (continuing due to FailOnMissingTopology=false)", exchangeName);
                }
            }
        }
    }

    private async Task PerformRoundTripTestAsync(IChannel channel, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing round-trip test");

        var testQueue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            arguments: null,
            cancellationToken: cancellationToken);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var testMessage = Guid.NewGuid().ToString();
        var receivedMessage = string.Empty;

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (sender, args) =>
        {
            receivedMessage = Encoding.UTF8.GetString(args.Body.Span);
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        await channel.BasicConsumeAsync(
            queue: testQueue.QueueName,
            autoAck: true,
            consumer: consumer,
            cancellationToken: cancellationToken);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: testQueue.QueueName,
            body: Encoding.UTF8.GetBytes(testMessage),
            cancellationToken: cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.RoundTripTestTimeout);

        try
        {
            await tcs.Task.WaitAsync(timeoutCts.Token);

            if (receivedMessage != testMessage)
            {
                throw new InvalidOperationException($"Round-trip test failed: expected '{testMessage}', received '{receivedMessage}'");
            }

            _logger.LogDebug("Round-trip test completed successfully");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Round-trip test timed out after {_options.RoundTripTestTimeout}");
        }
    }
}
