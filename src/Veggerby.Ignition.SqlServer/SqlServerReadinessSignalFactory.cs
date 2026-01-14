using System;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.SqlServer;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating SQL Server readiness signals with configurable connection strings.
/// </summary>
public sealed class SqlServerReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly SqlServerReadinessOptions _options;

    /// <summary>
    /// Creates a new SQL Server readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">SQL Server readiness options.</param>
    public SqlServerReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        SqlServerReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "sqlserver-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<SqlServerReadinessSignal>>();
        
        return new SqlServerReadinessSignal(connectionString, _options, logger);
    }
}
