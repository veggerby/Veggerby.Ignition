namespace Veggerby.Ignition.Memcached;

/// <summary>
/// Verification strategy for Memcached readiness checks.
/// </summary>
public enum MemcachedVerificationStrategy
{
    /// <summary>
    /// Only verify that connection can be established.
    /// Fastest option with minimal overhead.
    /// </summary>
    ConnectionOnly,

    /// <summary>
    /// Execute stats command to verify server responsiveness.
    /// </summary>
    Stats,

    /// <summary>
    /// Execute a test key set/get/delete round-trip.
    /// Most thorough verification ensuring full read/write capability.
    /// </summary>
    TestKey
}

/// <summary>
/// Configuration options for Memcached readiness verification.
/// </summary>
public sealed class MemcachedReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Verification strategy to use for readiness check.
    /// Default is <see cref="MemcachedVerificationStrategy.ConnectionOnly"/>.
    /// </summary>
    public MemcachedVerificationStrategy VerificationStrategy { get; set; } = MemcachedVerificationStrategy.ConnectionOnly;

    /// <summary>
    /// Prefix for test keys when using <see cref="MemcachedVerificationStrategy.TestKey"/>.
    /// Default is "ignition:readiness:".
    /// </summary>
    /// <remarks>
    /// Test keys are created with a 60-second expiration and explicitly deleted after verification.
    /// </remarks>
    public string TestKeyPrefix { get; set; } = "ignition:readiness:";

    /// <summary>
    /// Maximum number of retry attempts for transient connection failures.
    /// Default is 3 attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retry attempts.
    /// Subsequent delays use exponential backoff (doubled each retry).
    /// Default is 100 milliseconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

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
