#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Memcached;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
}
