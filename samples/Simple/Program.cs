using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Simple;

/// <summary>
/// A simple database connection signal that simulates establishing a database connection.
/// </summary>
public class DatabaseConnectionSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseConnectionSignal> _logger;

    public string Name => "database-connection";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public DatabaseConnectionSignal(ILogger<DatabaseConnectionSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Establishing database connection...");

        // Simulate database connection setup
        await Task.Delay(1000, cancellationToken);

        _logger.LogInformation("Database connection established successfully");
    }
}

/// <summary>
/// A configuration loading signal that simulates loading application configuration.
/// </summary>
public class ConfigurationLoadSignal : IIgnitionSignal
{
    private readonly ILogger<ConfigurationLoadSignal> _logger;

    public string Name => "configuration-load";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(3);

    public ConfigurationLoadSignal(ILogger<ConfigurationLoadSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading application configuration...");

        // Simulate configuration loading
        await Task.Delay(500, cancellationToken);

        _logger.LogInformation("Configuration loaded successfully");
    }
}

/// <summary>
/// Simple console application demonstrating basic Ignition usage.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register ignition services
                services.AddIgnition();

                // Register our custom signals
                services.AddTransient<IIgnitionSignal, DatabaseConnectionSignal>();
                services.AddTransient<IIgnitionSignal, ConfigurationLoadSignal>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            logger.LogInformation("Starting application initialization...");

            // Wait for all signals to complete
            await coordinator.WaitAllAsync();

            // Get the result
            var result = await coordinator.GetResultAsync();

            logger.LogInformation("Initialization completed in {Duration}ms",
                result.TotalDuration.TotalMilliseconds);

            // Check if all signals succeeded
            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            if (overallSuccess)
            {
                logger.LogInformation("All initialization signals completed successfully!");

                // Print individual signal results
                foreach (var signalResult in result.Results)
                {
                    logger.LogInformation("Signal '{SignalName}': {Status} ({Duration}ms)",
                        signalResult.Name,
                        signalResult.Status,
                        signalResult.Duration.TotalMilliseconds);
                }

                logger.LogInformation("Application is ready to serve requests.");
            }
            else
            {
                logger.LogError("Some initialization signals failed:");

                foreach (var signalResult in result.Results.Where(r => r.Status != IgnitionSignalStatus.Succeeded))
                {
                    logger.LogError("Signal '{SignalName}': {Status} - {Exception}",
                        signalResult.Name,
                        signalResult.Status,
                        signalResult.Exception?.Message ?? "No exception details");
                }

                Environment.ExitCode = 1;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize application");
            Environment.ExitCode = 1;
        }
    }
}
