using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace MultiEnvironment;

/// <summary>
/// Database connection signal with environment-specific timeout.
/// </summary>
public class DatabaseSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseSignal> _logger;
    private readonly TimeSpan _timeout;

    public string Name => "database-connection";

    public TimeSpan? Timeout => _timeout;

    public DatabaseSignal(ILogger<DatabaseSignal> logger, IConfiguration configuration)
    {
        _logger = logger;
        _timeout = configuration.GetValue<TimeSpan>("Ignition:Timeouts:Database");
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to database (timeout: {Timeout}s)...", _timeout.TotalSeconds);
        await Task.Delay(1500, cancellationToken);
        _logger.LogInformation("Database connection established");
    }
}

/// <summary>
/// External API health check signal with environment-specific timeout.
/// </summary>
public class ExternalApiSignal : IIgnitionSignal
{
    private readonly ILogger<ExternalApiSignal> _logger;
    private readonly TimeSpan _timeout;

    public string Name => "external-api-health";

    public TimeSpan? Timeout => _timeout;

    public ExternalApiSignal(ILogger<ExternalApiSignal> logger, IConfiguration configuration)
    {
        _logger = logger;
        _timeout = configuration.GetValue<TimeSpan>("Ignition:Timeouts:ExternalApi");
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking external API health (timeout: {Timeout}s)...", _timeout.TotalSeconds);
        await Task.Delay(800, cancellationToken);
        _logger.LogInformation("External API is healthy");
    }
}

/// <summary>
/// Cache warmup signal registered only in Production environment.
/// </summary>
public class CacheWarmupSignal : IIgnitionSignal
{
    private readonly ILogger<CacheWarmupSignal> _logger;
    private readonly TimeSpan _timeout;

    public string Name => "cache-warmup";

    public TimeSpan? Timeout => _timeout;

    public CacheWarmupSignal(ILogger<CacheWarmupSignal> logger, IConfiguration configuration)
    {
        _logger = logger;
        _timeout = configuration.GetValue<TimeSpan>("Ignition:Timeouts:CacheWarmup");
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Warming up cache (timeout: {Timeout}s)...", _timeout.TotalSeconds);
        await Task.Delay(2000, cancellationToken);
        _logger.LogInformation("Cache warmed successfully");
    }
}

/// <summary>
/// Development-only diagnostic signal for testing.
/// </summary>
public class DevelopmentDiagnosticsSignal : IIgnitionSignal
{
    private readonly ILogger<DevelopmentDiagnosticsSignal> _logger;

    public string Name => "development-diagnostics";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(2);

    public DevelopmentDiagnosticsSignal(ILogger<DevelopmentDiagnosticsSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running development diagnostics...");
        await Task.Delay(500, cancellationToken);
        _logger.LogInformation("Development diagnostics completed");
    }
}

