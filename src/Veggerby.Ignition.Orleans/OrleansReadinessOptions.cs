#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Orleans;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for Orleans cluster readiness verification.
/// </summary>
public sealed class OrleansReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
