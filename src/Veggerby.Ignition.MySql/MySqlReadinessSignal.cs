using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using MySqlConnector;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MySql;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying MySQL database readiness.
/// Validates connection establishment and optionally executes verification based on configured strategy.
/// </summary>
/// <remarks>
/// <para>
/// This signal supports multiple verification strategies:
/// <list type="bullet">
/// <item><description><see cref="MySqlVerificationStrategy.Ping"/>: Basic connection ping.</description></item>
/// <item><description><see cref="MySqlVerificationStrategy.SimpleQuery"/>: Execute SELECT 1 query.</description></item>
/// <item><description><see cref="MySqlVerificationStrategy.TableExists"/>: Verify specific tables exist.</description></item>
/// <item><description><see cref="MySqlVerificationStrategy.ConnectionPool"/>: Validate connection pool readiness.</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class MySqlReadinessSignal : IIgnitionSignal
{
    private readonly Func<MySqlConnection> _connectionFactory;
    private readonly MySqlReadinessOptions _options;
    private readonly ILogger<MySqlReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlReadinessSignal"/> class using a connection factory.
    /// </summary>
    /// <param name="connectionFactory">Factory function that creates MySQL connections.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This constructor is the recommended modern pattern for DI integration.
    /// The factory allows proper connection management and pooling coordination.
    /// </remarks>
    public MySqlReadinessSignal(
        Func<MySqlConnection> connectionFactory,
        MySqlReadinessOptions options,
        ILogger<MySqlReadinessSignal> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlReadinessSignal"/> class using a connection string.
    /// </summary>
    /// <param name="connectionString">MySQL connection string.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This constructor provides a simple pattern for scenarios where a connection factory is not registered in DI.
    /// For better connection pooling and DI integration, prefer the constructor accepting <see cref="Func{MySqlConnection}"/>.
    /// </remarks>
    public MySqlReadinessSignal(
        string connectionString,
        MySqlReadinessOptions options,
        ILogger<MySqlReadinessSignal> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
        _connectionFactory = () => new MySqlConnection(connectionString);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "mysql-readiness";

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

        var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

        using var connection = await retryPolicy.ExecuteAsync(async ct =>
        {
            var conn = _connectionFactory();
            await conn.OpenAsync(ct).ConfigureAwait(false);
            return conn;
        }, "MySQL connection", cancellationToken, _options.Timeout);

        var serverName = connection.DataSource;
        var databaseName = connection.Database;

        activity?.SetTag("mysql.server", serverName);
        activity?.SetTag("mysql.database", databaseName);
        activity?.SetTag("mysql.verification_strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "MySQL readiness check starting for {Server}/{Database} using {Strategy}",
            serverName,
            databaseName,
            _options.VerificationStrategy);

        try
        {
            // Use custom query if provided
            if (!string.IsNullOrWhiteSpace(_options.TestQuery))
            {
                await ExecuteCustomQueryAsync(connection, cancellationToken);
            }
            else
            {
                // Execute verification based on strategy
                await ExecuteVerificationStrategyAsync(connection, cancellationToken);
            }

            _logger.LogInformation("MySQL readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MySQL readiness check failed");
            throw;
        }
    }

    private async Task ExecuteVerificationStrategyAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        switch (_options.VerificationStrategy)
        {
            case MySqlVerificationStrategy.Ping:
                await ExecutePingAsync(connection, cancellationToken);
                break;

            case MySqlVerificationStrategy.SimpleQuery:
                await ExecuteSimpleQueryAsync(connection, cancellationToken);
                break;

            case MySqlVerificationStrategy.TableExists:
                await ExecuteTableExistsAsync(connection, cancellationToken);
                break;

            case MySqlVerificationStrategy.ConnectionPool:
                await ExecuteConnectionPoolAsync(connection, cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported verification strategy: {_options.VerificationStrategy}");
        }
    }

    private async Task ExecutePingAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing MySQL ping");

        await connection.PingAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("MySQL ping succeeded");
    }

    private async Task ExecuteSimpleQueryAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing simple query: SELECT 1");

        using var command = new MySqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (result == null || Convert.ToInt32(result) != 1)
        {
            throw new InvalidOperationException("Simple query did not return expected result");
        }

        _logger.LogDebug("Simple query executed successfully");
    }

    private async Task ExecuteTableExistsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        if (_options.VerifyTables.Count == 0)
        {
            _logger.LogWarning("TableExists strategy specified but no tables to verify");
            return;
        }

        var schema = _options.Schema ?? connection.Database;
        _logger.LogDebug("Verifying {Count} tables exist in schema {Schema}", _options.VerifyTables.Count, schema);

        foreach (var tableName in _options.VerifyTables)
        {
            var query = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = @Schema
                AND TABLE_NAME = @TableName";

            using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@Schema", schema);
            command.Parameters.AddWithValue("@TableName", tableName);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));

            if (count == 0)
            {
                var message = $"Table '{tableName}' does not exist in schema '{schema}'";
                _logger.LogWarning(message);

                if (_options.FailOnMissingTables)
                {
                    throw new InvalidOperationException(message);
                }
            }
            else
            {
                _logger.LogDebug("Table '{TableName}' exists in schema '{Schema}'", tableName, schema);
            }
        }

        _logger.LogDebug("Table existence verification completed");
    }

    private async Task ExecuteConnectionPoolAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating connection pool readiness");

        // Connection is already open from the outer retry policy
        // Just execute a simple query to ensure the connection is fully functional
        using var command = new MySqlCommand("SELECT 1", connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Connection pool validation completed");
    }

    private async Task ExecuteCustomQueryAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing custom test query: {Query}", _options.TestQuery);

        using var command = new MySqlCommand(_options.TestQuery, connection);

        if (_options.ExpectedMinimumRows.HasValue)
        {
            var rowCount = 0;
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rowCount++;
            }

            if (rowCount < _options.ExpectedMinimumRows.Value)
            {
                throw new InvalidOperationException(
                    $"Custom query returned {rowCount} rows, expected at least {_options.ExpectedMinimumRows.Value}");
            }

            _logger.LogDebug("Custom query returned {RowCount} rows (expected at least {MinRows})",
                rowCount, _options.ExpectedMinimumRows.Value);
        }
        else
        {
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Custom query executed successfully");
        }
    }
}
