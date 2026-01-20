using System;
using System.Collections.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MariaDb;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for MariaDB readiness verification.
/// </summary>
public sealed class MariaDbReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(30);

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

    /// <summary>
    /// Maximum number of retry attempts for transient connection failures.
    /// Default is 8 attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 8;

    /// <summary>
    /// Initial delay between retry attempts.
    /// Subsequent delays use exponential backoff (doubled each retry).
    /// Default is 500 milliseconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// MariaDB database verification strategy to use.
    /// Default is <see cref="MariaDbVerificationStrategy.Ping"/>.
    /// </summary>
    public MariaDbVerificationStrategy VerificationStrategy { get; set; } = MariaDbVerificationStrategy.Ping;

    /// <summary>
    /// List of table names to verify existence when using <see cref="MariaDbVerificationStrategy.TableExists"/>.
    /// </summary>
    public List<string> VerifyTables { get; } = new();

    /// <summary>
    /// Whether to fail readiness if any table in <see cref="VerifyTables"/> does not exist.
    /// Default is <c>true</c>.
    /// </summary>
    public bool FailOnMissingTables { get; set; } = true;

    /// <summary>
    /// Optional schema/database name for table verification.
    /// If <c>null</c>, uses the database from the connection string.
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Optional custom SQL query to execute for verification.
    /// If specified, overrides the verification strategy.
    /// </summary>
    public string? TestQuery { get; set; }

    /// <summary>
    /// Optional minimum number of rows expected from <see cref="TestQuery"/>.
    /// If specified, validation fails if the query returns fewer rows.
    /// </summary>
    public int? ExpectedMinimumRows { get; set; }
}
