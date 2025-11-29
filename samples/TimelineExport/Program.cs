using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace TimelineExport;

#region Sample Signals

/// <summary>
/// Database connection signal - simulates connecting to a database.
/// </summary>
public class DatabaseSignal : IIgnitionSignal
{
    public string Name => "database-connection";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(800, cancellationToken);
    }
}

/// <summary>
/// Cache warmup signal - simulates warming up application cache.
/// </summary>
public class CacheWarmupSignal : IIgnitionSignal
{
    public string Name => "cache-warmup";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(3);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(1200, cancellationToken);
    }
}

/// <summary>
/// Configuration loading signal - simulates loading configuration.
/// </summary>
public class ConfigurationSignal : IIgnitionSignal
{
    public string Name => "configuration-load";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(2);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(400, cancellationToken);
    }
}

/// <summary>
/// External service health check - simulates checking external service.
/// </summary>
public class ExternalServiceSignal : IIgnitionSignal
{
    public string Name => "external-service";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(4);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(600, cancellationToken);
    }
}

/// <summary>
/// A slow signal that will timeout (for demonstration).
/// </summary>
public class SlowSignal : IIgnitionSignal
{
    public string Name => "slow-initialization";
    public TimeSpan? Timeout => TimeSpan.FromMilliseconds(500);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(2000, cancellationToken);
    }
}

#endregion

/// <summary>
/// Demonstrates the Timeline Export feature for startup analysis and visualization.
/// Shows how to export ignition results to JSON and console-friendly formats.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              IGNITION TIMELINE EXPORT SAMPLE                                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This sample demonstrates how to export startup timing data for analysis,");
        Console.WriteLine("debugging, profiling, and visualization using the Timeline Export feature.");
        Console.WriteLine();

        await RunParallelExample();
        Console.WriteLine();
        await RunSequentialExample();
        Console.WriteLine();
        await RunWithTimeoutExample();
    }

    /// <summary>
    /// Example 1: Parallel execution with timeline export.
    /// </summary>
    private static async Task RunParallelExample()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 1: PARALLEL Execution Timeline                                      │");
        Console.WriteLine("│ Shows concurrent signal execution in Gantt-like visualization               │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.Parallel;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(10);
                });

                services.AddTransient<IIgnitionSignal, DatabaseSignal>();
                services.AddTransient<IIgnitionSignal, CacheWarmupSignal>();
                services.AddTransient<IIgnitionSignal, ConfigurationSignal>();
                services.AddTransient<IIgnitionSignal, ExternalServiceSignal>();
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .Build();

        var startTime = DateTimeOffset.UtcNow;
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        
        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();
        var endTime = DateTimeOffset.UtcNow;

        // Export timeline with metadata
        var timeline = result.ExportTimeline(
            executionMode: "Parallel",
            globalTimeout: TimeSpan.FromSeconds(10),
            startedAt: startTime,
            completedAt: endTime);

        // Display console visualization
        Console.WriteLine("📊 CONSOLE TIMELINE VISUALIZATION:");
        Console.WriteLine();
        timeline.WriteToConsole();

        // Show JSON export capability
        Console.WriteLine();
        Console.WriteLine("📄 JSON EXPORT (first 500 chars):");
        Console.WriteLine("─".PadRight(60, '─'));
        var json = timeline.ToJson();
        Console.WriteLine(json.Length > 500 ? json.Substring(0, 500) + "..." : json);
        Console.WriteLine();
        Console.WriteLine($"   Full JSON length: {json.Length} characters");
        Console.WriteLine("   Use timeline.ToJson() to get the complete JSON for external tools.");
        Console.WriteLine();
    }

    /// <summary>
    /// Example 2: Sequential execution showing ordering in timeline.
    /// </summary>
    private static async Task RunSequentialExample()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 2: SEQUENTIAL Execution Timeline                                    │");
        Console.WriteLine("│ Shows signals executing one after another                                   │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.Sequential;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(15);
                });

                services.AddTransient<IIgnitionSignal, ConfigurationSignal>();
                services.AddTransient<IIgnitionSignal, DatabaseSignal>();
                services.AddTransient<IIgnitionSignal, ExternalServiceSignal>();
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .Build();

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        
        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();

        var timeline = result.ExportTimeline(executionMode: "Sequential");

        Console.WriteLine("📊 CONSOLE TIMELINE VISUALIZATION:");
        Console.WriteLine();
        timeline.WriteToConsole();
    }

    /// <summary>
    /// Example 3: Timeline with timeout scenarios.
    /// </summary>
    private static async Task RunWithTimeoutExample()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 3: Timeline with TIMEOUT scenario                                   │");
        Console.WriteLine("│ Shows how timeouts appear in the timeline                                   │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.Parallel;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(5);
                    options.CancelIndividualOnTimeout = true;
                });

                services.AddTransient<IIgnitionSignal, ConfigurationSignal>();
                services.AddTransient<IIgnitionSignal, SlowSignal>(); // This will timeout
                services.AddTransient<IIgnitionSignal, ExternalServiceSignal>();
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .Build();

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        
        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();

        var timeline = result.ExportTimeline(
            executionMode: "Parallel",
            globalTimeout: TimeSpan.FromSeconds(5));

        Console.WriteLine("📊 CONSOLE TIMELINE VISUALIZATION:");
        Console.WriteLine();
        timeline.WriteToConsole();

        Console.WriteLine();
        Console.WriteLine("💡 TIP: Use timeline exports to:");
        Console.WriteLine("   • Debug slow startup issues");
        Console.WriteLine("   • Profile container warmup times");
        Console.WriteLine("   • Detect CI timing regressions by comparing JSON exports");
        Console.WriteLine("   • Visualize concurrent execution patterns");
        Console.WriteLine("   • Export to external tools (Chrome DevTools, Perfetto, etc.)");
        Console.WriteLine();
    }
}
