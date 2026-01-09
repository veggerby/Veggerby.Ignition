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
public sealed class SqlServerReadinessSignal : IIgnitionSignal
{
    private readonly string _connectionString;
    private readonly SqlServerReadinessOptions _options;
    private readonly ILogger<SqlServerReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerReadinessSignal"/> class.
    /// </summary>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SqlServerReadinessSignal(
        string connectionString,
        SqlServerReadinessOptions options,
        ILogger<SqlServerReadinessSignal> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
        _connectionString = connectionString;
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

        var builder = new SqlConnectionStringBuilder(_connectionString);
        var serverName = builder.DataSource;
        var databaseName = builder.InitialCatalog;

        activity?.SetTag("sqlserver.server", serverName);
        activity?.SetTag("sqlserver.database", databaseName);

        _logger.LogInformation(
            "SQL Server readiness check starting for {Server}/{Database}",
            serverName,
            databaseName);

        using var connection = new SqlConnection(_connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
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
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug("Validation query executed successfully");
    }
}
