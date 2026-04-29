using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a pre-flight validation check that runs before any ignition signal is executed.
/// </summary>
/// <remarks>
/// <para>
/// Validators are invoked at the start of coordinator execution, before any signals are started.
/// If any validator produces errors, the coordinator throws a <see cref="IgnitionValidationException"/>
/// and no signals are executed.
/// </para>
/// <para>
/// Typical uses:
/// <list type="bullet">
///   <item>Verify required environment variables or configuration values are present.</item>
///   <item>Check for duplicate signal names or conflicting registrations.</item>
///   <item>Validate that mandatory signals for the current environment are registered.</item>
///   <item>Assert minimum .NET runtime version or dependency versions.</item>
/// </list>
/// </para>
/// <para>
/// Validators receive <see cref="IIgnitionSignalFactory"/> descriptors rather than fully-constructed
/// <see cref="IIgnitionSignal"/> instances. This avoids triggering side effects (e.g., connection attempts,
/// resource allocation) that signal constructors may have, and ensures validation operates on the same
/// factory descriptors that the coordinator will use during execution.
/// </para>
/// <para>
/// Validators should be fast, deterministic, and stateless. Avoid performing I/O in a validator;
/// use <see cref="IIgnitionSignal"/> for I/O-based readiness checks instead.
/// </para>
/// </remarks>
public interface IIgnitionValidator
{
    /// <summary>
    /// Validates the ignition configuration before any signals execute.
    /// </summary>
    /// <param name="factories">The complete list of registered signal factories (descriptors), in registration order.</param>
    /// <param name="options">The resolved ignition options.</param>
    /// <param name="cancellationToken">Cancellation token for the validation operation.</param>
    /// <returns>
    /// A read-only list of validation error messages. An empty list (or <c>null</c>) indicates
    /// that validation passed. A non-empty list causes the coordinator to throw a
    /// <see cref="IgnitionValidationException"/> before executing any signals.
    /// </returns>
    ValueTask<IReadOnlyList<string>?> ValidateAsync(
        IReadOnlyList<IIgnitionSignalFactory> factories,
        IgnitionOptions options,
        CancellationToken cancellationToken);
}
