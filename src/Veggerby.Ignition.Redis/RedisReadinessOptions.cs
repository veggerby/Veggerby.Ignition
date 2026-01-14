#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Redis;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Verification strategy for Redis readiness checks.
/// </summary>
public enum RedisVerificationStrategy
{
    /// <summary>
    /// Only verify that connection can be established.
    /// Fastest option with minimal overhead.
    /// </summary>
    ConnectionOnly,

    /// <summary>
    /// Execute PING command to verify server responsiveness.
    /// </summary>
    Ping,

    /// <summary>
    /// Execute both PING and a test key set/get/delete round-trip.
    /// Most thorough verification ensuring full read/write capability.
    /// </summary>
    PingAndTestKey
}

/// <summary>
/// Configuration options for Redis readiness verification.
/// </summary>
public sealed class RedisReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Verification strategy to use for readiness check.
    /// Default is <see cref="RedisVerificationStrategy.ConnectionOnly"/>.
    /// </summary>
    public RedisVerificationStrategy VerificationStrategy { get; set; } = RedisVerificationStrategy.ConnectionOnly;

    /// <summary>
    /// Prefix for test keys when using <see cref="RedisVerificationStrategy.PingAndTestKey"/>.
    /// Default is "ignition:readiness:".
    /// </summary>
    /// <remarks>
    /// Test keys are created with a 60-second TTL and explicitly deleted after verification.
    /// </remarks>
    public string TestKeyPrefix { get; set; } = "ignition:readiness:";

    /// <summary>
    /// Maximum number of retry attempts for connection and operation failures.
    /// Default is 3 retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retry attempts. Uses exponential backoff.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Timeout for establishing initial Redis connection.
    /// Default is 10 seconds to allow for service readiness after container starts.
    /// </summary>
    /// <remarks>
    /// This is particularly important in containerized environments where Redis may report
    /// as "container ready" before it's actually accepting connections.
    /// </remarks>
    public int ConnectTimeout { get; set; } = 10000;

    /// <summary>
    /// Optional stage/phase number for staged execution.
    /// If <c>null</c>, the signal belongs to stage 0 (default/unstaged).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stages enable sequential execution across logical phases (e.g., infrastructure → services → workers).
    /// All signals in stage N complete before stage N+1 begins.
    /// </para>
    /// <para>
    /// Particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// </remarks>
    public int? Stage { get; set; }
}
