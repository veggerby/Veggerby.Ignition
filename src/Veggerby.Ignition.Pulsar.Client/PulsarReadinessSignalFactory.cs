using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Pulsar.Client;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating Pulsar readiness signals with configurable service URL.
/// </summary>
public sealed class PulsarReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _serviceUrlFactory;
    private readonly PulsarReadinessOptions _options;

    /// <summary>
    /// Creates a new Pulsar readiness signal factory.
    /// </summary>
    /// <param name="serviceUrlFactory">Factory that produces the service URL using the service provider.</param>
    /// <param name="options">Pulsar readiness options.</param>
    public PulsarReadinessSignalFactory(
        Func<IServiceProvider, string> serviceUrlFactory,
        PulsarReadinessOptions options)
    {
        _serviceUrlFactory = serviceUrlFactory ?? throw new ArgumentNullException(nameof(serviceUrlFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "pulsar-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var serviceUrl = _serviceUrlFactory(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<PulsarReadinessSignal>>();
        
        return new PulsarReadinessSignal(serviceUrl, _options, logger);
    }
}
