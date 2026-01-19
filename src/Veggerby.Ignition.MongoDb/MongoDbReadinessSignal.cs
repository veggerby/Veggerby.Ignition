using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MongoDb;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying MongoDB cluster readiness.
/// Validates cluster connectivity and optionally verifies collection existence.
/// </summary>
internal sealed class MongoDbReadinessSignal : IIgnitionSignal
{
    private readonly IMongoClient? _client;
    private readonly Func<IMongoClient>? _clientFactory;
    private readonly MongoDbReadinessOptions _options;
    private readonly ILogger<MongoDbReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbReadinessSignal"/> class.
    /// </summary>
    /// <param name="client">MongoDB client instance.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MongoDbReadinessSignal(
        IMongoClient client,
        MongoDbReadinessOptions options,
        ILogger<MongoDbReadinessSignal> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _clientFactory = null;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.VerifyCollection) && string.IsNullOrWhiteSpace(_options.DatabaseName))
        {
            throw new InvalidOperationException("DatabaseName must be specified when VerifyCollection is set.");
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbReadinessSignal"/> class
    /// using a factory function for lazy client creation.
    /// </summary>
    /// <param name="clientFactory">Factory function that creates a MongoDB client when invoked.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public MongoDbReadinessSignal(
        Func<IMongoClient> clientFactory,
        MongoDbReadinessOptions options,
        ILogger<MongoDbReadinessSignal> logger)
    {
        _client = null;
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_options.VerifyCollection) && string.IsNullOrWhiteSpace(_options.DatabaseName))
        {
            throw new InvalidOperationException("DatabaseName must be specified when VerifyCollection is set.");
        }
    }

    /// <inheritdoc/>
    public string Name => "mongodb-readiness";

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

        activity?.SetTag("mongodb.database", _options.DatabaseName);
        activity?.SetTag("mongodb.collection", _options.VerifyCollection);

        _logger.LogInformation("MongoDB readiness check starting");

        try
        {
            // Resolve client from factory if needed
            var client = _client ?? _clientFactory!();

            var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

            // Ping the cluster to verify connectivity
            await retryPolicy.ExecuteAsync(async ct =>
            {
                var database = client.GetDatabase("admin");
                var command = new BsonDocument("ping", 1);
                await database.RunCommandAsync<BsonDocument>(command, cancellationToken: ct).ConfigureAwait(false);
                _logger.LogDebug("MongoDB cluster ping successful");
            }, "MongoDB connection", cancellationToken, _options.Timeout);

            // Verify collection if specified
            if (!string.IsNullOrWhiteSpace(_options.DatabaseName) && !string.IsNullOrWhiteSpace(_options.VerifyCollection))
            {
                await VerifyCollectionAsync(client, cancellationToken);
            }

            _logger.LogInformation("MongoDB readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MongoDB readiness check failed");
            throw;
        }
    }

    private async Task VerifyCollectionAsync(IMongoClient client, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Verifying collection {Database}/{Collection}",
            _options.DatabaseName,
            _options.VerifyCollection);

        var database = client.GetDatabase(_options.DatabaseName);
        var filter = new BsonDocument("name", _options.VerifyCollection);
        var collections = await database.ListCollectionNamesAsync(
            new ListCollectionNamesOptions { Filter = filter },
            cancellationToken);

        var collectionExists = await collections.AnyAsync(cancellationToken).ConfigureAwait(false);

        if (!collectionExists)
        {
            throw new InvalidOperationException(
                $"Collection '{_options.VerifyCollection}' does not exist in database '{_options.DatabaseName}'");
        }

        _logger.LogDebug("Collection '{Collection}' verified", _options.VerifyCollection);
    }
}
