using System.Collections.Generic;

namespace Veggerby.Ignition;

/// <summary>
/// Outcome classification for an individual ignition signal.
/// </summary>
public enum IgnitionSignalStatus
{
    /// <summary>
    /// The signal completed successfully.
    /// </summary>
    Succeeded,
    /// <summary>
    /// The signal faulted with an exception.
    /// </summary>
    Failed,
    /// <summary>
    /// The signal exceeded its timeout before completion.
    /// </summary>
    TimedOut,
    /// <summary>
    /// The signal was not executed because one or more of its dependencies failed (dependency-aware mode only).
    /// </summary>
    Skipped
}

/// <summary>
/// Diagnostic details for a single ignition signal after evaluation.
/// </summary>
/// <param name="Name">Name of the signal.</param>
/// <param name="Status">Completion status.</param>
/// <param name="Duration">Elapsed time waiting for the signal.</param>
/// <param name="Exception">Captured exception if <see cref="IgnitionSignalStatus.Failed"/>.</param>
/// <param name="FailedDependencies">Names of dependency signals that failed, preventing this signal from executing (dependency-aware mode only).</param>
public sealed record IgnitionSignalResult(
    string Name,
    IgnitionSignalStatus Status,
    TimeSpan Duration,
    Exception? Exception = null,
    IReadOnlyList<string>? FailedDependencies = null)
{
    /// <summary>
    /// Gets whether this signal was skipped due to failed dependencies.
    /// </summary>
    public bool SkippedDueToDependencies => FailedDependencies is not null && FailedDependencies.Count > 0;
}

/// <summary>
/// Aggregated result representing all ignition signals and overall timing info.
/// </summary>
/// <param name="TotalDuration">Total elapsed time for ignition evaluation.</param>
/// <param name="Results">Per-signal results (may be partial if timed out).</param>
/// <param name="TimedOut">True if a global timeout occurred before all signals completed.</param>
public sealed record IgnitionResult(
    TimeSpan TotalDuration,
    IReadOnlyList<IgnitionSignalResult> Results,
    bool TimedOut)
{
    /// <summary>
    /// Convenience result for the case where no signals were registered.
    /// </summary>
    public static IgnitionResult EmptySuccess => new(TimeSpan.Zero, [], TimedOut: false);

    /// <summary>
    /// Creates a successful ignition result.
    /// </summary>
    public static IgnitionResult FromResults(IReadOnlyList<IgnitionSignalResult> results, TimeSpan total) => new(total, results, TimedOut: false);

    /// <summary>
    /// Creates a timeout ignition result with partial signal outcomes.
    /// </summary>
    public static IgnitionResult FromTimeout(IReadOnlyList<IgnitionSignalResult> partial, TimeSpan total) => new(total, partial, TimedOut: true);
}
