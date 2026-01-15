#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Built-in policy that stops execution immediately if any signal fails.
/// </summary>
/// <remarks>
/// <para>
/// This policy implements fail-fast semantics: as soon as a signal fails (status is <see cref="IgnitionSignalStatus.Failed"/>),
/// execution stops and remaining signals are not evaluated.
/// </para>
/// <para>
/// This policy corresponds to the <see cref="IgnitionPolicy.FailFast"/> enum value and maintains backward compatibility
/// with existing fail-fast behavior.
/// </para>
/// <para>
/// Timeouts are not considered failures by this policy. To stop on both failures and timeouts,
/// use a custom policy or <see cref="ContinueOnTimeoutPolicy"/> (which treats failures as stop conditions but allows timeouts).
/// </para>
/// </remarks>
public sealed class FailFastPolicy : IIgnitionPolicy
{
    /// <summary>
    /// Determines whether execution should continue after a signal completes.
    /// </summary>
    /// <param name="context">Context containing signal result and global state.</param>
    /// <returns>
    /// <c>true</c> if the signal succeeded; <c>false</c> if the signal failed.
    /// </returns>
    /// <remarks>
    /// This policy returns <c>false</c> only when <see cref="IgnitionSignalResult.Status"/> is <see cref="IgnitionSignalStatus.Failed"/>.
    /// All other statuses (Succeeded, TimedOut, Skipped, Cancelled) are treated as non-failure and return <c>true</c>.
    /// </remarks>
    public bool ShouldContinue(IgnitionPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return context.SignalResult.Status == IgnitionSignalStatus.Succeeded;
    }
}
