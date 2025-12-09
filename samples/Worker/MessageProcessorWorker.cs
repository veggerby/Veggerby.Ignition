namespace Worker;

/// <summary>
/// Example background worker that processes messages from a queue.
/// Demonstrates the pattern of signaling readiness via TaskCompletionSource.
/// </summary>
public sealed class MessageProcessorWorker : BackgroundService
{
    private readonly ILogger<MessageProcessorWorker> _logger;
    private readonly TaskCompletionSource _readyTcs = new();

    public Task ReadyTask => _readyTcs.Task;

    public MessageProcessorWorker(ILogger<MessageProcessorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("MessageProcessorWorker: Initializing...");

            // Simulate initialization work (e.g., subscribing to queue, setting up handlers)
            await Task.Delay(500, stoppingToken);

            _logger.LogInformation("MessageProcessorWorker: Initialization complete, marking ready");

            // Signal that worker is ready
            _readyTcs.SetResult();

            _logger.LogInformation("MessageProcessorWorker: Starting message processing loop");

            // Main processing loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MessageProcessorWorker: Error processing messages");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation("MessageProcessorWorker: Stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MessageProcessorWorker: Fatal error during startup");
            _readyTcs.TrySetException(ex);
            throw;
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken stoppingToken)
    {
        // Simulate message processing
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        _logger.LogInformation("MessageProcessorWorker: Processed batch of messages");
    }
}