/// <summary>
/// Demonstrates environment-specific configuration patterns for Ignition.
/// Shows how to configure different timeouts, policies, and signal registration per environment.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                var environment = context.HostingEnvironment;

                services.AddIgnition(options =>
                {
                    options.Policy = configuration.GetValue<IgnitionPolicy>("Ignition:Policy");
                    options.ExecutionMode = configuration.GetValue<IgnitionExecutionMode>("Ignition:ExecutionMode");
                    options.GlobalTimeout = configuration.GetValue<TimeSpan>("Ignition:GlobalTimeout");
                    options.CancelOnGlobalTimeout = configuration.GetValue<bool>("Ignition:CancelOnGlobalTimeout");
                    options.CancelIndividualOnTimeout = configuration.GetValue<bool>("Ignition:CancelIndividualOnTimeout");
                    options.EnableTracing = configuration.GetValue<bool>("Ignition:EnableTracing");
                    options.MaxDegreeOfParallelism = configuration.GetValue<int>("Ignition:MaxDegreeOfParallelism");
                });

                services.AddIgnitionSignal<DatabaseSignal>();
                services.AddIgnitionSignal<ExternalApiSignal>();

                if (environment.IsProduction())
                {
                    services.AddIgnitionSignal<CacheWarmupSignal>();
                }

                if (environment.IsDevelopment())
                {
                    services.AddIgnitionSignal<DevelopmentDiagnosticsSignal>();
                }
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(context.HostingEnvironment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        var environment = host.Services.GetRequiredService<IHostEnvironment>();
        var configuration = host.Services.GetRequiredService<IConfiguration>();

        Console.WriteLine("=== Multi-Environment Ignition Sample ===\n");
        Console.WriteLine($"Environment: {environment.EnvironmentName}");
        Console.WriteLine($"Policy: {configuration.GetValue<string>("Ignition:Policy")}");
        Console.WriteLine($"Execution Mode: {configuration.GetValue<string>("Ignition:ExecutionMode")}");
        Console.WriteLine($"Global Timeout: {configuration.GetValue<TimeSpan>("Ignition:GlobalTimeout").TotalSeconds}s");
        Console.WriteLine($"Max Parallelism: {configuration.GetValue<int>("Ignition:MaxDegreeOfParallelism")}");
        Console.WriteLine();

        try
        {
            logger.LogInformation("Starting application initialization for {Environment} environment...", environment.EnvironmentName);

            await coordinator.WaitAllAsync();
            var result = await coordinator.GetResultAsync();

            Console.WriteLine("\n=== Initialization Results ===\n");
            Console.WriteLine($"Total Duration: {result.TotalDuration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"Timed Out: {(result.TimedOut ? "YES" : "NO")}");
            Console.WriteLine($"Signals Executed: {result.Results.Count}\n");

            Console.WriteLine("Signal Details:");
            foreach (var signalResult in result.Results.OrderBy(r => r.Name))
            {
                var icon = signalResult.Status switch
                {
                    IgnitionSignalStatus.Succeeded => "✅",
                    IgnitionSignalStatus.Failed => "❌",
                    IgnitionSignalStatus.TimedOut => "⏰",
                    IgnitionSignalStatus.Skipped => "⏭️ ",
                    _ => "❓"
                };

                Console.WriteLine($"  {icon} {signalResult.Name}");
                Console.WriteLine($"     Status: {signalResult.Status}");
                Console.WriteLine($"     Duration: {signalResult.Duration.TotalMilliseconds:F0}ms");

                if (signalResult.Exception != null)
                {
                    Console.WriteLine($"     Error: {signalResult.Exception.Message}");
                }

                Console.WriteLine();
            }

            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            if (overallSuccess)
            {
                logger.LogInformation("All initialization signals completed successfully!");

                Console.WriteLine("\n=== Environment-Specific Behavior ===\n");
                if (environment.IsDevelopment())
                {
                    Console.WriteLine("Development Mode Active:");
                    Console.WriteLine("  • Shorter timeouts for faster feedback");
                    Console.WriteLine("  • BestEffort policy for resilience");
                    Console.WriteLine("  • Development diagnostics signal included");
                    Console.WriteLine("  • No cache warmup (not needed in dev)");
                }
                else if (environment.IsProduction())
                {
                    Console.WriteLine("Production Mode Active:");
                    Console.WriteLine("  • Longer timeouts for reliability");
                    Console.WriteLine("  • FailFast policy for strict validation");
                    Console.WriteLine("  • Cache warmup signal included");
                    Console.WriteLine("  • No development diagnostics");
                }

                Console.WriteLine("\n✨ Application is ready to serve requests.");
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

        Console.WriteLine("\n=== Configuration Tips ===\n");
        Console.WriteLine("To run in Production mode:");
        Console.WriteLine("  dotnet run --environment Production");
        Console.WriteLine("\nTo run in Development mode:");
        Console.WriteLine("  dotnet run --environment Development");
        Console.WriteLine("\nTo override configuration:");
        Console.WriteLine("  dotnet run --Ignition:GlobalTimeout=00:01:00");
    }
}
