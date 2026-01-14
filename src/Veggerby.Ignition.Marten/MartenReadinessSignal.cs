using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Marten;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Marten;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Marten document store readiness.
/// Validates that the document store is accessible and ready.
/// </summary>
public sealed class MartenReadinessSignal : IIgnitionSignal
{
    private readonly IDocumentStore _documentStore;
    private readonly MartenReadinessOptions _options;
    private readonly ILogger<MartenReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MartenReadinessSignal"/> class.
    /// </summary>
    /// <param name="documentStore">Marten document store instance.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MartenReadinessSignal(
        IDocumentStore documentStore,
        MartenReadinessOptions options,
        ILogger<MartenReadinessSignal> logger)
    {
        _documentStore = documentStore ?? throw new ArgumentNullException(nameof(documentStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "marten-readiness";

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

        activity?.SetTag("marten.verify_store", _options.VerifyDocumentStore);

        _logger.LogInformation("Marten document store readiness check starting");

        try
        {
            if (_options.VerifyDocumentStore)
            {
                using var session = _documentStore.LightweightSession();
                
                // Perform a simple query to verify connectivity with a guaranteed database round-trip
                await session.QueryAsync<int>("SELECT 1", token: cancellationToken).ConfigureAwait(false);
                
                _logger.LogDebug("Marten document store connection verified");
            }

            _logger.LogInformation("Marten document store readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Marten document store readiness check failed");
            throw;
        }
    }
}
