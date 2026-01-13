using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating ignition signals on demand.
/// Enables lazy instantiation of signals, allowing them to be created when actually needed
/// rather than during service registration.
/// </summary>
/// <remarks>
/// <para>
/// This interface enables proper dependency injection in staged execution scenarios where
/// earlier stages produce resources (e.g., connection strings from Testcontainers) that
/// later stages need to consume.
/// </para>
/// <para>
/// The factory receives the fully built service provider when creating the signal,
/// allowing it to resolve all dependencies including those registered or modified by earlier stages.
/// </para>
/// </remarks>
public interface IIgnitionSignalFactory
{
    /// <summary>
    /// Human-friendly name used for logging, diagnostics and health reporting.
    /// This is the name that will be used for the created signal.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// Creates the ignition signal using the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving dependencies.</param>
    /// <returns>The created ignition signal.</returns>
    /// <remarks>
    /// This method is called by the coordinator when the signal is first needed.
    /// The implementation should resolve all necessary dependencies from the service provider
    /// and return a fully configured signal instance.
    /// </remarks>
    IIgnitionSignal CreateSignal(IServiceProvider serviceProvider);
}
