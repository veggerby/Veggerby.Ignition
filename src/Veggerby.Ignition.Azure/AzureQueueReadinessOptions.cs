#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for Azure Queue Storage readiness verification.
/// </summary>
public sealed class AzureQueueReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Name of the queue to verify. If <c>null</c> or empty, only service-level connectivity is verified.
    /// </summary>
    public string? QueueName { get; set; }

    /// <summary>
    /// Whether to verify that the specified queue exists.
    /// Default is <c>true</c> when <see cref="QueueName"/> is provided.
    /// </summary>
    public bool VerifyQueueExists { get; set; } = true;

    /// <summary>
    /// Whether to create the queue if it does not exist during verification.
    /// Only applies when <see cref="VerifyQueueExists"/> is <c>true</c>.
    /// Default is <c>false</c> (fail if queue is missing).
    /// </summary>
    /// <remarks>
    /// Set to <c>true</c> to automatically provision queues during startup.
    /// Ensure the connection has appropriate permissions to create queues.
    /// </remarks>
    public bool CreateIfNotExists { get; set; } = false;
}
