namespace Veggerby.Ignition.Stages;

/// <summary>
/// Determines how stage transitions are handled during staged execution.
/// Controls when the coordinator moves from one stage to the next based on signal outcomes.
/// </summary>
public enum IgnitionStagePolicy
{
    /// <summary>
    /// All signals in the current stage must succeed before proceeding to the next stage (default).
    /// If any signal fails or times out, subsequent stages are not executed.
    /// </summary>
    AllMustSucceed,

    /// <summary>
    /// The next stage starts when all signals in the current stage have completed,
    /// regardless of individual success/failure status.
    /// Failures are logged but do not block progression.
    /// </summary>
    BestEffort,

    /// <summary>
    /// Stop immediately if any signal in any stage fails.
    /// Remaining signals in the current stage continue but subsequent stages are skipped.
    /// </summary>
    FailFast,

    /// <summary>
    /// Move to the next stage when the early promotion threshold is met.
    /// Use with <see cref="IgnitionOptions.EarlyPromotionThreshold"/> to configure the required success percentage.
    /// Remaining signals in the current stage continue executing but don't block progression.
    /// </summary>
    EarlyPromotion
}
