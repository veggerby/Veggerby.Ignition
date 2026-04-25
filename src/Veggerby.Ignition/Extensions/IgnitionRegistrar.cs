using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Default implementation of <see cref="IIgnitionRegistrar"/> that delegates to
/// the <see cref="IgnitionExtensions"/> methods on an underlying <see cref="IServiceCollection"/>.
/// </summary>
internal sealed class IgnitionRegistrar : IIgnitionRegistrar
{
    private readonly IServiceCollection _services;

    public IgnitionRegistrar(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        _services = services;
    }

    /// <inheritdoc/>
    public IIgnitionRegistrar AddSignal(string name, Func<CancellationToken, Task> taskFactory, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(taskFactory, nameof(taskFactory));

        _services.AddIgnitionFromTask(name, taskFactory, timeout);
        return this;
    }

    /// <inheritdoc/>
    public IIgnitionRegistrar AddSignal(string name, Task readyTask, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(readyTask, nameof(readyTask));

        _services.AddIgnitionFromTask(name, readyTask, timeout);
        return this;
    }

    /// <inheritdoc/>
    public IIgnitionRegistrar AddSignal(IIgnitionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal, nameof(signal));

        _services.AddIgnitionSignal(signal);
        return this;
    }

    /// <inheritdoc/>
    public IIgnitionRegistrar AddSignal<TSignal>() where TSignal : class, IIgnitionSignal
    {
        _services.AddIgnitionSignal<TSignal>();
        return this;
    }

    /// <inheritdoc/>
    public IIgnitionRegistrar AddSignalFactory(IIgnitionSignalFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        _services.AddSingleton<IIgnitionSignalFactory>(factory);
        return this;
    }
}
