using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines lifecycle hooks for observing ignition execution stages.
/// </summary>
/// <remarks>
/// <para>
/// Hooks are invoked in the following order:
/// <list type="number">
///   <item><see cref="OnBeforeIgnitionAsync"/> - Once before any signal executes</item>
///   <item><see cref="OnBeforeSignalAsync"/> - Before each individual signal</item>
///   <item><see cref="OnAfterSignalAsync"/> - After each individual signal completes</item>
///   <item><see cref="OnAfterIgnitionAsync"/> - Once after all signals complete (success, failure, or timeout)</item>
/// </list>
/// </para>
/// <para>
/// Hooks provide read-only observation points and cannot modify ignition behavior or results.
/// Exceptions thrown by hooks are caught and logged but do not affect ignition outcome.
/// All methods have default implementations returning <see cref="Task.CompletedTask"/> for optional implementation.
/// </para>
/// </remarks>
public interface IIgnitionLifecycleHooks
{
    /// <summary>
    /// Called once before any signals execute.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the hook operation (not tied to signal execution).</param>
    /// <returns>A task representing the hook's asynchronous operation.</returns>
    /// <remarks>
    /// This hook executes after the coordinator transitions to <see cref="IgnitionState.Running"/>
    /// but before any signal starts. Use for global setup, telemetry initialization, or logging.
    /// Exceptions are caught and logged without affecting execution.
    /// </remarks>
    Task OnBeforeIgnitionAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called once after all signals complete (success, failure, or timeout).
    /// </summary>
    /// <param name="result">The aggregated ignition result containing all signal outcomes.</param>
    /// <param name="cancellationToken">Cancellation token for the hook operation.</param>
    /// <returns>A task representing the hook's asynchronous operation.</returns>
    /// <remarks>
    /// This hook executes after all signals finish but before the coordinator transitions to its final state.
    /// Use for cleanup, final telemetry recording, or conditional logic based on overall outcome.
    /// Exceptions are caught and logged without affecting the final result classification.
    /// </remarks>
    Task OnAfterIgnitionAsync(IgnitionResult result, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called before each individual signal executes.
    /// </summary>
    /// <param name="signalName">Name of the signal about to execute.</param>
    /// <param name="cancellationToken">Cancellation token for the hook operation (linked to signal's execution context).</param>
    /// <returns>A task representing the hook's asynchronous operation.</returns>
    /// <remarks>
    /// This hook is invoked immediately before calling <see cref="IIgnitionSignal.WaitAsync"/>
    /// on each signal. The cancellation token is linked to the signal's execution context
    /// (global timeout and per-signal timeout if configured). Use for per-signal setup or logging.
    /// Exceptions are caught and logged without preventing the signal from executing.
    /// </remarks>
    Task OnBeforeSignalAsync(string signalName, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Called after each individual signal completes.
    /// </summary>
    /// <param name="result">The signal result containing status, duration, and optional exception.</param>
    /// <param name="cancellationToken">Cancellation token for the hook operation (not linked to signal execution).</param>
    /// <returns>A task representing the hook's asynchronous operation.</returns>
    /// <remarks>
    /// This hook is invoked immediately after a signal completes (succeeded, failed, timed out, or cancelled).
    /// Use for per-signal telemetry recording, cleanup, or conditional logging based on outcome.
    /// Exceptions are caught and logged without affecting the signal's recorded result.
    /// </remarks>
    Task OnAfterSignalAsync(IgnitionSignalResult result, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
