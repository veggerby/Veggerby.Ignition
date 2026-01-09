#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Marten;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for Marten document store readiness verification.
/// </summary>
public sealed class MartenReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// When true, verifies that the document store is accessible by checking the connection.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remarks>
    /// This performs a basic connectivity check to ensure Marten can communicate with PostgreSQL.
    /// </remarks>
    public bool VerifyDocumentStore { get; set; } = true;
}
