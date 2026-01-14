using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Default implementation of <see cref="IIgnitionSignalFactory"/> that creates signals using a factory delegate.
/// </summary>
internal sealed class DelegateIgnitionSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, IIgnitionSignal> _factory;

    public DelegateIgnitionSignalFactory(string name, Func<IServiceProvider, IIgnitionSignal> factory, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        Name = name;
        _factory = factory;
        Timeout = timeout;
    }

    public string Name { get; }
    public TimeSpan? Timeout { get; }
    public int? Stage => null; // Non-staged factory

    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));
        return _factory(serviceProvider);
    }
}
