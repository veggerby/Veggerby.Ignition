using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;

namespace Veggerby.Ignition.Orleans;

/// <summary>
/// Factory for creating Orleans readiness signals with dynamic client configuration.
/// </summary>
public sealed class OrleansReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly OrleansReadinessOptions _options;

    /// <summary>
    /// Creates a new Orleans readiness signal factory.
    /// </summary>
    /// <param name="options">Orleans readiness options.</param>
    public OrleansReadinessSignalFactory(OrleansReadinessOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "orleans-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var clusterClient = serviceProvider.GetRequiredService<IClusterClient>();
        var logger = serviceProvider.GetRequiredService<ILogger<OrleansReadinessSignal>>();
        
        return new OrleansReadinessSignal(clusterClient, _options, logger);
    }
}
