using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Advanced;

/// <summary>
/// A fast cache warming signal that completes quickly.
/// </summary>
public class CacheWarmupSignal : IIgnitionSignal
{
    private readonly ILogger<CacheWarmupSignal> _logger;

    public string Name => "cache-warmup";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(2);

    public CacheWarmupSignal(ILogger<CacheWarmupSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Warming up cache...");
        await Task.Delay(300, cancellationToken);
        _logger.LogInformation("Cache warmup completed");
    }
}

/// <summary>
/// A slower database migration signal that takes more time.
/// </summary>
public class DatabaseMigrationSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseMigrationSignal> _logger;

    public string Name => "database-migration";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(8);

    public DatabaseMigrationSignal(ILogger<DatabaseMigrationSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running database migrations...");
        await Task.Delay(2000, cancellationToken);
        _logger.LogInformation("Database migrations completed");
    }
}

/// <summary>
/// An external service dependency that might fail.
/// </summary>
public class ExternalServiceSignal : IIgnitionSignal
{
    private readonly ILogger<ExternalServiceSignal> _logger;
    private readonly bool _shouldFail;

    public string Name => "external-service";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public ExternalServiceSignal(ILogger<ExternalServiceSignal> logger)
    {
        _logger = logger;
        // Simulate random failure for demonstration
        _shouldFail = Random.Shared.Next(0, 3) == 0; // 1 in 3 chance of failure
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to external service...");
        await Task.Delay(1500, cancellationToken);

        if (_shouldFail)
        {
            _logger.LogWarning("External service is unavailable");
            throw new InvalidOperationException("External service connection failed");
        }

        _logger.LogInformation("External service connected successfully");
    }
}

/// <summary>
/// A signal that will always timeout for demonstration purposes.
/// </summary>
public class SlowServiceSignal : IIgnitionSignal
{
    private readonly ILogger<SlowServiceSignal> _logger;

    public string Name => "slow-service";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(1);

    public SlowServiceSignal(ILogger<SlowServiceSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting slow service initialization...");

        try
        {
            // This will take longer than the timeout
            await Task.Delay(3000, cancellationToken);
            _logger.LogInformation("Slow service initialization completed");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Slow service initialization was cancelled");
            throw;
        }
    }
}

/// <summary>
/// Advanced console application demonstrating complex Ignition scenarios.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Advanced Ignition Sample ===\n");

        await RunScenario1();
        await RunScenario2();
        await RunScenario3();
    }

    /// <summary>
    /// Scenario 1: Parallel execution with BestEffort policy
    /// </summary>
    private static async Task RunScenario1()
    {
        Console.WriteLine("🔄 Scenario 1: Parallel BestEffort (tolerates failures)");
        Console.WriteLine("Expected: All signals run in parallel, continues despite failures\n");

        var host = CreateHost(options =>
        {
            options.Policy = IgnitionPolicy.BestEffort;
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
            options.GlobalTimeout = TimeSpan.FromSeconds(10);
            options.EnableTracing = true;
        });

        await RunAndReport(host, "Scenario 1");
        Console.WriteLine(new string('=', 60) + "\n");
    }

    /// <summary>
    /// Scenario 2: Sequential execution with FailFast policy
    /// </summary>
    private static async Task RunScenario2()
    {
        Console.WriteLine("🔄 Scenario 2: Sequential FailFast (stops on first failure)");
        Console.WriteLine("Expected: Signals run one by one, stops immediately on failure\n");

        var host = CreateHost(options =>
        {
            options.Policy = IgnitionPolicy.FailFast;
            options.ExecutionMode = IgnitionExecutionMode.Sequential;
            options.GlobalTimeout = TimeSpan.FromSeconds(15);
            options.CancelIndividualOnTimeout = true;
        });

        await RunAndReport(host, "Scenario 2");
        Console.WriteLine(new string('=', 60) + "\n");
    }

    /// <summary>
    /// Scenario 3: Parallel with concurrency limiting and ContinueOnTimeout
    /// </summary>
    private static async Task RunScenario3()
    {
        Console.WriteLine("🔄 Scenario 3: Limited concurrency with ContinueOnTimeout");
        Console.WriteLine("Expected: Max 2 signals run concurrently, timeouts are ignored\n");

        var host = CreateHost(options =>
        {
            options.Policy = IgnitionPolicy.ContinueOnTimeout;
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
            options.MaxDegreeOfParallelism = 2;
            options.GlobalTimeout = TimeSpan.FromSeconds(12);
            options.CancelOnGlobalTimeout = false;
        });

        await RunAndReport(host, "Scenario 3");
        Console.WriteLine(new string('=', 60) + "\n");
    }

    private static IHost CreateHost(Action<IgnitionOptions> configureOptions)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(configureOptions);

                // Register all signals
                services.AddTransient<IIgnitionSignal, CacheWarmupSignal>();
                services.AddTransient<IIgnitionSignal, DatabaseMigrationSignal>();
                services.AddTransient<IIgnitionSignal, ExternalServiceSignal>();
                services.AddTransient<IIgnitionSignal, SlowServiceSignal>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();
    }

    private static async Task RunAndReport(IHost host, string scenarioName)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            await coordinator.WaitAllAsync();
            var result = await coordinator.GetResultAsync();

            Console.WriteLine($"📊 {scenarioName} Results:");
            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            Console.WriteLine($"   Overall Success: {overallSuccess}");
            Console.WriteLine($"   Total Duration: {result.TotalDuration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"   Global Timeout: {(result.TimedOut ? "YES" : "NO")}");
            Console.WriteLine();

            Console.WriteLine("📋 Individual Signal Results:");
            foreach (var signalResult in result.Results.OrderBy(r => r.Name))
            {
                var status = signalResult.Status switch
                {
                    IgnitionSignalStatus.Succeeded => "✅",
                    IgnitionSignalStatus.Failed => "❌",
                    IgnitionSignalStatus.TimedOut => "⏰",
                    _ => "❓"
                };

                Console.WriteLine($"   {status} {signalResult.Name}: {signalResult.Status} ({signalResult.Duration.TotalMilliseconds:F0}ms)");

                if (signalResult.Exception != null)
                {
                    Console.WriteLine($"      Error: {signalResult.Exception.Message}");
                }
            }
        }
        catch (AggregateException ex)
        {
            Console.WriteLine($"❌ {scenarioName} failed with {ex.InnerExceptions.Count} exception(s):");
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"   - {inner.GetType().Name}: {inner.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ {scenarioName} failed: {ex.Message}");
        }

        Console.WriteLine();
    }
}
