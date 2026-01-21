#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Pulsar.Client;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the verification strategy for Apache Pulsar readiness checks.
/// </summary>
public enum PulsarVerificationStrategy
{
    /// <summary>
    /// Validates basic cluster connectivity by creating a client connection.
    /// Fast and lightweight—recommended for most scenarios.
    /// </summary>
    ClusterHealth = 0,

    /// <summary>
    /// Verifies that specified topics exist by retrieving topic metadata.
    /// Requires <see cref="PulsarReadinessOptions.VerifyTopics"/> to be populated.
    /// </summary>
    TopicMetadata = 1,

    /// <summary>
    /// Produces a test message to verify producer connectivity.
    /// More thorough but adds overhead—use when end-to-end verification is required.
    /// Requires <see cref="PulsarReadinessOptions.VerifyTopics"/> to contain at least one topic.
    /// </summary>
    ProducerTest = 2,

    /// <summary>
    /// Verifies that the specified subscription exists for a topic.
    /// Requires <see cref="PulsarReadinessOptions.VerifySubscription"/> and 
    /// <see cref="PulsarReadinessOptions.SubscriptionTopic"/> to be set.
    /// </summary>
    SubscriptionCheck = 3,

    /// <summary>
    /// Validates broker health using the Pulsar admin API health endpoint.
    /// Requires <see cref="PulsarReadinessOptions.AdminServiceUrl"/> to be set.
    /// </summary>
    AdminApiCheck = 4
}
