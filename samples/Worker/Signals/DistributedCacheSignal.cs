using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Worker.Signals;

/// <summary>
/// Signal representing distributed cache readiness.
/// Ensures Redis or other distributed cache is accessible before worker processing begins.
/// </summary>
public sealed class DistributedCacheSignal : IIgnitionSignal
{
    private readonly ILogger<DistributedCacheSignal> _logger;

    public string Name => "distributed-cache";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public DistributedCacheSignal(ILogger<DistributedCacheSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to distributed cache (Redis)...");

        // Simulate cache connection
        await Task.Delay(800, cancellationToken);

        // Simulate ping test
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("Distributed cache connection successful");
    }
}
