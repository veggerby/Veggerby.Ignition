#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Postgres;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for PostgreSQL readiness verification.
/// </summary>
public sealed class PostgresReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Optional SQL query to execute for validation after connection is established.
    /// If <c>null</c>, only connection establishment is verified.
    /// Default is <c>null</c> (connection verification only).
    /// </summary>
    /// <remarks>
    /// Use simple queries like "SELECT 1" for basic health checks.
    /// More complex queries can verify schema readiness or data availability.
    /// </remarks>
    public string? ValidationQuery { get; set; }

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
}
