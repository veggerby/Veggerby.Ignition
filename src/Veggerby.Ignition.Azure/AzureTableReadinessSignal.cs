using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Azure Table Storage readiness.
/// Validates connection and optionally verifies table existence or creates it if missing.
/// </summary>
internal sealed class AzureTableReadinessSignal : IIgnitionSignal
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly AzureTableReadinessOptions _options;
    private readonly ILogger<AzureTableReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureTableReadinessSignal"/> class
    /// using an existing <see cref="TableServiceClient"/>.
    /// </summary>
    /// <param name="tableServiceClient">Azure Table Storage service client.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public AzureTableReadinessSignal(
        TableServiceClient tableServiceClient,
        AzureTableReadinessOptions options,
        ILogger<AzureTableReadinessSignal> logger)
    {
        _tableServiceClient = tableServiceClient ?? throw new ArgumentNullException(nameof(tableServiceClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "azure-table-readiness";

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
        activity?.SetTag("azure.storage.type", "table");
        activity?.SetTag("azure.table.name", _options.TableName);
        activity?.SetTag("azure.table.verify_exists", _options.VerifyTableExists);
        activity?.SetTag("azure.table.create_if_not_exists", _options.CreateIfNotExists);

        _logger.LogInformation(
            "Azure Table Storage readiness check starting for account {AccountName}, table {TableName}",
            accountName ?? "(unknown)",
            _options.TableName ?? "(none)");

        try
        {
            // Verify service connectivity by querying service properties
            _logger.LogDebug("Verifying Azure Table Storage service connection");
            await _tableServiceClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);

            if (_options.VerifyTableExists && !string.IsNullOrWhiteSpace(_options.TableName))
            {
                await VerifyTableAsync(cancellationToken);
            }

            _logger.LogInformation("Azure Table Storage readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Azure Table Storage readiness check failed");
            throw;
        }
    }

    private async Task VerifyTableAsync(CancellationToken cancellationToken)
    {
        var tableClient = _tableServiceClient.GetTableClient(_options.TableName);

        _logger.LogDebug("Verifying Azure Table existence: {TableName}", _options.TableName);

        // Use CreateIfNotExists to safely check and optionally create the table
        // If CreateIfNotExists=false, we'll get an exception if the table doesn't exist
        try
        {
            if (_options.CreateIfNotExists)
            {
                _logger.LogInformation("Ensuring Azure Table exists: {TableName}", _options.TableName);
                await tableClient.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Attempt to get table properties to verify existence
                // This will throw RequestFailedException if table doesn't exist
                await tableClient.GetAccessPoliciesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Azure Table exists: {TableName}", _options.TableName);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new InvalidOperationException(
                $"Azure Table '{_options.TableName}' does not exist", ex);
        }
    }

    private string? TryGetAccountName()
    {
        try
        {
            return _tableServiceClient.AccountName;
        }
        catch
        {
            return null;
        }
    }
}
