using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides read-only context information for timeout strategy decisions on a per-signal basis.
/// </summary>
/// <remarks>
/// <para>
/// This type is purposely limited to the information that is relevant for timeout decisions,
/// decoupling <see cref="IIgnitionTimeoutStrategy"/> implementations from the full
/// <see cref="IgnitionOptions"/> object and ensuring forward compatibility when new options are added.
/// </para>
/// <para>
/// Adaptive strategies can use <see cref="RemainingGlobalBudget"/> and <see cref="PendingSignalCount"/>
/// to distribute remaining time across outstanding signals or apply backpressure as the deadline approaches.
/// </para>
/// </remarks>
public readonly struct IgnitionTimeoutContext
{
    /// <summary>
    /// Gets the configured global timeout deadline for the entire ignition run.
    /// </summary>
    /// <remarks>
    /// This is the value of <see cref="IgnitionOptions.GlobalTimeout"/> at execution time.
    /// A value of <see cref="System.Threading.Timeout.InfiniteTimeSpan"/> indicates no global deadline.
    /// </remarks>
    public TimeSpan GlobalTimeout { get; init; }

    /// <summary>
    /// Gets the global setting for whether individual signals should be cancelled on timeout.
    /// </summary>
    /// <remarks>
    /// This reflects <see cref="IgnitionOptions.CancelIndividualOnTimeout"/>.
    /// Strategies may override this per-signal by returning a different value from
    /// <see cref="IIgnitionTimeoutStrategy.GetTimeout"/>.
    /// </remarks>
    public bool CancelIndividualOnTimeout { get; init; }

    /// <summary>
    /// Gets the elapsed time since ignition started, measured at the point this context was created.
    /// </summary>
    /// <remarks>
    /// Adaptive strategies can use this to understand how much of the global budget has been consumed.
    /// </remarks>
    public TimeSpan ElapsedTime { get; init; }

    /// <summary>
    /// Gets the remaining budget before the global deadline elapses.
    /// </summary>
    /// <remarks>
    /// Computed as <c>GlobalTimeout - ElapsedTime</c>. May be negative if the global deadline has already passed.
    /// Adaptive strategies can use this to proportionally distribute remaining time across outstanding signals.
    /// </remarks>
    public TimeSpan RemainingGlobalBudget => GlobalTimeout - ElapsedTime;

    /// <summary>
    /// Gets the number of signals that have not yet completed at the time this context was created.
    /// </summary>
    /// <remarks>
    /// Adaptive strategies can use this to implement fair scheduling (e.g., divide remaining budget equally
    /// across all pending signals).
    /// </remarks>
    public int PendingSignalCount { get; init; }
}
