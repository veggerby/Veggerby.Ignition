using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Kafka;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the verification strategy for Kafka readiness checks.
/// </summary>
public enum KafkaVerificationStrategy
{
    /// <summary>
    /// Validates basic cluster connectivity by retrieving broker metadata.
    /// Fast and lightweight—recommended for most scenarios.
    /// </summary>
    ClusterMetadata = 0,

    /// <summary>
    /// Verifies that specified topics exist by retrieving topic metadata.
    /// Requires <see cref="KafkaReadinessOptions.VerifyTopics"/> to be populated.
    /// </summary>
    TopicMetadata = 1,

    /// <summary>
    /// Produces a test message to a temporary topic to verify producer connectivity.
    /// More thorough but adds overhead—use when end-to-end verification is required.
    /// </summary>
    ProducerTest = 2,

    /// <summary>
    /// Verifies that the specified consumer group exists by listing consumer groups.
    /// Requires <see cref="KafkaReadinessOptions.VerifyConsumerGroup"/> to be set.
    /// </summary>
    ConsumerGroupCheck = 3
}
