#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Default implementation of <see cref="IIgnitionTimeoutStrategy"/> that applies the standard timeout behavior:
/// uses the signal's own <see cref="IIgnitionSignal.Timeout"/> if specified, and respects the global
/// <see cref="IgnitionOptions.CancelIndividualOnTimeout"/> setting for cancellation.
/// </summary>
/// <remarks>
/// This strategy preserves backward-compatible behavior with the original timeout handling.
/// It is registered by default when no custom strategy is provided.
/// </remarks>
public sealed class DefaultIgnitionTimeoutStrategy : IIgnitionTimeoutStrategy
{
    /// <summary>
    /// Singleton instance of the default timeout strategy.
    /// </summary>
    public static readonly DefaultIgnitionTimeoutStrategy Instance = new();

    private DefaultIgnitionTimeoutStrategy()
    {
    }

    /// <summary>
    /// Returns the signal's configured timeout and the global cancellation setting.
    /// </summary>
    /// <param name="signal">The ignition signal being evaluated.</param>
    /// <param name="options">The current ignition options providing global configuration context.</param>
    /// <returns>
    /// A tuple containing the signal's <see cref="IIgnitionSignal.Timeout"/> (or <c>null</c> if not specified)
    /// and the value of <see cref="IgnitionOptions.CancelIndividualOnTimeout"/>.
    /// </returns>
    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(signal, nameof(signal));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        return (signal.Timeout, options.CancelIndividualOnTimeout);
    }
}
