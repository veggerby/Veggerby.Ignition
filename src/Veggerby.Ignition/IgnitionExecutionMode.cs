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
    Sequential,
    /// <summary>
    /// Signals are awaited based on their dependency relationships defined in an <see cref="IIgnitionGraph"/>.
    /// Signals with no dependencies start immediately; dependent signals start only after their prerequisites complete.
    /// Independent branches execute in parallel automatically.
    /// Requires an <see cref="IIgnitionGraph"/> to be registered in the DI container.
    /// </summary>
    DependencyAware
}
