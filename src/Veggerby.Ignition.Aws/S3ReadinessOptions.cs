namespace Veggerby.Ignition.Aws;

/// <summary>
/// Configuration options for AWS S3 readiness verification.
/// </summary>
public sealed class S3ReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Name of the S3 bucket to verify. If <c>null</c> or empty, only service-level connectivity is verified.
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// AWS region for the S3 bucket. If <c>null</c>, uses the region from the client configuration.
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Whether to verify that the bucket exists and is accessible.
    /// Default is <c>true</c> when <see cref="BucketName"/> is provided.
    /// </summary>
    /// <remarks>
    /// When enabled, performs a lightweight GetBucketLocation call to verify bucket existence and access.
    /// This validates that the credentials have appropriate permissions to access the bucket.
    /// </remarks>
    public bool VerifyBucketAccess { get; set; } = true;

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
