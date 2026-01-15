#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Built-in policy that continues execution when signals time out but stops on failures.
/// </summary>
/// <remarks>
/// <para>
/// This policy implements timeout-tolerant semantics: execution continues when signals time out
/// (status is <see cref="IgnitionSignalStatus.TimedOut"/>) but stops immediately when a signal fails
/// (status is <see cref="IgnitionSignalStatus.Failed"/>).
/// </para>
/// <para>
/// This policy corresponds to the <see cref="IgnitionPolicy.ContinueOnTimeout"/> enum value and maintains backward compatibility
/// with existing continue-on-timeout behavior.
/// </para>
/// <para>
/// Use this policy when timeouts are acceptable (e.g., optional health checks or background initialization tasks)
/// but actual failures are critical and should halt startup.
/// </para>
/// </remarks>
public sealed class ContinueOnTimeoutPolicy : IIgnitionPolicy
{
    /// <summary>
    /// Determines whether execution should continue after a signal completes.
    /// </summary>
    /// <param name="context">Context containing signal result and global state.</param>
    /// <returns>
    /// <c>true</c> if the signal did not fail (succeeded, timed out, skipped, or cancelled);
    /// <c>false</c> if the signal failed.
    /// </returns>
    /// <remarks>
    /// This policy returns <c>false</c> only when <see cref="IgnitionSignalResult.Status"/> is <see cref="IgnitionSignalStatus.Failed"/>.
    /// All other statuses (Succeeded, TimedOut, Skipped, Cancelled) are treated as non-critical and return <c>true</c>.
    /// </remarks>
    public bool ShouldContinue(IgnitionPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return context.SignalResult.Status != IgnitionSignalStatus.Failed;
    }
}
