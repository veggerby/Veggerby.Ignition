namespace Veggerby.Ignition;

/// <summary>
/// Specifies the reason why an ignition signal was cancelled.
/// Enables detailed diagnostics for hierarchical cancellation scenarios.
/// </summary>
public enum CancellationReason
{
    /// <summary>
    /// The signal was not cancelled.
    /// </summary>
    None,

    /// <summary>
    /// The signal was cancelled due to the global timeout being reached with <see cref="IgnitionOptions.CancelOnGlobalTimeout"/> enabled.
    /// </summary>
    GlobalTimeout,

    /// <summary>
    /// The signal was cancelled due to its per-signal timeout being reached with <see cref="IgnitionOptions.CancelIndividualOnTimeout"/> enabled.
    /// </summary>
    PerSignalTimeout,

    /// <summary>
    /// The signal was cancelled because a dependency failed and <see cref="IgnitionOptions.CancelDependentsOnFailure"/> is enabled.
    /// </summary>
    DependencyFailed,

    /// <summary>
    /// The signal was cancelled because its parent cancellation scope was cancelled.
    /// </summary>
    ScopeCancelled,

    /// <summary>
    /// The signal was cancelled because another signal in the same bundle failed or timed out.
    /// </summary>
    BundleCancelled,

    /// <summary>
    /// The signal was cancelled due to an external cancellation token being triggered.
    /// </summary>
    ExternalCancellation
}
