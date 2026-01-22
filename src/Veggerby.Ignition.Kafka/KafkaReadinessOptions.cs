using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Veggerby.Ignition.Kafka;

/// <summary>
/// Configuration options for Kafka readiness verification.
/// </summary>
public sealed class KafkaReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// Default is 30 seconds to accommodate slow Kafka broker initialization in CI environments.
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
    /// Default is 8 attempts to accommodate Kafka broker startup time in CI environments.
    /// With exponential backoff starting at 500ms: 500ms + 1s + 2s + 4s + 8s = ~15.5s retry window.
    /// </summary>
    public int MaxRetries { get; set; } = 8;

    /// <summary>
    /// Initial delay between retry attempts.
    /// Subsequent delays use exponential backoff (doubled each retry).
    /// Default is 500 milliseconds to reduce noise during Kafka broker initialization.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Verification strategy to use for readiness checks.
    /// Default is <see cref="KafkaVerificationStrategy.ClusterMetadata"/> (fast, validates broker connectivity).
    /// </summary>
    public KafkaVerificationStrategy VerificationStrategy { get; set; } = KafkaVerificationStrategy.ClusterMetadata;

    /// <summary>
    /// List of topic names to verify during readiness check.
    /// Only applies when <see cref="VerificationStrategy"/> is <see cref="KafkaVerificationStrategy.TopicMetadata"/>.
    /// Empty by default (no topic verification performed).
    /// </summary>
    public List<string> VerifyTopics { get; } = new List<string>();

    /// <summary>
    /// When true, missing topics will cause the signal to fail.
    /// When false, missing topics are logged as a warning but do not fail the signal.
    /// Default is <c>true</c> (fail on missing topics).
    /// Only applies when <see cref="VerificationStrategy"/> is <see cref="KafkaVerificationStrategy.TopicMetadata"/>.
    /// </summary>
    public bool FailOnMissingTopics { get; set; } = true;

    /// <summary>
    /// Consumer group name to verify during readiness check.
    /// Only applies when <see cref="VerificationStrategy"/> is <see cref="KafkaVerificationStrategy.ConsumerGroupCheck"/>.
    /// </summary>
    public string? VerifyConsumerGroup { get; set; }

    /// <summary>
    /// Schema Registry URL for Confluent-specific schema verification (optional).
    /// When set and <see cref="VerifySchemaRegistry"/> is <c>true</c>, the signal will verify Schema Registry accessibility.
    /// </summary>
    public string? SchemaRegistryUrl { get; set; }

    /// <summary>
    /// When true, verifies Schema Registry accessibility (Confluent-specific feature).
    /// Requires <see cref="SchemaRegistryUrl"/> to be set.
    /// Default is <c>false</c> (no Schema Registry verification).
    /// </summary>
    public bool VerifySchemaRegistry { get; set; } = false;

    /// <summary>
    /// Adds a topic name to the list of topics to verify.
    /// </summary>
    /// <param name="topicName">Name of the topic to verify.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    [ExcludeFromCodeCoverage]
    public KafkaReadinessOptions WithTopic(string topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName, nameof(topicName));
        VerifyTopics.Add(topicName);
        return this;
    }
}
