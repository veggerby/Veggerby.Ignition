using System;
using System.Collections.Generic;

namespace Veggerby.Ignition.Elasticsearch;

/// <summary>
/// Configuration options for Elasticsearch readiness verification.
/// </summary>
public sealed class ElasticsearchReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(10);

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
    /// Maximum number of retry attempts for connection and operation failures.
    /// Default is 3 retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retry attempts. Uses exponential backoff.
    /// Default is 200ms.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Verification strategy to use for readiness check.
    /// Default is <see cref="ElasticsearchVerificationStrategy.ClusterHealth"/>.
    /// </summary>
    public ElasticsearchVerificationStrategy VerificationStrategy { get; set; } = ElasticsearchVerificationStrategy.ClusterHealth;

    /// <summary>
    /// List of index names to verify when using <see cref="ElasticsearchVerificationStrategy.IndexExists"/>.
    /// </summary>
    /// <remarks>
    /// Each index name in the list will be checked for existence.
    /// If <see cref="FailOnMissingIndices"/> is true, verification fails if any index is missing.
    /// </remarks>
    public List<string> VerifyIndices { get; } = new();

    /// <summary>
    /// Whether to fail the readiness check if any specified indices are missing.
    /// Default is true.
    /// </summary>
    /// <remarks>
    /// Only applies when <see cref="VerificationStrategy"/> is <see cref="ElasticsearchVerificationStrategy.IndexExists"/>.
    /// When false, missing indices are logged as warnings but do not cause verification failure.
    /// </remarks>
    public bool FailOnMissingIndices { get; set; } = true;

    /// <summary>
    /// Name of the index template to verify when using <see cref="ElasticsearchVerificationStrategy.TemplateValidation"/>.
    /// </summary>
    public string? VerifyTemplate { get; set; }

    /// <summary>
    /// Name of the index to query when using <see cref="ElasticsearchVerificationStrategy.QueryTest"/>.
    /// </summary>
    /// <remarks>
    /// The test query performs a simple match_all query with size=0 to verify the index is queryable
    /// without retrieving actual documents.
    /// </remarks>
    public string? TestQueryIndex { get; set; }
}
