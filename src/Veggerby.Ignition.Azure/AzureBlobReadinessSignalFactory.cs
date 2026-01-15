using System;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating Azure Blob Storage readiness signals with configurable connection strings.
/// </summary>
public sealed class AzureBlobReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _connectionStringFactory;
    private readonly AzureBlobReadinessOptions _options;

    /// <summary>
    /// Creates a new Azure Blob Storage readiness signal factory.
    /// </summary>
    /// <param name="connectionStringFactory">Factory that produces the connection string using the service provider.</param>
    /// <param name="options">Azure Blob Storage readiness options.</param>
    public AzureBlobReadinessSignalFactory(
        Func<IServiceProvider, string> connectionStringFactory,
        AzureBlobReadinessOptions options)
    {
        _connectionStringFactory = connectionStringFactory ?? throw new ArgumentNullException(nameof(connectionStringFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "azure-blob-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var connectionString = _connectionStringFactory(serviceProvider);
        var client = new BlobServiceClient(connectionString);
        var logger = serviceProvider.GetRequiredService<ILogger<AzureBlobReadinessSignal>>();
        
        return new AzureBlobReadinessSignal(client, _options, logger);
    }
}
