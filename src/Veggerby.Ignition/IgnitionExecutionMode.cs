namespace Veggerby.Ignition;

/// <summary>
/// Determines how ignition signals are scheduled.
/// </summary>
public enum IgnitionExecutionMode
{
    /// <summary>
    /// All signals are awaited concurrently (default).
    /// </summary>
    Parallel,
    /// <summary>
    /// Signals are awaited one after another in registration order.
    /// Useful when initialization steps depend on prior ones or to reduce startup resource spikes.
    /// </summary>
    Sequential
}
