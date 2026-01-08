using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MassTransit;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for MassTransit bus readiness verification.
/// </summary>
public sealed class MassTransitReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Maximum time to wait for the bus to become ready after startup.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan BusReadyTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
