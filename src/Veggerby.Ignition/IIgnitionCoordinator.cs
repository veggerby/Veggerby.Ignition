namespace Veggerby.Ignition;

/// <summary>
/// Coordinates waiting for all registered <see cref="IIgnitionSignal"/> instances.
/// Provides aggregated diagnostics and timing information for startup readiness.
/// </summary>
/// <remarks>
/// Implementations cache the underlying wait operation to avoid re-running expensive initialization logic.
/// Consumers may await <see cref="WaitAllAsync"/> or inspect the final result via <see cref="GetResultAsync"/>.
/// </remarks>
public interface IIgnitionCoordinator
{
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
