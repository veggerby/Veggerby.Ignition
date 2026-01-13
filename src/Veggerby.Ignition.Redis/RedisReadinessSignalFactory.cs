using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Redis;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating Redis readiness signals with configurable connection strings.
/// </summary>
public sealed class RedisReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly RedisReadinessOptions _options;

    /// <summary>
    /// Creates a new Redis readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">Redis readiness options.</param>
    public RedisReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        RedisReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "redis-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var multiplexer = ConnectionMultiplexer.Connect(connectionString);
        var logger = serviceProvider.GetRequiredService<ILogger<RedisReadinessSignal>>();
        
        return new RedisReadinessSignal(multiplexer, _options, logger);
    }
}
