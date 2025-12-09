namespace Veggerby.Ignition.Metrics;

/// <summary>
/// Abstraction for recording ignition metrics, enabling integration with observability systems
/// (OpenTelemetry, Prometheus, App Metrics, etc.) without adding any external dependencies.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to be thread-safe as metrics may be recorded from multiple concurrent signals.
/// The default <see cref="NullIgnitionMetrics"/> implementation is a no-op that adds no overhead.
/// </para>
/// <para>
/// Users can provide their own implementation to integrate with enterprise monitoring stacks
/// by configuring <see cref="IgnitionOptions.Metrics"/>.
/// </para>
/// </remarks>
public interface IIgnitionMetrics
{
    /// <summary>
    /// Records the duration of a signal execution.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="duration">The elapsed time for the signal execution.</param>
    /// <remarks>
    /// Called once per signal when it completes, regardless of success, failure, or timeout.
    /// Implementations should avoid allocations and blocking operations.
    /// </remarks>
    void RecordSignalDuration(string name, TimeSpan duration);

    /// <summary>
    /// Records the completion status of a signal.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="status">The outcome status of the signal.</param>
    /// <remarks>
    /// Called once per signal when it completes. Useful for tracking success/failure rates.
    /// Implementations should avoid allocations and blocking operations.
    /// </remarks>
    void RecordSignalStatus(string name, IgnitionSignalStatus status);

    /// <summary>
    /// Records the total duration of the ignition process.
    /// </summary>
    /// <param name="duration">The total elapsed time for all signals.</param>
    /// <remarks>
    /// Called once when the coordinator completes execution.
    /// Implementations should avoid allocations and blocking operations.
    /// </remarks>
    void RecordTotalDuration(TimeSpan duration);
}
