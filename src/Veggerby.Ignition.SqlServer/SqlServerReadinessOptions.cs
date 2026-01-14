#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.SqlServer;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for SQL Server readiness verification.
/// </summary>
public sealed class SqlServerReadinessOptions
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
