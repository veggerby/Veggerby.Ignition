using System.Collections.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides context for policy decision-making after a signal completes.
/// </summary>
/// <remarks>
/// <para>
/// This context is passed to <see cref="IIgnitionPolicy.ShouldContinue"/> after each signal completes,
/// enabling policies to make informed decisions based on the current signal result and overall ignition state.
/// </para>
/// <para>
/// All properties are read-only to ensure policies observe execution state without mutating it.
/// </para>
/// </remarks>
public sealed class IgnitionPolicyContext
{
    /// <summary>
    /// Gets the result of the signal that just completed.
    /// </summary>
    /// <remarks>
    /// This is the most recent signal result. The policy can inspect its <see cref="IgnitionSignalResult.Status"/>,
    /// <see cref="IgnitionSignalResult.Duration"/>, <see cref="IgnitionSignalResult.Exception"/>, and other
    /// properties to determine whether execution should continue.
    /// </remarks>
    public required IgnitionSignalResult SignalResult { get; init; }

    /// <summary>
    /// Gets a read-only list of all signals that have completed so far (including the current signal).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This list includes all signals that have finished execution up to this point, regardless of their status.
    /// Policies can use this to compute aggregate metrics such as success rates, failure counts, or timeout counts.
    /// </para>
    /// <para>
    /// In sequential execution mode, this list grows one signal at a time. In parallel execution mode,
    /// the list may grow by multiple signals between policy invocations.
    /// </para>
    /// </remarks>
    public required IReadOnlyList<IgnitionSignalResult> CompletedSignals { get; init; }

    /// <summary>
    /// Gets the total number of signals registered for ignition.
    /// </summary>
    /// <remarks>
    /// This count includes all signals, whether completed, in progress, or not yet started.
    /// Policies can compare <see cref="CompletedSignals"/> count against this value to determine
    /// completion progress.
    /// </remarks>
    public required int TotalSignalCount { get; init; }

    /// <summary>
    /// Gets the elapsed time since ignition started.
    /// </summary>
    /// <remarks>
    /// This value reflects the total time from when ignition execution began to when the current signal completed.
    /// Policies can use this to implement time-based decisions (e.g., fail if overall duration exceeds a threshold).
    /// </remarks>
    public required TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets whether the global timeout deadline has elapsed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <c>true</c> if the <see cref="IgnitionOptions.GlobalTimeout"/> deadline has passed,
    /// regardless of whether <see cref="IgnitionOptions.CancelOnGlobalTimeout"/> is enabled.
    /// </para>
    /// <para>
    /// Policies can use this to implement custom timeout handling logic. For example, a policy might
    /// continue execution even after global timeout if a certain percentage of signals have succeeded.
    /// </para>
    /// </remarks>
    public required bool GlobalTimeoutElapsed { get; init; }

    /// <summary>
    /// Gets the execution mode used for this ignition run.
    /// </summary>
    /// <remarks>
    /// Policies can adjust their behavior based on execution mode. For example, a policy might
    /// be more lenient in parallel mode (where failures are expected to be aggregated) versus
    /// sequential mode (where failures should stop execution immediately).
    /// </remarks>
    public required IgnitionExecutionMode ExecutionMode { get; init; }
}
