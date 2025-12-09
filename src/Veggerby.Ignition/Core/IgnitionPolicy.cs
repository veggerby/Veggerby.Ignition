#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Determines how the application reacts to ignition (startup readiness) failures or timeouts.
/// </summary>
public enum IgnitionPolicy
{
    /// <summary>
    /// Abort startup if any signal fails; aggregated exceptions may be thrown.
    /// </summary>
    FailFast,

    /// <summary>
    /// Log failures but proceed with startup unless the global timeout policy dictates otherwise.
    /// </summary>
    BestEffort,

    /// <summary>
    /// Continue immediately upon reaching the global timeout, logging incomplete signals.
    /// </summary>
    ContinueOnTimeout
}
