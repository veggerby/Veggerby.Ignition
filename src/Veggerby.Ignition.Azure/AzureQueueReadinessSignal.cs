using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Azure Queue Storage readiness.
/// Validates connection and optionally verifies queue existence or creates it if missing.
/// </summary>
internal sealed class AzureQueueReadinessSignal : IIgnitionSignal
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly AzureQueueReadinessOptions _options;
    private readonly ILogger<AzureQueueReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureQueueReadinessSignal"/> class
    /// using an existing <see cref="QueueServiceClient"/>.
    /// </summary>
    /// <param name="queueServiceClient">Azure Queue Storage service client.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public AzureQueueReadinessSignal(
        QueueServiceClient queueServiceClient,
        AzureQueueReadinessOptions options,
        ILogger<AzureQueueReadinessSignal> logger)
    {
        _queueServiceClient = queueServiceClient ?? throw new ArgumentNullException(nameof(queueServiceClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "azure-queue-readiness";

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
        activity?.SetTag("azure.storage.type", "queue");
        activity?.SetTag("azure.queue.name", _options.QueueName);
        activity?.SetTag("azure.queue.verify_exists", _options.VerifyQueueExists);
        activity?.SetTag("azure.queue.create_if_not_exists", _options.CreateIfNotExists);

        _logger.LogInformation(
            "Azure Queue Storage readiness check starting for account {AccountName}, queue {QueueName}",
            accountName ?? "(unknown)",
            _options.QueueName ?? "(none)");

        try
        {
            // Verify service connectivity by getting service properties
            _logger.LogDebug("Verifying Azure Queue Storage service connection");
            await _queueServiceClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);

            if (_options.VerifyQueueExists && !string.IsNullOrWhiteSpace(_options.QueueName))
            {
                await VerifyQueueAsync(cancellationToken);
            }

            _logger.LogInformation("Azure Queue Storage readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Azure Queue Storage readiness check failed");
            throw;
        }
    }

    private async Task VerifyQueueAsync(CancellationToken cancellationToken)
    {
        var queueClient = _queueServiceClient.GetQueueClient(_options.QueueName);

        _logger.LogDebug("Verifying Azure Queue existence: {QueueName}", _options.QueueName);

        var exists = await queueClient.ExistsAsync(cancellationToken).ConfigureAwait(false);

        if (!exists.Value)
        {
            if (_options.CreateIfNotExists)
            {
                _logger.LogInformation("Creating Azure Queue: {QueueName}", _options.QueueName);
                await queueClient.CreateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Azure Queue '{_options.QueueName}' does not exist");
            }
        }
        else
        {
            _logger.LogDebug("Azure Queue exists: {QueueName}", _options.QueueName);
        }
    }

    private string? TryGetAccountName()
    {
        try
        {
            return _queueServiceClient.AccountName;
        }
        catch
        {
            return null;
        }
    }
}
