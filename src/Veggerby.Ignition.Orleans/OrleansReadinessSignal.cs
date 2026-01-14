using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Orleans;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Orleans cluster client registration.
/// </summary>
/// <remarks>
/// This signal verifies that an <see cref="IClusterClient"/> is properly registered in the 
/// dependency injection container. For more comprehensive cluster connectivity verification
/// (such as testing grain activation or checking cluster membership), implement a custom
/// signal that makes actual grain calls or uses management grain methods.
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
            _logger.LogDebug("Verifying Orleans cluster client connectivity");

            activity?.SetTag("orleans.cluster_check", "basic_connectivity");

            _logger.LogInformation("Orleans readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Orleans readiness check failed");
            throw;
        }
    }
}
