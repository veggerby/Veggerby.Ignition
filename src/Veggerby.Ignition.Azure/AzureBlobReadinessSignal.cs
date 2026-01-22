using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Azure;

/// <summary>
/// Ignition signal for verifying Azure Blob Storage readiness.
/// Validates connection and optionally verifies container existence or creates it if missing.
/// </summary>
internal sealed class AzureBlobReadinessSignal : IIgnitionSignal
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureBlobReadinessOptions _options;
    private readonly ILogger<AzureBlobReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobReadinessSignal"/> class
    /// using an existing <see cref="BlobServiceClient"/>.
    /// </summary>
    /// <param name="blobServiceClient">Azure Blob Storage service client.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public AzureBlobReadinessSignal(
        BlobServiceClient blobServiceClient,
        AzureBlobReadinessOptions options,
        ILogger<AzureBlobReadinessSignal> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "azure-blob-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTask is null)
        {
            lock (_sync)
            {
                _cachedTask ??= ExecuteAsync(cancellationToken);
            }
        }

        return cancellationToken.CanBeCanceled && !_cachedTask.IsCompleted
            ? _cachedTask.WaitAsync(cancellationToken)
            : _cachedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        var accountName = TryGetAccountName();
        activity?.SetTag("azure.storage.account", accountName);
        activity?.SetTag("azure.storage.type", "blob");
        activity?.SetTag("azure.blob.container", _options.ContainerName);
        activity?.SetTag("azure.blob.verify_exists", _options.VerifyContainerExists);
        activity?.SetTag("azure.blob.create_if_not_exists", _options.CreateIfNotExists);

        _logger.LogInformation(
            "Azure Blob Storage readiness check starting for account {AccountName}, container {ContainerName}",
            accountName ?? "(unknown)",
            _options.ContainerName ?? "(none)");

        try
        {
            var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

            // Verify service connectivity by getting account info
            _logger.LogDebug("Verifying Azure Blob Storage service connection");
            await retryPolicy.ExecuteAsync(async ct =>
            {
                await _blobServiceClient.GetAccountInfoAsync(ct).ConfigureAwait(false);
            }, "Azure Blob Storage connection", cancellationToken, _options.Timeout);

            if (_options.VerifyContainerExists && !string.IsNullOrWhiteSpace(_options.ContainerName))
            {
                await VerifyContainerAsync(cancellationToken);
            }

            _logger.LogInformation("Azure Blob Storage readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Azure Blob Storage readiness check failed");
            throw;
        }
    }

    private async Task VerifyContainerAsync(CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);

        _logger.LogDebug("Verifying Azure Blob container existence: {ContainerName}", _options.ContainerName);

        var exists = await containerClient.ExistsAsync(cancellationToken).ConfigureAwait(false);

        if (!exists.Value)
        {
            if (_options.CreateIfNotExists)
            {
                _logger.LogInformation("Creating Azure Blob container: {ContainerName}", _options.ContainerName);
                await containerClient.CreateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Azure Blob container '{_options.ContainerName}' does not exist");
            }
        }
        else
        {
            _logger.LogDebug("Azure Blob container exists: {ContainerName}", _options.ContainerName);
        }
    }

    private string? TryGetAccountName()
    {
        try
        {
            return _blobServiceClient.AccountName;
        }
        catch
        {
            return null;
        }
    }
}
