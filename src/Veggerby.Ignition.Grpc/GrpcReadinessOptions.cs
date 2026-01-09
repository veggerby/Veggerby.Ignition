#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Grpc;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for gRPC service readiness verification.
/// </summary>
public sealed class GrpcReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Optional service name to check via gRPC health check protocol.
    /// If <c>null</c>, checks overall server health.
    /// </summary>
    /// <remarks>
    /// Specify a service name (e.g., "myservice") to verify health of a specific service.
    /// Leave null to check the overall server health.
    /// </remarks>
    public string? ServiceName { get; set; }
}
