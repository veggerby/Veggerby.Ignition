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
}
