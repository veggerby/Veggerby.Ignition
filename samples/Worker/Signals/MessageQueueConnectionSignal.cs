using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Worker.Signals;

/// <summary>
/// Signal representing message queue connection readiness.
/// Simulates establishing a connection to a message broker (e.g., RabbitMQ, Azure Service Bus, Kafka).
/// </summary>
public sealed class MessageQueueConnectionSignal : IIgnitionSignal
{
    private readonly ILogger<MessageQueueConnectionSignal> _logger;

    public string Name => "message-queue-connection";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public MessageQueueConnectionSignal(ILogger<MessageQueueConnectionSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to message queue...");

        // Simulate connection establishment
        await Task.Delay(1500, cancellationToken);

        // Simulate connection health check
        await Task.Delay(200, cancellationToken);

        _logger.LogInformation("Message queue connection established successfully");
    }
}
