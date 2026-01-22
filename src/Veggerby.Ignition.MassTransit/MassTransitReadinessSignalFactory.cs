using System;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.MassTransit;

/// <summary>
/// Factory for creating MassTransit bus readiness signals with dynamic bus configuration.
/// </summary>
public sealed class MassTransitReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly MassTransitReadinessOptions _options;

    /// <summary>
    /// Creates a new MassTransit readiness signal factory.
    /// </summary>
    /// <param name="options">MassTransit readiness options.</param>
    public MassTransitReadinessSignalFactory(MassTransitReadinessOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "masstransit-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var bus = serviceProvider.GetRequiredService<IBus>();
        var logger = serviceProvider.GetRequiredService<ILogger<MassTransitReadinessSignal>>();
        
        return new MassTransitReadinessSignal(bus, _options, logger);
    }
}
