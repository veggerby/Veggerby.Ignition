using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Redis;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Redis cache readiness.
/// Validates connection and optionally executes PING or test key operations.
/// </summary>
public sealed class RedisReadinessSignal : IIgnitionSignal
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisReadinessOptions _options;
    private readonly ILogger<RedisReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisReadinessSignal"/> class
    /// using an existing <see cref="IConnectionMultiplexer"/>.
    /// </summary>
    /// <param name="connectionMultiplexer">Redis connection multiplexer.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RedisReadinessSignal(
        IConnectionMultiplexer connectionMultiplexer,
        RedisReadinessOptions options,
        ILogger<RedisReadinessSignal> logger)
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "redis-readiness";

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

        var endpoints = string.Join(", ", _connectionMultiplexer.GetEndPoints().Select(ep => ep.ToString()));
        activity?.SetTag("redis.endpoints", endpoints);
        activity?.SetTag("redis.verification_strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "Redis readiness check starting for endpoints {Endpoints} using strategy {Strategy}",
            endpoints,
            _options.VerificationStrategy);

        try
        {
            if (!_connectionMultiplexer.IsConnected)
            {
                throw new InvalidOperationException("Redis connection multiplexer is not connected");
            }

            _logger.LogDebug("Redis connection established");

            if (_options.VerificationStrategy == RedisVerificationStrategy.Ping ||
                _options.VerificationStrategy == RedisVerificationStrategy.PingAndTestKey)
            {
                await ExecutePingAsync(cancellationToken);
            }

            if (_options.VerificationStrategy == RedisVerificationStrategy.PingAndTestKey)
            {
                await ExecuteTestKeyRoundTripAsync(cancellationToken);
            }

            _logger.LogInformation("Redis readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Redis readiness check failed");
            throw;
        }
    }

    private async Task ExecutePingAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing Redis PING command");

        var db = _connectionMultiplexer.GetDatabase();
        var pingResult = await db.PingAsync();

        _logger.LogDebug("Redis PING completed in {Duration}ms", pingResult.TotalMilliseconds);
    }

    private async Task ExecuteTestKeyRoundTripAsync(CancellationToken cancellationToken)
    {
        var testKey = $"{_options.TestKeyPrefix}{Guid.NewGuid():N}";
        var testValue = Guid.NewGuid().ToString("N");

        _logger.LogDebug("Executing Redis test key round-trip for key {TestKey}", testKey);

        var db = _connectionMultiplexer.GetDatabase();

        try
        {
            // Set with 60-second TTL
            var setResult = await db.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(60));
            if (!setResult)
            {
                throw new InvalidOperationException("Failed to set test key in Redis");
            }

            // Get and verify
            var getValue = await db.StringGetAsync(testKey);
            if (getValue.IsNullOrEmpty || getValue != testValue)
            {
                throw new InvalidOperationException("Test key value mismatch in Redis");
            }

            _logger.LogDebug("Redis test key round-trip completed successfully");
        }
        finally
        {
            // Clean up test key
            await db.KeyDeleteAsync(testKey);
        }
    }
}
