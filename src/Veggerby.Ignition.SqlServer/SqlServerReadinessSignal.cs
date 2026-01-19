using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.SqlServer;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying SQL Server database readiness.
/// Validates connection establishment and optionally executes a validation query.
/// </summary>
internal sealed class SqlServerReadinessSignal : IIgnitionSignal
{
    private readonly Func<SqlConnection> _connectionFactory;
    private readonly SqlServerReadinessOptions _options;
    private readonly ILogger<SqlServerReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerReadinessSignal"/> class using a connection factory.
    /// </summary>
    /// <param name="connectionFactory">Factory function that creates SQL Server connections.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This constructor is the recommended modern pattern for DI integration.
    /// The factory allows proper connection management and pooling coordination.
    /// </remarks>
    public SqlServerReadinessSignal(
        Func<SqlConnection> connectionFactory,
        SqlServerReadinessOptions options,
        ILogger<SqlServerReadinessSignal> logger)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerReadinessSignal"/> class using a connection string.
    /// </summary>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <remarks>
    /// This constructor provides a simple pattern for scenarios where a connection factory is not registered in DI.
    /// For better connection pooling and DI integration, prefer the constructor accepting <see cref="Func{SqlConnection}"/>.
    /// </remarks>
    public SqlServerReadinessSignal(
        string connectionString,
        SqlServerReadinessOptions options,
        ILogger<SqlServerReadinessSignal> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
        _connectionFactory = () => new SqlConnection(connectionString);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "sqlserver-readiness";

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
        }, "SQL Server connection", cancellationToken, _options.Timeout);

        var serverName = connection.DataSource;
        var databaseName = connection.Database;

        activity?.SetTag("sqlserver.server", serverName);
        activity?.SetTag("sqlserver.database", databaseName);

        _logger.LogInformation(
            "SQL Server readiness check starting for {Server}/{Database}",
            serverName,
            databaseName);

        try
        {
            _logger.LogDebug("SQL Server connection established");

            if (!string.IsNullOrWhiteSpace(_options.ValidationQuery))
            {
                activity?.SetTag("sqlserver.validation_query", _options.ValidationQuery);
                await ExecuteValidationQueryAsync(connection, cancellationToken);
            }

            _logger.LogInformation("SQL Server readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "SQL Server readiness check failed");
            throw;
        }
    }

    private async Task ExecuteValidationQueryAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing validation query: {Query}", _options.ValidationQuery);

        using var command = new SqlCommand(_options.ValidationQuery, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Validation query executed successfully");
    }
}
