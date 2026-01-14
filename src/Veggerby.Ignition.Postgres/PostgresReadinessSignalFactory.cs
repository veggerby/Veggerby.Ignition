using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Postgres;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating PostgreSQL readiness signals with configurable connection strings.
/// </summary>
public sealed class PostgresReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly PostgresReadinessOptions _options;

    /// <summary>
    /// Creates a new PostgreSQL readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">PostgreSQL readiness options.</param>
    public PostgresReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        PostgresReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "postgres-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => null;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<PostgresReadinessSignal>>();
        
        return new PostgresReadinessSignal(connectionString, _options, logger);
    }
}
