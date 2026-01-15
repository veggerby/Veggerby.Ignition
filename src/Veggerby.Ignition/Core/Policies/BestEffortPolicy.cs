#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Built-in policy that continues execution regardless of signal failures or timeouts.
/// </summary>
/// <remarks>
/// <para>
/// This policy implements best-effort semantics: execution continues even when signals fail or time out,
/// allowing all signals to complete before ignition finalizes.
/// </para>
/// <para>
/// This policy corresponds to the <see cref="IgnitionPolicy.BestEffort"/> enum value and maintains backward compatibility
/// with existing best-effort behavior.
/// </para>
/// <para>
/// Use this policy when you want maximum observability of all signal outcomes, even in the presence of failures.
/// Failed signals are logged but do not prevent remaining signals from executing.
/// </para>
/// </remarks>
public sealed class BestEffortPolicy : IIgnitionPolicy
{
    /// <summary>
    /// Determines whether execution should continue after a signal completes.
    /// </summary>
    /// <param name="context">Context containing signal result and global state.</param>
    /// <returns>
    /// Always returns <c>true</c>, continuing execution regardless of signal status.
    /// </returns>
    /// <remarks>
    /// This policy always returns <c>true</c>, allowing execution to continue regardless of the signal's outcome.
    /// Even if a signal fails, times out, or is cancelled, remaining signals will still execute.
    /// </remarks>
    public bool ShouldContinue(IgnitionPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        return true;
    }
}
