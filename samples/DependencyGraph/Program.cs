using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace DependencyGraph;

/// <summary>
/// Database connection signal - has no dependencies (root signal).
/// </summary>
public class DatabaseSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseSignal> _logger;

    public string Name => "database";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public DatabaseSignal(ILogger<DatabaseSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔌 Connecting to database...");
        await Task.Delay(800, cancellationToken);
        _logger.LogInformation("✅ Database connected successfully");
    }
}

/// <summary>
/// Cache signal - depends on Database (needs DB connection to warm cache).
/// </summary>
[SignalDependency("database")]
public class CacheSignal : IIgnitionSignal
{
    private readonly ILogger<CacheSignal> _logger;

    public string Name => "cache";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public CacheSignal(ILogger<CacheSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🔥 Warming up cache...");
        await Task.Delay(600, cancellationToken);
        _logger.LogInformation("✅ Cache warmed successfully");
    }
}

/// <summary>
/// Configuration signal - has no dependencies (root signal).
/// </summary>
public class ConfigurationSignal : IIgnitionSignal
{
    private readonly ILogger<ConfigurationSignal> _logger;

    public string Name => "configuration";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(3);

    public ConfigurationSignal(ILogger<ConfigurationSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("⚙️  Loading configuration...");
        await Task.Delay(400, cancellationToken);
        _logger.LogInformation("✅ Configuration loaded successfully");
    }
}

/// <summary>
/// Worker signal - depends on both Cache and Configuration.
/// </summary>
[SignalDependency("cache")]
[SignalDependency("configuration")]
public class WorkerSignal : IIgnitionSignal
{
    private readonly ILogger<WorkerSignal> _logger;

    public string Name => "worker";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public WorkerSignal(ILogger<WorkerSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("⚡ Starting background worker...");
        await Task.Delay(500, cancellationToken);
        _logger.LogInformation("✅ Worker started successfully");
    }
}

/// <summary>
/// API signal - depends on Configuration (needs config to set up API routes).
/// </summary>
[SignalDependency("configuration")]
public class ApiSignal : IIgnitionSignal
{
    private readonly ILogger<ApiSignal> _logger;

    public string Name => "api";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(4);

    public ApiSignal(ILogger<ApiSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🌐 Initializing API endpoints...");
        await Task.Delay(700, cancellationToken);
        _logger.LogInformation("✅ API initialized successfully");
    }
}

