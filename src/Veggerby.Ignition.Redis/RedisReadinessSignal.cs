using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Veggerby.Ignition.Redis;

/// <summary>
/// Ignition signal for verifying Redis cache readiness.
/// Validates connection and optionally executes PING or test key operations.
/// </summary>
internal sealed class RedisReadinessSignal : IIgnitionSignal
{
    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly Func<IConnectionMultiplexer>? _connectionMultiplexerFactory;
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
        _connectionMultiplexerFactory = null;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisReadinessSignal"/> class
    /// using a factory function for lazy multiplexer creation.
    /// </summary>
    /// <param name="connectionMultiplexerFactory">Factory function that creates a connection multiplexer when invoked.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public RedisReadinessSignal(
        Func<IConnectionMultiplexer> connectionMultiplexerFactory,
        RedisReadinessOptions options,
        ILogger<RedisReadinessSignal> logger)
    {
        _connectionMultiplexer = null;
        _connectionMultiplexerFactory = connectionMultiplexerFactory ?? throw new ArgumentNullException(nameof(connectionMultiplexerFactory));
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

        // Resolve multiplexer from factory if needed
        var multiplexer = _connectionMultiplexer ?? _connectionMultiplexerFactory!();

        var endpoints = string.Join(", ", multiplexer.GetEndPoints().Select(ep => ep.ToString()));
        activity?.SetTag("redis.endpoints", endpoints);
        activity?.SetTag("redis.verification_strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "Redis readiness check starting for endpoints {Endpoints} using strategy {Strategy}",
            endpoints,
            _options.VerificationStrategy);

        var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

        await retryPolicy.ExecuteAsync(
            async ct =>
            {
                if (!multiplexer.IsConnected)
                {
                    throw new InvalidOperationException("Redis connection multiplexer is not connected");
                }

                _logger.LogDebug("Redis connection established");

                if (_options.VerificationStrategy == RedisVerificationStrategy.Ping ||
                    _options.VerificationStrategy == RedisVerificationStrategy.PingAndTestKey)
                {
                    await ExecutePingAsync(multiplexer, ct);
                }

                if (_options.VerificationStrategy == RedisVerificationStrategy.PingAndTestKey)
                {
                    await ExecuteTestKeyRoundTripAsync(multiplexer, ct);
                }
            },
            "Redis readiness check",
            cancellationToken,
            _options.Timeout);

        _logger.LogInformation("Redis readiness check completed successfully");
    }

    private async Task ExecutePingAsync(IConnectionMultiplexer multiplexer, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing Redis PING command");

        var db = multiplexer.GetDatabase();
        var pingResult = await db.PingAsync().ConfigureAwait(false);

        _logger.LogDebug("Redis PING completed in {Duration}ms", pingResult.TotalMilliseconds);
    }

    private async Task ExecuteTestKeyRoundTripAsync(IConnectionMultiplexer multiplexer, CancellationToken cancellationToken)
    {
        var testKey = $"{_options.TestKeyPrefix}{Guid.NewGuid():N}";
        var testValue = Guid.NewGuid().ToString("N");

        _logger.LogDebug("Executing Redis test key round-trip for key {TestKey}", testKey);

        var db = multiplexer.GetDatabase();

        try
        {
            // Set with 60-second TTL
            var setResult = await db.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            if (!setResult)
            {
                throw new InvalidOperationException("Failed to set test key in Redis");
            }

            // Get and verify
            var getValue = await db.StringGetAsync(testKey).ConfigureAwait(false);
            if (getValue.IsNullOrEmpty || getValue != testValue)
            {
                throw new InvalidOperationException("Test key value mismatch in Redis");
            }

            _logger.LogDebug("Redis test key round-trip completed successfully");
        }
        finally
        {
            // Clean up test key
            await db.KeyDeleteAsync(testKey).ConfigureAwait(false);
        }
    }
}
