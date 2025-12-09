#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a pluggable strategy for determining timeout behavior for individual ignition signals.
/// Implementations can provide custom timeout logic based on signal properties, options, or external context.
/// </summary>
/// <remarks>
/// <para>
/// Timeout strategies enable advanced scenarios such as:
/// <list type="bullet">
///   <item>Exponential scaling based on failure count</item>
///   <item>Adaptive timeouts (e.g., slow I/O detection)</item>
///   <item>Dynamic per-stage deadlines</item>
///   <item>User-defined per-class or per-assembly defaults</item>
/// </list>
/// </para>
/// <para>
/// Implementations must be deterministic and thread-safe as they may be invoked concurrently
/// for multiple signals. The strategy is consulted once per signal evaluation; results are
/// not cached by the coordinator.
/// </para>
/// </remarks>
public interface IIgnitionTimeoutStrategy
{
    /// <summary>
    /// Determines the timeout and cancellation behavior for a specific ignition signal.
    /// </summary>
    /// <param name="signal">The ignition signal being evaluated.</param>
    /// <param name="options">The current ignition options providing global configuration context.</param>
    /// <returns>
    /// A tuple containing:
    /// <list type="bullet">
    ///   <item><c>signalTimeout</c>: The timeout duration for this signal, or <c>null</c> to indicate no per-signal timeout.</item>
    ///   <item><c>cancelImmediately</c>: When <c>true</c>, the signal's underlying task is cancelled upon timeout;
    ///   when <c>false</c>, the timeout is classified without forcing cancellation.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para>
    /// The returned <c>signalTimeout</c> takes precedence over any timeout configured on the signal itself
    /// (via <see cref="IIgnitionSignal.Timeout"/>). Return the signal's own timeout to preserve default behavior.
    /// </para>
    /// <para>
    /// The <c>cancelImmediately</c> return value provides per-signal cancellation control.
    /// Returning <c>true</c> cancels the signal's task immediately upon timeout;
    /// <c>false</c> allows the task to continue running while classifying the result as timed out.
    /// </para>
    /// </remarks>
    (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options);
}
