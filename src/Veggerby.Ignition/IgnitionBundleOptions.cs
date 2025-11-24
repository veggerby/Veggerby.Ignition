using System;

namespace Veggerby.Ignition;

/// <summary>
/// Configuration options for a specific ignition bundle, enabling per-bundle timeout and policy overrides.
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
}
