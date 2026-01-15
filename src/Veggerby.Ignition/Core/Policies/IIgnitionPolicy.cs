#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines a policy for determining whether ignition should continue after a signal completes.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface to create custom failure handling strategies beyond the built-in policies
/// (FailFast, BestEffort, ContinueOnTimeout). Custom policies enable domain-specific logic such as
/// retry strategies, circuit breakers, conditional fail-fast, and percentage-based thresholds.
/// </para>
/// <para>
/// The <see cref="ShouldContinue"/> method is invoked after each signal completes during execution.
/// Returning <c>false</c> stops execution immediately and finalizes ignition with the current results.
/// </para>
/// <para>
/// Custom policies should be deterministic: given the same context, they should return the same decision.
/// Avoid introducing randomness or nondeterministic behavior that could affect ignition reproducibility.
/// </para>
/// </remarks>
public interface IIgnitionPolicy
{
    /// <summary>
    /// Determines whether execution should continue after a signal completes.
    /// </summary>
    /// <param name="context">Context containing signal result and global state.</param>
    /// <returns>
    /// <c>true</c> to continue executing remaining signals;
    /// <c>false</c> to stop execution immediately and finalize ignition.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is called after each signal completes (succeeds, fails, or times out).
    /// The <paramref name="context"/> provides access to:
    /// <list type="bullet">
    ///   <item>The current signal's result (<see cref="IgnitionPolicyContext.SignalResult"/>)</item>
    ///   <item>All previously completed signals (<see cref="IgnitionPolicyContext.CompletedSignals"/>)</item>
    ///   <item>Total signal count (<see cref="IgnitionPolicyContext.TotalSignalCount"/>)</item>
    ///   <item>Elapsed time (<see cref="IgnitionPolicyContext.ElapsedTime"/>)</item>
    ///   <item>Global timeout status (<see cref="IgnitionPolicyContext.GlobalTimeoutElapsed"/>)</item>
    ///   <item>Execution mode (<see cref="IgnitionPolicyContext.ExecutionMode"/>)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Implementations should be efficient as this method is called frequently during execution.
    /// Avoid expensive I/O operations or blocking calls.
    /// </para>
    /// </remarks>
    bool ShouldContinue(IgnitionPolicyContext context);
}
