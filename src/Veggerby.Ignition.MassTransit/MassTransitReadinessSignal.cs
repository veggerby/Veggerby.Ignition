using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using global::MassTransit;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MassTransit;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying MassTransit bus readiness.
/// Leverages MassTransit's built-in health checks to ensure the bus is started and ready to process messages.
/// </summary>
internal sealed class MassTransitReadinessSignal : IIgnitionSignal
{
    private readonly IBus _bus;
    private readonly MassTransitReadinessOptions _options;
    private readonly ILogger<MassTransitReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MassTransitReadinessSignal"/> class.
    /// </summary>
    /// <param name="bus">The MassTransit bus instance to verify.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MassTransitReadinessSignal(
        IBus bus,
        MassTransitReadinessOptions options,
        ILogger<MassTransitReadinessSignal> logger)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "masstransit-readiness";

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
        activity?.SetTag("masstransit.bus", "ready-check");

        _logger.LogInformation("MassTransit bus readiness check starting");

        try
        {
            var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

            // Cast to IBusControl to access health check methods
            if (_bus is not IBusControl busControl)
            {
                throw new InvalidOperationException("The registered IBus instance does not implement IBusControl");
            }

            await retryPolicy.ExecuteAsync(async ct =>
            {
                var busHealth = busControl.CheckHealth();

                if (busHealth.Status == BusHealthStatus.Unhealthy)
                {
                    _logger.LogWarning("MassTransit bus is unhealthy: {Description}", busHealth.Description);
                    throw new InvalidOperationException($"MassTransit bus is unhealthy: {busHealth.Description}");
                }

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(_options.BusReadyTimeout);

                try
                {
                    await busControl.WaitForHealthStatus(BusHealthStatus.Healthy, timeoutCts.Token).ConfigureAwait(false);
                    _logger.LogDebug("MassTransit bus is healthy");
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new TimeoutException($"MassTransit bus did not become healthy within {_options.BusReadyTimeout}");
                }
            }, "MassTransit bus", cancellationToken);

            _logger.LogInformation("MassTransit bus readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MassTransit bus readiness check failed");
            throw;
        }
    }
}