/// <summary>
/// Demonstrates dependency-aware (DAG) execution mode with automatic dependency resolution.
/// 
/// Dependency structure:
///        Database          Configuration
///           |               /     \
///         Cache            /       \
///           |             /         \
///           +--------- Worker       API
/// 
/// Expected execution order:
/// 1. Database and Configuration start in parallel (no dependencies)
/// 2. Cache starts after Database completes
/// 3. API starts after Configuration completes (parallel with Cache)
/// 4. Worker starts after both Cache AND Configuration complete
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Dependency-Aware Execution (DAG) Sample ===\n");
        Console.WriteLine("This sample demonstrates how signals can declare dependencies");
        Console.WriteLine("and execute in the correct order automatically.\n");

        // Demonstrate both attribute-based and fluent API approaches
        await RunAttributeBasedExample();
        Console.WriteLine();
        await RunFluentApiExample();
    }

    /// <summary>
    /// Example using SignalDependency attributes for declarative dependencies.
    /// </summary>
    private static async Task RunAttributeBasedExample()
    {
        Console.WriteLine("🏗️  Example 1: Attribute-Based Dependencies");
        Console.WriteLine("   Using [SignalDependency] attributes on signal classes\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure ignition with dependency-aware execution
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                    options.MaxDegreeOfParallelism = 3; // Limit concurrent signals
                });

                // Register signals
                services.AddIgnitionSignal<DatabaseSignal>();
                services.AddIgnitionSignal<CacheSignal>();
                services.AddIgnitionSignal<ConfigurationSignal>();
                services.AddIgnitionSignal<WorkerSignal>();
                services.AddIgnitionSignal<ApiSignal>();

                // Build graph from attributes
                services.AddIgnitionGraphFromFactories(applyAttributeDependencies: true);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await ExecuteAndReport(host, "Attribute-Based");
    }

    /// <summary>
    /// Example using fluent API for programmatic dependency declaration.
    /// </summary>
    private static async Task RunFluentApiExample()
    {
        Console.WriteLine("🏗️  Example 2: Fluent API Dependencies");
        Console.WriteLine("   Using builder.DependsOn() to define dependencies programmatically\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure ignition
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                });

                // Register signals
                services.AddIgnitionSignal<DatabaseSignal>();
                services.AddIgnitionSignal<CacheSignal>();
                services.AddIgnitionSignal<ConfigurationSignal>();
                services.AddIgnitionSignal<WorkerSignal>();
                services.AddIgnitionSignal<ApiSignal>();

                // Build graph using fluent API
                services.AddIgnitionGraph((builder, sp) =>
                {
                    // Note: we resolve factories and construct signals manually here rather than using
                    // AddIgnitionGraphFromFactories because this example explicitly demonstrates the
                    // fluent API for defining dependencies programmatically. We need concrete signal
                    // instances to pass into builder.DependsOn(...) below.
                    
                    // Get factories and create signals
                    var factories = sp.GetServices<IIgnitionSignalFactory>();
                    var signals = factories.Select(f => f.CreateSignal(sp)).ToList();

                    var db = signals.First(s => s.Name == "database");
                    var cache = signals.First(s => s.Name == "cache");
                    var config = signals.First(s => s.Name == "configuration");
                    var worker = signals.First(s => s.Name == "worker");
                    var api = signals.First(s => s.Name == "api");

                    builder.AddSignals(signals);

                    // Define dependencies explicitly
                    builder.DependsOn(cache, db);              // Cache depends on Database
                    builder.DependsOn(worker, cache, config);  // Worker depends on Cache AND Configuration
                    builder.DependsOn(api, config);            // API depends on Configuration
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await ExecuteAndReport(host, "Fluent API");
    }

    private static async Task ExecuteAndReport(IHost host, string exampleName)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            var startTime = DateTime.UtcNow;
            await coordinator.WaitAllAsync();
            var result = await coordinator.GetResultAsync();

            Console.WriteLine($"\n📊 {exampleName} Results:");
            Console.WriteLine($"   Total Duration: {result.TotalDuration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"   Timed Out: {(result.TimedOut ? "YES" : "NO")}");

            var succeeded = result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded);
            var failed = result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed);
            var skipped = result.Results.Count(r => r.Status == IgnitionSignalStatus.Skipped);
            var timedOut = result.Results.Count(r => r.Status == IgnitionSignalStatus.TimedOut);

            Console.WriteLine($"   Success: {succeeded}/{result.Results.Count}");
            if (failed > 0) Console.WriteLine($"   Failed: {failed}");
            if (skipped > 0) Console.WriteLine($"   Skipped: {skipped}");
            if (timedOut > 0) Console.WriteLine($"   Timed Out: {timedOut}");

            Console.WriteLine("\n📋 Signal Execution Details:");
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

                Console.Write($"   {icon} {signalResult.Name}: {signalResult.Status} ({signalResult.Duration.TotalMilliseconds:F0}ms)");

                if (signalResult.SkippedDueToDependencies)
                {
                    Console.Write($" - Skipped due to failed dependencies: {string.Join(", ", signalResult.FailedDependencies!)}");
                }
                else if (signalResult.Exception is not null)
                {
                    Console.Write($" - {signalResult.Exception.Message}");
                }

                Console.WriteLine();
            }

            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            Console.WriteLine($"\n{(overallSuccess ? "✅" : "❌")} Overall Status: {(overallSuccess ? "SUCCESS" : "PARTIAL SUCCESS")}");

            if (overallSuccess)
            {
                Console.WriteLine("\n🎉 All signals completed successfully!");
                Console.WriteLine("   Notice how:");
                Console.WriteLine("   - Database and Configuration started first (no dependencies)");
                Console.WriteLine("   - Cache started only after Database completed");
                Console.WriteLine("   - API started only after Configuration completed");
                Console.WriteLine("   - Worker started only after BOTH Cache and Configuration completed");
                Console.WriteLine("   - Independent branches (Cache and API) ran in parallel");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute {ExampleName}", exampleName);
        }

        Console.WriteLine(new string('=', 60));
    }
}
