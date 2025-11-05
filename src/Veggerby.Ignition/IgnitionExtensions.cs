using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Veggerby.Ignition;

/// <summary>
/// Extension methods for registering ignition (startup readiness) services and signals with the dependency injection container.
/// </summary>
public static class IgnitionExtensions
{
    /// <summary>
    /// Registers the ignition coordinator and its supporting services.
    /// Optionally applies configuration via the provided delegate.
    /// </summary>
    /// <param name="services">The service collection to modify.</param>
    /// <param name="configure">Optional delegate to configure <see cref="IgnitionOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddIgnition(
        this IServiceCollection services,
        Action<IgnitionOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IIgnitionCoordinator, IgnitionCoordinator>();

        // Health check is safe to add even if not consumed elsewhere.
        services.AddHealthChecks().AddCheck<IgnitionHealthCheck>("ignition-readiness");

        return services;
    }

    /// <summary>
    /// Registers a single ignition signal instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="signal">The ignition signal to register.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIgnitionSignal(
        this IServiceCollection services,
        IIgnitionSignal signal)
    {
        services.AddSingleton(signal);
        return services;
    }

    /// <summary>
    /// Registers a single ignition signal type to be constructed by DI.
    /// </summary>
    /// <typeparam name="TSignal">Concrete ignition signal type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIgnitionSignal<TSignal>(
        this IServiceCollection services) where TSignal : class, IIgnitionSignal
    {
        services.AddSingleton<IIgnitionSignal, TSignal>();
        return services;
    }

    /// <summary>
    /// Registers multiple ignition signals provided as an enumerable.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="signals">Collection of ignition signals.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIgnitionSignals(
        this IServiceCollection services,
        IEnumerable<IIgnitionSignal> signals)
    {
        foreach (var s in signals)
        {
            services.AddSingleton(s);
        }

        return services;
    }

    /// <summary>
    /// Registers multiple ignition signals provided as params.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="signals">Array of ignition signals.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIgnitionSignals(
        this IServiceCollection services,
        params IIgnitionSignal[] signals)
        => services.AddIgnitionSignals((IEnumerable<IIgnitionSignal>)signals);

    /// <summary>
    /// Convenience method that wraps an existing readiness <see cref="Task"/> as an ignition signal.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Name of the signal for diagnostics.</param>
    /// <param name="readyTask">Task representing readiness completion.</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIgnitionTask(
        this IServiceCollection services,
        string name,
        Task readyTask,
        TimeSpan? timeout = null)
        => services.AddIgnitionSignal(IgnitionSignal.FromTask(name, readyTask, timeout));

    /// <summary>
    /// Convenience method that wraps a cancellable Task factory as an ignition signal, invoking it lazily and at most once.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Name of the signal for diagnostics.</param>
    /// <param name="readyTaskFactory">Factory producing the readiness task; receives a cancellation token.</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddIgnitionTask(
        this IServiceCollection services,
        string name,
        Func<CancellationToken, Task> readyTaskFactory,
        TimeSpan? timeout = null)
        => services.AddIgnitionSignal(IgnitionSignal.FromTaskFactory(name, readyTaskFactory, timeout));
}