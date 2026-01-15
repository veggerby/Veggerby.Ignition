using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Orleans;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Orleans cluster client connectivity.
/// </summary>
/// <remarks>
/// This signal verifies Orleans cluster connectivity by checking active silos via the management grain.
/// The check retrieves the list of active silos to ensure the cluster is accessible and operational.
/// </remarks>
internal sealed class OrleansReadinessSignal : IIgnitionSignal
{
    private readonly IClusterClient _clusterClient;
    private readonly OrleansReadinessOptions _options;
    private readonly ILogger<OrleansReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrleansReadinessSignal"/> class.
    /// </summary>
    /// <param name="clusterClient">Orleans cluster client to verify.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public OrleansReadinessSignal(
        IClusterClient clusterClient,
        OrleansReadinessOptions options,
        ILogger<OrleansReadinessSignal> logger)
    {
        _clusterClient = clusterClient ?? throw new ArgumentNullException(nameof(clusterClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "orleans-readiness";

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

        _logger.LogInformation("Orleans cluster readiness check starting");

        try
        {
            var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

            _logger.LogDebug("Verifying Orleans cluster client connectivity");

            activity?.SetTag("orleans.cluster_check", "management_grain");

            // Verify actual cluster connectivity by accessing the management grain
            await retryPolicy.ExecuteAsync(async ct =>
            {
                var managementGrain = _clusterClient.GetGrain<IManagementGrain>(0);
                var hosts = await managementGrain.GetHosts(onlyActive: true);
                
                if (hosts == null || hosts.Count == 0)
                {
                    throw new InvalidOperationException("No active silos found in Orleans cluster");
                }
                
                _logger.LogDebug("Orleans cluster has {ActiveSilos} active silo(s)", hosts.Count);
            }, "Orleans cluster connection", cancellationToken);

            _logger.LogInformation("Orleans readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Orleans readiness check failed");
            throw;
        }
    }
}
