using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a filter that is consulted before each ignition signal executes.
/// </summary>
/// <remarks>
/// <para>
/// Signal filters allow external logic to intercept and conditionally suppress signal execution.
/// Common uses:
/// <list type="bullet">
///   <item>Skip signals whose dependencies are absent in a given environment (e.g., skip Redis checks if Redis is disabled via configuration).</item>
///   <item>Inject pre-execution tracing, logging, or metrics before the signal's own work begins.</item>
///   <item>Enforce security or compliance requirements before allowing a signal to run.</item>
/// </list>
/// </para>
/// <para>
/// Filters are evaluated in the order they are registered. If <em>any</em> filter returns
/// <c>false</c> from <see cref="ShouldExecuteAsync"/>, the signal is skipped with status
/// <see cref="IgnitionSignalStatus.Skipped"/> and subsequent filters for that signal are not called.
/// </para>
/// <para>
/// Implementations must be thread-safe; they may be invoked concurrently for multiple signals.
/// Filters should be lightweight and should <em>not</em> perform the same I/O as the signal itself.
/// </para>
/// </remarks>
public interface IIgnitionSignalFilter
{
    /// <summary>
    /// Determines whether the specified signal should execute.
    /// </summary>
    /// <param name="signal">The signal that is about to execute.</param>
    /// <param name="cancellationToken">Cancellation token linked to the global ignition context.</param>
    /// <returns>
    /// <c>true</c> to allow the signal to run; <c>false</c> to skip the signal
    /// (recording it with <see cref="IgnitionSignalStatus.Skipped"/>).
    /// </returns>
    ValueTask<bool> ShouldExecuteAsync(IIgnitionSignal signal, CancellationToken cancellationToken);
}
