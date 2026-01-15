using System;
using Enyim.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Memcached;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating Memcached readiness signals with dynamic client configuration.
/// </summary>
public sealed class MemcachedReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly MemcachedReadinessOptions _options;

    /// <summary>
    /// Creates a new Memcached readiness signal factory.
    /// </summary>
    /// <param name="options">Memcached readiness options.</param>
    public MemcachedReadinessSignalFactory(MemcachedReadinessOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "memcached-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var memcachedClient = serviceProvider.GetRequiredService<IMemcachedClient>();
        var logger = serviceProvider.GetRequiredService<ILogger<MemcachedReadinessSignal>>();
        
        return new MemcachedReadinessSignal(memcachedClient, _options, logger);
    }
}
