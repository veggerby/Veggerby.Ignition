using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.RabbitMq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating RabbitMQ readiness signals with configurable connection strings.
/// </summary>
public sealed class RabbitMqReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly RabbitMqReadinessOptions _options;

    /// <summary>
    /// Creates a new RabbitMQ readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">RabbitMQ readiness options.</param>
    public RabbitMqReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        RabbitMqReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "rabbitmq-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => null;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var connectionFactory = new ConnectionFactory { Uri = new Uri(connectionString) };
        var logger = serviceProvider.GetRequiredService<ILogger<RabbitMqReadinessSignal>>();
        
        return new RabbitMqReadinessSignal(connectionFactory, _options, logger);
    }
}
