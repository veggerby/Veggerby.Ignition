#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for Azure Table Storage readiness verification.
/// </summary>
public sealed class AzureTableReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Name of the table to verify. If <c>null</c> or empty, only service-level connectivity is verified.
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Whether to verify that the specified table exists.
    /// Default is <c>true</c> when <see cref="TableName"/> is provided.
    /// </summary>
    public bool VerifyTableExists { get; set; } = true;

    /// <summary>
    /// Whether to create the table if it does not exist during verification.
    /// Only applies when <see cref="VerifyTableExists"/> is <c>true</c>.
    /// Default is <c>false</c> (fail if table is missing).
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> to automatically provision tables during startup.
    /// Ensure the connection has appropriate permissions to create tables.
    /// </remarks>
    public bool CreateIfNotExists { get; set; } = false;
}
