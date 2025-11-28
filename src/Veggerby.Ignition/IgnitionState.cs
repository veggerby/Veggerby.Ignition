namespace Veggerby.Ignition;

/// <summary>
/// Represents the lifecycle state of the ignition coordinator.
/// </summary>
public enum IgnitionState
{
    /// <summary>
    /// The coordinator has not yet started executing signals.
    /// </summary>
    NotStarted,

    /// <summary>
    /// The coordinator is currently executing signals.
    /// </summary>
    Running,

    /// <summary>
    /// All signals completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// One or more signals failed.
    /// </summary>
    Failed,

    /// <summary>
    /// The coordinator timed out before all signals could complete.
    /// </summary>
    TimedOut
}
