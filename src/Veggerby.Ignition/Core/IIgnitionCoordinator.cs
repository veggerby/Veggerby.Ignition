using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Coordinates waiting for all registered <see cref="IIgnitionSignal"/> instances.
/// Provides aggregated diagnostics and timing information for startup readiness.
/// </summary>
/// <remarks>
/// <para>
/// Implementations cache the underlying wait operation to avoid re-running expensive initialization logic.
/// Consumers may await <see cref="WaitAllAsync"/> or inspect the final result via <see cref="GetResultAsync"/>.
/// </para>
/// <para>
/// The coordinator exposes a state machine with observable lifecycle events for monitoring progress.
/// State transitions: <see cref="IgnitionState.NotStarted"/> → <see cref="IgnitionState.Running"/> →
/// (<see cref="IgnitionState.Completed"/> | <see cref="IgnitionState.Failed"/> | <see cref="IgnitionState.TimedOut"/>)
/// </para>
/// </remarks>
public interface IIgnitionCoordinator
{
    /// <summary>
    /// Gets the current state of the coordinator lifecycle.
    /// </summary>
    /// <remarks>
    /// The state transitions once from <see cref="IgnitionState.NotStarted"/> to <see cref="IgnitionState.Running"/>
    /// when <see cref="WaitAllAsync"/> is first called, and then to a terminal state upon completion.
    /// </remarks>
    IgnitionState State { get; }

    /// <summary>
    /// Occurs when an individual signal starts execution.
    /// </summary>
    /// <remarks>
    /// Event handlers are invoked synchronously on the thread executing the signal.
    /// Handlers should be fast and non-blocking to avoid delaying signal execution.
    /// </remarks>
    event EventHandler<IgnitionSignalStartedEventArgs>? SignalStarted;

    /// <summary>
    /// Occurs when an individual signal completes execution (success, failure, timeout, or skipped).
    /// </summary>
    /// <remarks>
    /// Event handlers are invoked synchronously on the thread that completed the signal.
    /// Handlers should be fast and non-blocking to avoid delaying subsequent signal execution.
    /// </remarks>
    event EventHandler<IgnitionSignalCompletedEventArgs>? SignalCompleted;

    /// <summary>
    /// Occurs when the global timeout is reached during execution.
    /// </summary>
    /// <remarks>
    /// This event is raised once when the global timeout deadline elapses, regardless of whether
    /// <see cref="IgnitionOptions.CancelOnGlobalTimeout"/> is enabled. Handlers can use this
    /// to observe timeout conditions before the final result is available.
    /// </remarks>
    event EventHandler<IgnitionGlobalTimeoutEventArgs>? GlobalTimeoutReached;

    /// <summary>
    /// Occurs when the coordinator completes execution (terminal state reached).
    /// </summary>
    /// <remarks>
    /// This event is raised exactly once when the coordinator transitions to a terminal state
    /// (<see cref="IgnitionState.Completed"/>, <see cref="IgnitionState.Failed"/>, or <see cref="IgnitionState.TimedOut"/>).
    /// The <see cref="IgnitionCoordinatorCompletedEventArgs.Result"/> contains the full aggregated result.
    /// </remarks>
    event EventHandler<IgnitionCoordinatorCompletedEventArgs>? CoordinatorCompleted;

    /// <summary>
    /// Await completion (success, failure or timeout) of all ignition signals according to configured <see cref="IgnitionOptions"/>.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation to stop waiting early.</param>
    /// <returns>A task that completes when readiness evaluation is finished.</returns>
    Task WaitAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the aggregated result of the ignition process including per-signal outcomes and total duration.
    /// </summary>
    /// <returns>The <see cref="IgnitionResult"/> describing completion state.</returns>
    Task<IgnitionResult> GetResultAsync();
}
