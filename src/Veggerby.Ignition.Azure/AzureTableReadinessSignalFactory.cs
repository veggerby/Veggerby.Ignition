using System;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Azure;

/// <summary>
/// Factory for creating Azure Table Storage readiness signals with configurable connection strings.
/// </summary>
public sealed class AzureTableReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly AzureTableReadinessOptions _options;

    /// <summary>
    /// Creates a new Azure Table Storage readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">Azure Table Storage readiness options.</param>
    public AzureTableReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        AzureTableReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "azure-table-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var client = new TableServiceClient(connectionString);
        var logger = serviceProvider.GetRequiredService<ILogger<AzureTableReadinessSignal>>();
        
        return new AzureTableReadinessSignal(client, _options, logger);
    }
}
