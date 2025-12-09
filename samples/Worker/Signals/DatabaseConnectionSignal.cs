using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Worker.Signals;

/// <summary>
/// Signal representing database connection readiness for background workers.
/// Ensures the worker can access required data stores before starting processing.
/// </summary>
public sealed class DatabaseConnectionSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseConnectionSignal> _logger;

    public string Name => "database-connection";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(8);

    public DatabaseConnectionSignal(ILogger<DatabaseConnectionSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing database connection pool...");

        // Simulate connection pool initialization
        await Task.Delay(1000, cancellationToken);

        // Simulate connectivity test
        await Task.Delay(300, cancellationToken);

        _logger.LogInformation("Database connection pool ready with 20 connections");
    }
}
