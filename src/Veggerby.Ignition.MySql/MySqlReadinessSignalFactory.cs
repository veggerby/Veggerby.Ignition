using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.MySql;

/// <summary>
/// Factory for creating MySQL readiness signals with configurable connection strings.
/// </summary>
public sealed class MySqlReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly MySqlReadinessOptions _options;

    /// <summary>
    /// Creates a new MySQL readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">MySQL readiness options.</param>
    public MySqlReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        MySqlReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "mysql-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<MySqlReadinessSignal>>();

        return new MySqlReadinessSignal(connectionString, _options, logger);
    }
}
