using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Veggerby.Ignition.Redis;

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
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var configOptions = ConfigurationOptions.Parse(connectionString);
        // Ensure resilient connection: retry until timeout instead of failing immediately
        configOptions.AbortOnConnectFail = false;
        configOptions.ConnectTimeout = _options.ConnectTimeout;
        var multiplexer = ConnectionMultiplexer.Connect(configOptions);
        var logger = serviceProvider.GetRequiredService<ILogger<RedisReadinessSignal>>();
        
        return new RedisReadinessSignal(multiplexer, _options, logger);
    }
}
