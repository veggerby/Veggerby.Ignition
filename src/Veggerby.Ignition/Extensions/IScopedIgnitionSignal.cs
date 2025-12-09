#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extended interface for ignition signals that participate in hierarchical cancellation.
/// Signals implementing this interface can be associated with a cancellation scope,
/// enabling grouped cancellation semantics such as cancelling all signals in a bundle
/// when one fails.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IIgnitionSignal"/> to support hierarchical cancellation trees.
/// When a signal is associated with a scope:
/// <list type="bullet">
///   <item>The signal's execution uses the scope's cancellation token</item>
///   <item>If the scope is cancelled, the signal receives the cancellation</item>
///   <item>The signal can optionally trigger scope cancellation on failure</item>
/// </list>
/// </para>
/// <para>
/// Signals not implementing this interface continue to work with the flat cancellation model
/// (global timeout vs per-signal timeout).
/// </para>
/// </remarks>
public interface IScopedIgnitionSignal : IIgnitionSignal
{
    /// <summary>
    /// Gets the cancellation scope this signal belongs to, or <c>null</c> if the signal
    /// uses the default flat cancellation model.
    /// </summary>
    ICancellationScope? CancellationScope { get; }

    /// <summary>
    /// Gets whether this signal should trigger cancellation of its scope when it fails or times out.
    /// </summary>
    /// <remarks>
    /// When <c>true</c>, if this signal fails or times out, all other signals in the same scope
    /// (and child scopes) will be cancelled. This enables bundle-level failure propagation.
    /// Default is <c>false</c>.
    /// </remarks>
    bool CancelScopeOnFailure { get; }
}
