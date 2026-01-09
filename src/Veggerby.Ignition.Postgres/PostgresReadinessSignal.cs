using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Npgsql;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Postgres;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying PostgreSQL database readiness.
/// Validates connection establishment and optionally executes a validation query.
/// </summary>
public sealed class PostgresReadinessSignal : IIgnitionSignal
{
    private readonly string _connectionString;
    private readonly PostgresReadinessOptions _options;
    private readonly ILogger<PostgresReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresReadinessSignal"/> class.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public PostgresReadinessSignal(
        string connectionString,
        PostgresReadinessOptions options,
        ILogger<PostgresReadinessSignal> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
        _connectionString = connectionString;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "postgres-readiness";

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

        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var serverName = builder.Host;
        var databaseName = builder.Database;

        activity?.SetTag("postgres.server", serverName);
        activity?.SetTag("postgres.database", databaseName);

        _logger.LogInformation(
            "PostgreSQL readiness check starting for {Server}/{Database}",
            serverName,
            databaseName);

        using var connection = new NpgsqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
            _logger.LogDebug("PostgreSQL connection established");

            if (!string.IsNullOrWhiteSpace(_options.ValidationQuery))
            {
                activity?.SetTag("postgres.validation_query", _options.ValidationQuery);
                await ExecuteValidationQueryAsync(connection, cancellationToken);
            }

            _logger.LogInformation("PostgreSQL readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "PostgreSQL readiness check failed");
            throw;
        }
    }

    private async Task ExecuteValidationQueryAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing validation query: {Query}", _options.ValidationQuery);

        using var command = new NpgsqlCommand(_options.ValidationQuery, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug("Validation query executed successfully");
    }
}
