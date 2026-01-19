using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Elasticsearch;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Elasticsearch cluster readiness.
/// Validates cluster health, index existence, templates, or query execution.
/// </summary>
internal sealed class ElasticsearchReadinessSignal : IIgnitionSignal
{
    private readonly ElasticsearchClient? _client;
    private readonly Func<ElasticsearchClient>? _clientFactory;
    private readonly ElasticsearchReadinessOptions _options;
    private readonly ILogger<ElasticsearchReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchReadinessSignal"/> class
    /// using an existing <see cref="ElasticsearchClient"/>.
    /// </summary>
    /// <param name="client">Elasticsearch client.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ElasticsearchReadinessSignal(
        ElasticsearchClient client,
        ElasticsearchReadinessOptions options,
        ILogger<ElasticsearchReadinessSignal> logger)
    {
        ArgumentNullException.ThrowIfNull(client, nameof(client));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _client = client;
        _clientFactory = null;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticsearchReadinessSignal"/> class
    /// using a factory function for lazy client creation.
    /// </summary>
    /// <param name="clientFactory">Factory function that creates an Elasticsearch client when invoked.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public ElasticsearchReadinessSignal(
        Func<ElasticsearchClient> clientFactory,
        ElasticsearchReadinessOptions options,
        ILogger<ElasticsearchReadinessSignal> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory, nameof(clientFactory));
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));

        _client = null;
        _clientFactory = clientFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "elasticsearch-readiness";

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

        // Resolve client from factory if needed
        var client = _client ?? _clientFactory!();

        activity?.SetTag("elasticsearch.verification_strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "Elasticsearch readiness check starting using strategy {Strategy}",
            _options.VerificationStrategy);

        var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

        await retryPolicy.ExecuteAsync(
            async ct =>
            {
                switch (_options.VerificationStrategy)
                {
                    case ElasticsearchVerificationStrategy.ClusterHealth:
                        await VerifyClusterHealthAsync(client, ct);
                        break;

                    case ElasticsearchVerificationStrategy.IndexExists:
                        await VerifyIndicesExistAsync(client, ct);
                        break;

                    case ElasticsearchVerificationStrategy.TemplateValidation:
                        await VerifyTemplateAsync(client, ct);
                        break;

                    case ElasticsearchVerificationStrategy.QueryTest:
                        await ExecuteTestQueryAsync(client, ct);
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown verification strategy: {_options.VerificationStrategy}");
                }
            },
            "Elasticsearch readiness check",
            cancellationToken,
            _options.Timeout);

        _logger.LogInformation("Elasticsearch readiness check completed successfully");
    }

    private async Task VerifyClusterHealthAsync(ElasticsearchClient client, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking Elasticsearch cluster health");

        var healthResponse = await client.Cluster.HealthAsync(cancellationToken: cancellationToken);

        if (!healthResponse.IsValidResponse)
        {
            throw new InvalidOperationException($"Failed to retrieve cluster health: {healthResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        var status = healthResponse.Status.ToString();
        _logger.LogInformation("Elasticsearch cluster health status: {Status}", status);

        var activity = Activity.Current;
        activity?.SetTag("elasticsearch.cluster.status", status);
        activity?.SetTag("elasticsearch.cluster.number_of_nodes", healthResponse.NumberOfNodes);
        activity?.SetTag("elasticsearch.cluster.active_shards", healthResponse.ActiveShards);
    }

    private async Task VerifyIndicesExistAsync(ElasticsearchClient client, CancellationToken cancellationToken)
    {
        if (_options.VerifyIndices.Count == 0)
        {
            throw new InvalidOperationException("VerifyIndices list is empty. Specify at least one index to verify.");
        }

        _logger.LogDebug("Verifying existence of {Count} indices", _options.VerifyIndices.Count);

        var missingIndices = new List<string>();

        foreach (var indexName in _options.VerifyIndices)
        {
            var existsResponse = await client.Indices.ExistsAsync(indexName, cancellationToken: cancellationToken);

            if (!existsResponse.IsValidResponse || !existsResponse.Exists)
            {
                missingIndices.Add(indexName);
                _logger.LogWarning("Index {IndexName} does not exist", indexName);
            }
            else
            {
                _logger.LogDebug("Index {IndexName} exists", indexName);
            }
        }

        if (missingIndices.Count > 0 && _options.FailOnMissingIndices)
        {
            throw new InvalidOperationException($"Missing indices: {string.Join(", ", missingIndices)}");
        }

        _logger.LogInformation(
            "Index verification completed: {ExistingCount}/{TotalCount} indices exist",
            _options.VerifyIndices.Count - missingIndices.Count,
            _options.VerifyIndices.Count);
    }

    private async Task VerifyTemplateAsync(ElasticsearchClient client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.VerifyTemplate))
        {
            throw new InvalidOperationException("VerifyTemplate is not specified. Set the template name to verify.");
        }

        _logger.LogDebug("Verifying index template {TemplateName}", _options.VerifyTemplate);

        var templateResponse = await client.Indices.ExistsIndexTemplateAsync(_options.VerifyTemplate, cancellationToken: cancellationToken);

        if (!templateResponse.IsValidResponse || !templateResponse.Exists)
        {
            throw new InvalidOperationException($"Index template '{_options.VerifyTemplate}' does not exist");
        }

        _logger.LogInformation("Index template {TemplateName} verified successfully", _options.VerifyTemplate);
    }

    private async Task ExecuteTestQueryAsync(ElasticsearchClient client, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TestQueryIndex))
        {
            throw new InvalidOperationException("TestQueryIndex is not specified. Set the index name to query.");
        }

        _logger.LogDebug("Executing test query on index {IndexName}", _options.TestQueryIndex);

        // Execute a simple match_all query with size=0 to test queryability without retrieving documents
        var searchResponse = await client.SearchAsync<object>(s => s
            .Index(_options.TestQueryIndex)
            .Size(0)
            .Query(q => q.MatchAll(new Elastic.Clients.Elasticsearch.QueryDsl.MatchAllQuery())),
            cancellationToken);

        if (!searchResponse.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Test query failed on index '{_options.TestQueryIndex}': {searchResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"}");
        }

        _logger.LogInformation(
            "Test query completed successfully on index {IndexName} ({TotalHits} total documents)",
            _options.TestQueryIndex,
            searchResponse.Total);
    }
}
