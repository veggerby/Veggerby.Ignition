#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Aws;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
}
