#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for a specific ignition bundle, enabling per-bundle timeout, policy,
/// and cancellation scope behavior overrides.
/// </summary>
/// <remarks>
/// Bundle-level options are applied to all signals registered by the bundle unless a signal explicitly
/// specifies its own timeout via <see cref="IIgnitionSignal.Timeout"/>. This enables bundle authors to
/// provide sensible defaults while preserving user override capability.
/// </remarks>
public sealed class IgnitionBundleOptions
{
    /// <summary>
    /// Optional per-bundle timeout applied to all signals within this bundle unless overridden by individual signals.
    /// </summary>
    /// <remarks>
    /// If <c>null</c>, signals inherit from the global <see cref="IgnitionOptions.GlobalTimeout"/>.
    /// If a signal within the bundle specifies <see cref="IIgnitionSignal.Timeout"/>, that takes precedence.
    /// </remarks>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Optional per-bundle policy override influencing failure or timeout behavior for this bundle's signals.
    /// </summary>
    /// <remarks>
    /// Currently not enforced by coordinator logic (coordinator uses global policy).
    /// Reserved for future enhancement supporting per-bundle policy isolation or staged execution semantics.
    /// </remarks>
    public IgnitionPolicy? Policy { get; set; }

    /// <summary>
    /// When <c>true</c>, signals registered by this bundle share a common cancellation scope.
    /// If any signal in the bundle fails or times out, all remaining signals in the bundle are cancelled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This enables grouped cancellation semantics for bundles. For example, a "Redis Starter Bundle"
    /// might contain connection, ping, and warmup signals. If the connection signal fails, there's no
    /// point continuing with ping and warmup - they should be cancelled immediately.
    /// </para>
    /// <para>
    /// Default is <c>false</c> to maintain backward compatibility.
    /// </para>
    /// </remarks>
    public bool EnableScopedCancellation { get; set; }

    /// <summary>
    /// Gets or sets the explicit cancellation scope for this bundle.
    /// When set, all signals registered by this bundle will use this scope.
    /// </summary>
    /// <remarks>
    /// This allows advanced scenarios where bundles share cancellation scopes with other bundles
    /// or integrate into a larger cancellation hierarchy. If <c>null</c> and <see cref="EnableScopedCancellation"/>
    /// is <c>true</c>, a new scope is created automatically for the bundle.
    /// </remarks>
    public ICancellationScope? CancellationScope { get; set; }
}
