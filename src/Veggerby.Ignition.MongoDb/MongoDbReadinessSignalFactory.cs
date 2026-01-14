using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MongoDb;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating MongoDB readiness signals with configurable connection strings.
/// </summary>
public sealed class MongoDbReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly MongoDbReadinessOptions _options;

    /// <summary>
    /// Creates a new MongoDB readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">MongoDB readiness options.</param>
    public MongoDbReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        MongoDbReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "mongodb-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var client = new MongoClient(connectionString);
        var logger = serviceProvider.GetRequiredService<ILogger<MongoDbReadinessSignal>>();
        
        return new MongoDbReadinessSignal(client, _options, logger);
    }
}
