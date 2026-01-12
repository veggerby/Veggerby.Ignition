#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for Azure Blob Storage readiness verification.
/// </summary>
public sealed class AzureBlobReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Name of the container to verify. If <c>null</c> or empty, only service-level connectivity is verified.
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Whether to verify that the specified container exists.
    /// Default is <c>true</c> when <see cref="ContainerName"/> is provided.
    /// </summary>
    public bool VerifyContainerExists { get; set; } = true;

    /// <summary>
    /// Whether to create the container if it does not exist during verification.
    /// Only applies when <see cref="VerifyContainerExists"/> is <c>true</c>.
    /// Default is <c>false</c> (fail if container is missing).
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> to automatically provision containers during startup.
    /// Ensure the connection has appropriate permissions to create containers.
    /// </remarks>
    public bool CreateIfNotExists { get; set; } = false;
}
