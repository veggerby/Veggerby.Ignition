using System;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Provides a narrowly-scoped API for registering ignition signals inside a bundle.
/// </summary>
/// <remarks>
/// <para>
/// Bundles receive an <see cref="IIgnitionRegistrar"/> rather than the raw
/// <see cref="Microsoft.Extensions.DependencyInjection.IServiceCollection"/> to prevent them from
/// registering arbitrary, unrelated services. This enforces the single-responsibility principle:
/// a bundle is a packaged set of ignition signals, nothing more.
/// </para>
/// <para>
/// All overloads mirror the corresponding
/// <see cref="IgnitionExtensions"/> methods with the same semantics.
/// </para>
/// </remarks>
public interface IIgnitionRegistrar
{
    /// <summary>
    /// Registers a named signal from a task factory.
    /// </summary>
    /// <param name="name">Unique signal name for diagnostics and reporting.</param>
    /// <param name="taskFactory">Factory producing the readiness task when first awaited.</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global option.</param>
    /// <returns>The same registrar for fluent chaining.</returns>
    IIgnitionRegistrar AddSignal(string name, Func<CancellationToken, Task> taskFactory, TimeSpan? timeout = null);

    /// <summary>
    /// Registers a named signal from an already-created task.
    /// </summary>
    /// <param name="name">Unique signal name for diagnostics and reporting.</param>
    /// <param name="readyTask">Task that completes when the component is ready.</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global option.</param>
    /// <returns>The same registrar for fluent chaining.</returns>
    IIgnitionRegistrar AddSignal(string name, Task readyTask, TimeSpan? timeout = null);

    /// <summary>
    /// Registers a pre-built signal instance.
    /// </summary>
    /// <param name="signal">The signal to register.</param>
    /// <returns>The same registrar for fluent chaining.</returns>
    IIgnitionRegistrar AddSignal(IIgnitionSignal signal);

    /// <summary>
    /// Registers a signal by type, allowing the DI container to construct it.
    /// </summary>
    /// <typeparam name="TSignal">Concrete type implementing <see cref="IIgnitionSignal"/>.</typeparam>
    /// <returns>The same registrar for fluent chaining.</returns>
    IIgnitionRegistrar AddSignal<TSignal>() where TSignal : class, IIgnitionSignal;

    /// <summary>
    /// Registers a signal factory.
    /// </summary>
    /// <param name="factory">The signal factory to register.</param>
    /// <returns>The same registrar for fluent chaining.</returns>
    IIgnitionRegistrar AddSignalFactory(IIgnitionSignalFactory factory);
}
