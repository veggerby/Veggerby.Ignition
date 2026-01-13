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
/// <remarks>
/// <para>
/// This signal supports two approaches:
/// <list type="bullet">
/// <item><description>Recommended: Use <see cref="NpgsqlDataSource"/> from DI for connection pooling and modern best practices.</description></item>
/// <item><description>Alternative: Use connection string for simpler scenarios.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class PostgresReadinessSignal : IIgnitionSignal
{
    private readonly NpgsqlDataSource? _dataSource;
    private readonly Func<NpgsqlDataSource>? _dataSourceFactory;
    private readonly bool _ownsDataSource;
    private readonly PostgresReadinessOptions _options;
    private readonly ILogger<PostgresReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresReadinessSignal"/> class
    /// using an existing <see cref="NpgsqlDataSource"/>.
    /// </summary>
    /// <param name="dataSource">PostgreSQL data source for creating connections.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This is the recommended approach as it leverages connection pooling and integrates
    /// with DI container lifecycle management.
    /// </remarks>
    public PostgresReadinessSignal(
        NpgsqlDataSource dataSource,
        PostgresReadinessOptions options,
        ILogger<PostgresReadinessSignal> logger)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _dataSourceFactory = null;
        _ownsDataSource = false;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresReadinessSignal"/> class
    /// using a factory function for lazy <see cref="NpgsqlDataSource"/> creation.
    /// </summary>
    /// <param name="dataSourceFactory">Factory function that creates a PostgreSQL data source when invoked.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This constructor defers data source creation until the signal executes, enabling scenarios
    /// where connection strings are not available at registration time (e.g., Testcontainers).
    /// The factory will be invoked once during signal execution.
    /// </remarks>
    public PostgresReadinessSignal(
        Func<NpgsqlDataSource> dataSourceFactory,
        PostgresReadinessOptions options,
        ILogger<PostgresReadinessSignal> logger)
    {
        _dataSource = null;
        _dataSourceFactory = dataSourceFactory ?? throw new ArgumentNullException(nameof(dataSourceFactory));
        _ownsDataSource = false;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresReadinessSignal"/> class
    /// using a connection string.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This constructor creates an internal <see cref="NpgsqlDataSource"/> that will be
    /// disposed when the signal completes. For production scenarios with connection pooling,
    /// prefer using the constructor that accepts <see cref="NpgsqlDataSource"/>.
    /// </remarks>
    public PostgresReadinessSignal(
        string connectionString,
        PostgresReadinessOptions options,
        ILogger<PostgresReadinessSignal> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _dataSourceFactory = null;
        _ownsDataSource = true;
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

        // Resolve data source from factory if needed
        var dataSource = _dataSource ?? _dataSourceFactory!();

        var builder = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);
        var serverName = builder.Host;
        var databaseName = builder.Database;

        activity?.SetTag("postgres.server", serverName);
        activity?.SetTag("postgres.database", databaseName);

        _logger.LogInformation(
            "PostgreSQL readiness check starting for {Server}/{Database}",
            serverName,
            databaseName);

        try
        {
            using var connection = await OpenConnectionWithRetryAsync(dataSource, cancellationToken);

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
        finally
        {
            // Dispose data source if we own it (created from connection string)
            if (_ownsDataSource && _dataSource != null)
            {
                await _dataSource.DisposeAsync();
            }
        }
    }

    private async Task<NpgsqlConnection> OpenConnectionWithRetryAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        const int initialDelayMs = 100;
        const double multiplier = 1.5;
        const int maxDelayMs = 5000;

        var delay = initialDelayMs;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            NpgsqlConnection? connection = null;
            try
            {
                connection = await dataSource.OpenConnectionAsync(cancellationToken);
                return connection;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (connection != null)
                {
                    await connection.DisposeAsync();
                }

                _logger.LogDebug(
                    ex,
                    "PostgreSQL connection attempt failed, retrying in {DelayMs}ms",
                    delay);

                await Task.Delay(delay, cancellationToken);
                delay = Math.Min((int)(delay * multiplier), maxDelayMs);
            }
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
