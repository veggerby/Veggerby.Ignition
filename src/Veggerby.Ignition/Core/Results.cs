using System.Collections.Generic;
using System.Linq;

using Veggerby.Ignition.Stages;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
    Skipped,
    /// <summary>
    /// The signal was cancelled due to hierarchical cancellation propagation (e.g., parent scope cancelled,
    /// sibling signal failed within a bundle, or dependency failed with cancellation propagation enabled).
    /// </summary>
    Cancelled
}

/// <summary>
/// Diagnostic details for a single ignition signal after evaluation.
/// </summary>
/// <param name="Name">Name of the signal.</param>
/// <param name="Status">Completion status.</param>
/// <param name="Duration">Elapsed time waiting for the signal.</param>
/// <param name="Exception">Captured exception if <see cref="IgnitionSignalStatus.Failed"/>.</param>
/// <param name="FailedDependencies">Names of dependency signals that failed, preventing this signal from executing (dependency-aware mode only).</param>
/// <param name="CancellationReason">Reason for cancellation if the signal was cancelled (timed out or cancelled via scope).</param>
/// <param name="CancelledBySignal">Name of the signal that triggered the cancellation, if applicable (hierarchical cancellation).</param>
/// <param name="StartedAt">Offset from ignition start when this signal began execution. Used for timeline export.</param>
/// <param name="CompletedAt">Offset from ignition start when this signal completed. Used for timeline export.</param>
public sealed record IgnitionSignalResult(
    string Name,
    IgnitionSignalStatus Status,
    TimeSpan Duration,
    Exception? Exception = null,
    IReadOnlyList<string>? FailedDependencies = null,
    CancellationReason CancellationReason = CancellationReason.None,
    string? CancelledBySignal = null,
    TimeSpan? StartedAt = null,
    TimeSpan? CompletedAt = null)
{
    /// <summary>
    /// Gets whether this signal was skipped due to failed dependencies.
    /// Returns <c>true</c> only when the status is <see cref="IgnitionSignalStatus.Skipped"/>
    /// and there are failed dependencies recorded.
    /// </summary>
    public bool SkippedDueToDependencies =>
        Status == IgnitionSignalStatus.Skipped &&
        FailedDependencies is not null &&
        FailedDependencies.Count > 0;

    /// <summary>
    /// Gets whether this signal has failed dependencies, regardless of status.
    /// </summary>
    public bool HasFailedDependencies => FailedDependencies is not null && FailedDependencies.Count > 0;

    /// <summary>
    /// Gets whether this signal was cancelled due to hierarchical cancellation propagation.
    /// </summary>
    public bool WasCancelledByScope => CancellationReason is CancellationReason.ScopeCancelled
                                       or CancellationReason.BundleCancelled
                                       or CancellationReason.DependencyFailed;

    /// <summary>
    /// Gets whether this signal result contains timeline data (start and completion timestamps).
    /// </summary>
    public bool HasTimelineData => StartedAt.HasValue && CompletedAt.HasValue;
}

/// <summary>
/// Aggregated result representing all ignition signals and overall timing info.
/// </summary>
/// <param name="TotalDuration">Total elapsed time for ignition evaluation.</param>
/// <param name="Results">Per-signal results (may be partial if timed out).</param>
/// <param name="TimedOut">True if a global timeout occurred before all signals completed.</param>
/// <param name="StageResults">Per-stage results when using <see cref="IgnitionExecutionMode.Staged"/> execution mode.</param>
public sealed record IgnitionResult(
    TimeSpan TotalDuration,
    IReadOnlyList<IgnitionSignalResult> Results,
    bool TimedOut,
    IReadOnlyList<IgnitionStageResult>? StageResults = null)
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

    /// <summary>
    /// Creates a staged ignition result with per-stage information.
    /// </summary>
    public static IgnitionResult FromStaged(IReadOnlyList<IgnitionSignalResult> results, IReadOnlyList<IgnitionStageResult> stageResults, TimeSpan total, bool timedOut = false)
        => new(total, results, timedOut, stageResults);

    /// <summary>
    /// Gets whether this result includes stage-level information.
    /// </summary>
    public bool HasStageResults => StageResults is not null && StageResults.Count > 0;

    /// <summary>
    /// Gets whether this result contains timeline data (per-signal start and completion timestamps).
    /// Returns true if all signal results have timeline data populated.
    /// </summary>
    public bool HasTimelineData
    {
        get
        {
            if (Results.Count == 0)
            {
                return false;
            }

            return Results.All(result => result.HasTimelineData);
        }
    }
}
