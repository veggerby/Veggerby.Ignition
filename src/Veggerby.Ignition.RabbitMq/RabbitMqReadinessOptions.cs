using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Veggerby.Ignition.RabbitMq;

/// <summary>
/// Configuration options for RabbitMQ readiness verification.
/// </summary>
public sealed class RabbitMqReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Queue names to verify during readiness check.
    /// If specified, the signal will attempt to verify each queue exists and is accessible.
    /// Empty by default (no queue verification performed).
    /// </summary>
    public ICollection<string> VerifyQueues { get; } = new List<string>();

    /// <summary>
    /// Exchange names to verify during readiness check.
    /// If specified, the signal will attempt to declare each exchange passively to verify it exists.
    /// Empty by default (no exchange verification performed).
    /// </summary>
    public ICollection<string> VerifyExchanges { get; } = new List<string>();

    /// <summary>
    /// When true, missing queues or exchanges will cause the signal to fail.
    /// When false, missing topology is logged as a warning but does not fail the signal.
    /// Default is <c>true</c> (fail on missing topology).
    /// </summary>
    public bool FailOnMissingTopology { get; set; } = true;

    /// <summary>
    /// When true, performs a publish/consume round-trip test on a temporary queue.
    /// This validates end-to-end messaging capability but adds overhead.
    /// Default is <c>false</c> (connection verification only).
    /// </summary>
    public bool PerformRoundTripTest { get; set; } = false;

    /// <summary>
    /// Maximum time to wait for the round-trip test message acknowledgment.
    /// Only applies when <see cref="PerformRoundTripTest"/> is <c>true</c>.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan RoundTripTestTimeout { get; set; } = TimeSpan.FromSeconds(5);

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
    /// Adds a queue name to the list of queues to verify.
    /// </summary>
    /// <param name="queueName">Name of the queue to verify.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    [ExcludeFromCodeCoverage]
    public RabbitMqReadinessOptions WithQueue(string queueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName, nameof(queueName));
        VerifyQueues.Add(queueName);
        return this;
    }

    /// <summary>
    /// Adds an exchange name to the list of exchanges to verify.
    /// </summary>
    /// <param name="exchangeName">Name of the exchange to verify.</param>
    /// <returns>This options instance for fluent chaining.</returns>
    [ExcludeFromCodeCoverage]
    public RabbitMqReadinessOptions WithExchange(string exchangeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeName, nameof(exchangeName));
        VerifyExchanges.Add(exchangeName);
        return this;
    }

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
