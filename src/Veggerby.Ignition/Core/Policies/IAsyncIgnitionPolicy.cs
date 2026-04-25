using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extends <see cref="IIgnitionPolicy"/> with an asynchronous continuation-decision method.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface when the policy decision requires asynchronous work, for example:
/// <list type="bullet">
///   <item>Consulting a remote circuit breaker before continuing.</item>
///   <item>Publishing policy decisions to an event bus for external monitoring.</item>
///   <item>Recording structured metrics with an async sink before deciding to stop.</item>
/// </list>
/// </para>
/// <para>
/// The coordinator will call <see cref="ShouldContinueAsync"/> in preference over
/// <see cref="IIgnitionPolicy.ShouldContinue"/> whenever the policy also implements this interface.
/// Implementations should still provide a correct synchronous fallback via
/// <see cref="IIgnitionPolicy.ShouldContinue"/> for environments or callers that cannot await.
/// </para>
/// <para>
/// Implementations must be thread-safe; they may be invoked concurrently for multiple signals
/// in parallel execution mode.
/// </para>
/// </remarks>
public interface IAsyncIgnitionPolicy : IIgnitionPolicy
{
    /// <summary>
    /// Asynchronously determines whether execution should continue after a signal completes.
    /// </summary>
    /// <param name="context">Context containing the current signal result and overall ignition state.</param>
    /// <param name="cancellationToken">Cancellation token linked to the global ignition context.</param>
    /// <returns>
    /// <c>true</c> to continue executing remaining signals;
    /// <c>false</c> to stop execution immediately and finalize ignition.
    /// </returns>
    ValueTask<bool> ShouldContinueAsync(IgnitionPolicyContext context, CancellationToken cancellationToken);
}
