using System.Threading;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a hierarchical cancellation scope that can be used to group related ignition signals.
/// Cancellation scopes enable structured cancellation trees where child scopes inherit cancellation
/// from parent scopes, and cancellation of any signal can optionally propagate to siblings within the same scope.
/// </summary>
/// <remarks>
/// <para>
/// Cancellation scopes provide fine-grained control over shutdown cascades and failure propagation:
/// <list type="bullet">
///   <item>Cancel a whole stage by cancelling the stage's scope</item>
///   <item>Cancel all signals dependent on a failed signal</item>
///   <item>Cancel all signals sharing a bundle when one fails</item>
/// </list>
/// </para>
/// <para>
/// The scope creates a linked <see cref="CancellationTokenSource"/> that inherits cancellation from
/// its parent scope (if any). When the scope is cancelled, all child scopes and associated signals
/// receive the cancellation signal.
/// </para>
/// </remarks>
public interface ICancellationScope : IDisposable
{
    /// <summary>
    /// Gets the unique name identifying this cancellation scope.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the parent scope, or <c>null</c> if this is a root scope.
    /// </summary>
    ICancellationScope? Parent { get; }

    /// <summary>
    /// Gets the cancellation token associated with this scope.
    /// This token is cancelled when the scope is cancelled or when any parent scope is cancelled.
    /// </summary>
    CancellationToken Token { get; }

    /// <summary>
    /// Gets whether this scope has been cancelled.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Gets the reason why this scope was cancelled, or <see cref="CancellationReason.None"/> if not cancelled.
    /// </summary>
    CancellationReason CancellationReason { get; }

    /// <summary>
    /// Gets the name of the signal that triggered the cancellation, if applicable.
    /// </summary>
    string? TriggeringSignalName { get; }

    /// <summary>
    /// Cancels this scope with the specified reason and triggering signal name.
    /// All signals using this scope's token will receive the cancellation signal.
    /// </summary>
    /// <param name="reason">The reason for cancellation.</param>
    /// <param name="triggeringSignalName">Optional name of the signal that triggered the cancellation.</param>
    void Cancel(CancellationReason reason, string? triggeringSignalName = null);

    /// <summary>
    /// Creates a child scope that inherits cancellation from this scope.
    /// </summary>
    /// <param name="name">The name for the child scope.</param>
    /// <returns>A new child scope linked to this scope.</returns>
    ICancellationScope CreateChildScope(string name);
}
