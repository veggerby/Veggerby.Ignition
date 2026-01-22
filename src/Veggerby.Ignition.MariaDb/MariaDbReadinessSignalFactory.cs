using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Veggerby.Ignition.MariaDb;

/// <summary>
/// Factory for creating MariaDB readiness signals with configurable connection strings.
/// </summary>
public sealed class MariaDbReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly MariaDbReadinessOptions _options;

    /// <summary>
    /// Creates a new MariaDB readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">MariaDB readiness options.</param>
    public MariaDbReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        MariaDbReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "mariadb-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<MariaDbReadinessSignal>>();

        return new MariaDbReadinessSignal(connectionString, _options, logger);
    }
}
