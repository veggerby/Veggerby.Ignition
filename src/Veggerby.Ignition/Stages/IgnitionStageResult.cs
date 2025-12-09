using System.Collections.Generic;

namespace Veggerby.Ignition.Stages;

/// <summary>
/// Diagnostic details for a single stage/phase after evaluation.
/// </summary>
/// <param name="StageNumber">The stage number (0-based).</param>
/// <param name="Duration">Total elapsed time for this stage.</param>
/// <param name="Results">Per-signal results for signals in this stage.</param>
/// <param name="SucceededCount">Number of signals that succeeded in this stage.</param>
/// <param name="FailedCount">Number of signals that failed in this stage.</param>
/// <param name="TimedOutCount">Number of signals that timed out in this stage.</param>
/// <param name="Completed">Whether this stage completed (all signals finished, regardless of status).</param>
/// <param name="Promoted">Whether the next stage was started before this stage fully completed (early promotion).</param>
public sealed record IgnitionStageResult(
    int StageNumber,
    TimeSpan Duration,
    IReadOnlyList<IgnitionSignalResult> Results,
    int SucceededCount,
    int FailedCount,
    int TimedOutCount,
    bool Completed,
    bool Promoted = false)
{
    /// <summary>
    /// Gets the total number of signals in this stage.
    /// </summary>
    public int TotalSignals => Results.Count;

    /// <summary>
    /// Gets whether all signals in this stage succeeded.
    /// </summary>
    public bool AllSucceeded => SucceededCount == TotalSignals;

    /// <summary>
    /// Gets whether any signal in this stage failed (not including timeouts).
    /// </summary>
    public bool HasFailures => FailedCount > 0;

    /// <summary>
    /// Gets whether any signal in this stage timed out.
    /// </summary>
    public bool HasTimeouts => TimedOutCount > 0;

    /// <summary>
    /// Gets the success ratio (0.0 to 1.0) for this stage.
    /// </summary>
    public double SuccessRatio => TotalSignals > 0 ? (double)SucceededCount / TotalSignals : 1.0;
}
