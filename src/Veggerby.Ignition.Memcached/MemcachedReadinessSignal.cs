using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Enyim.Caching;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Memcached;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Memcached cache readiness.
/// Validates connection and optionally executes stats or test key operations.
/// </summary>
internal sealed class MemcachedReadinessSignal : IIgnitionSignal
{
    private readonly IMemcachedClient _memcachedClient;
    private readonly MemcachedReadinessOptions _options;
    private readonly ILogger<MemcachedReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemcachedReadinessSignal"/> class.
    /// </summary>
    /// <param name="memcachedClient">Memcached client instance.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MemcachedReadinessSignal(
        IMemcachedClient memcachedClient,
        MemcachedReadinessOptions options,
        ILogger<MemcachedReadinessSignal> logger)
    {
        _memcachedClient = memcachedClient ?? throw new ArgumentNullException(nameof(memcachedClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "memcached-readiness";

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

        activity?.SetTag("memcached.verification_strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "Memcached readiness check starting using strategy {Strategy}",
            _options.VerificationStrategy);

        try
        {
            _logger.LogDebug("Memcached client initialized");

            if (_options.VerificationStrategy == MemcachedVerificationStrategy.Stats)
            {
                await ExecuteStatsAsync(cancellationToken);
            }

            if (_options.VerificationStrategy == MemcachedVerificationStrategy.TestKey)
            {
                await ExecuteTestKeyRoundTripAsync(cancellationToken);
            }

            _logger.LogInformation("Memcached readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Memcached readiness check failed");
            throw;
        }
    }

    private Task ExecuteStatsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing Memcached stats command");

        var stats = _memcachedClient.Stats();
        if (stats == null)
        {
            throw new InvalidOperationException("Failed to retrieve Memcached stats");
        }

        _logger.LogDebug("Memcached stats retrieved successfully");
        return Task.CompletedTask;
    }

    private async Task ExecuteTestKeyRoundTripAsync(CancellationToken cancellationToken)
    {
        var testKey = $"{_options.TestKeyPrefix}{Guid.NewGuid():N}";
        var testValue = Guid.NewGuid().ToString("N");

        _logger.LogDebug("Executing Memcached test key round-trip for key {TestKey}", testKey);

        try
        {
            // Set with 60-second expiration
            var setResult = await _memcachedClient.SetAsync(testKey, testValue, 60).ConfigureAwait(false);
            if (!setResult)
            {
                throw new InvalidOperationException("Failed to set test key in Memcached");
            }

            // Get and verify
            var getValue = await _memcachedClient.GetAsync<string>(testKey);
            if (getValue.Value == null || getValue.Value != testValue)
            {
                throw new InvalidOperationException("Test key value mismatch in Memcached");
            }

            _logger.LogDebug("Memcached test key round-trip completed successfully");
        }
        finally
        {
            // Clean up test key
            await _memcachedClient.RemoveAsync(testKey).ConfigureAwait(false);
        }
    }
}
