using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;
using Veggerby.Ignition.Bundles;

namespace Bundles;

/// <summary>
/// Custom bundle demonstrating a Redis-like startup sequence.
/// </summary>
public class RedisStarterBundle : IIgnitionBundle
{
    private readonly string _connectionString;

    public RedisStarterBundle(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string Name => "RedisStarter";

    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        // Register three signals for Redis initialization
        services.AddIgnitionFromTask(
            "redis:connect",
            async ct =>
            {
                Console.WriteLine($"📡 Connecting to Redis at {_connectionString}...");
                await Task.Delay(800, ct);
                Console.WriteLine("✅ Redis connection established");
            },
            options.DefaultTimeout);

        services.AddIgnitionFromTask(
            "redis:health-check",
            async ct =>
            {
                Console.WriteLine("🏥 Performing Redis health check...");
                await Task.Delay(300, ct);
                Console.WriteLine("✅ Redis health check passed");
            },
            options.DefaultTimeout);

        services.AddIgnitionFromTask(
            "redis:warmup-cache",
            async ct =>
            {
                Console.WriteLine("🔥 Warming up Redis cache...");
                await Task.Delay(600, ct);
                Console.WriteLine("✅ Redis cache warmed successfully");
            },
            options.DefaultTimeout);

        // Configure dependency graph: connect → health → warmup
        services.AddIgnitionGraph((builder, sp) =>
        {
            var signals = sp.GetServices<IIgnitionSignal>().ToList();
            var connectSig = signals.FirstOrDefault(s => s.Name == "redis:connect");
            var healthSig = signals.FirstOrDefault(s => s.Name == "redis:health-check");
            var warmupSig = signals.FirstOrDefault(s => s.Name == "redis:warmup-cache");

            if (connectSig is not null && healthSig is not null && warmupSig is not null)
            {
                builder.AddSignals(new[] { connectSig, healthSig, warmupSig });
                builder.DependsOn(healthSig, connectSig);
                builder.DependsOn(warmupSig, healthSig);
            }
        });
    }
}

/// <summary>
/// Custom bundle for message queue initialization.
/// </summary>
public class MessageQueueBundle : IIgnitionBundle
{
    private readonly string _queueName;

    public MessageQueueBundle(string queueName)
    {
        _queueName = queueName;
    }

    public string Name => $"MessageQueue:{_queueName}";

    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        services.AddIgnitionFromTask(
            $"queue:{_queueName}:connect",
            async ct =>
            {
                Console.WriteLine($"🔌 Connecting to queue '{_queueName}'...");
                await Task.Delay(500, ct);
                Console.WriteLine($"✅ Queue '{_queueName}' connected");
            },
            options.DefaultTimeout);

        services.AddIgnitionFromTask(
            $"queue:{_queueName}:subscribe",
            async ct =>
            {
                Console.WriteLine($"📬 Subscribing to queue '{_queueName}'...");
                await Task.Delay(400, ct);
                Console.WriteLine($"✅ Subscribed to queue '{_queueName}'");
            },
            options.DefaultTimeout);

        // Configure dependency: subscribe depends on connect
        services.AddIgnitionGraph((builder, sp) =>
        {
            var signals = sp.GetServices<IIgnitionSignal>().ToList();
            var connectSig = signals.FirstOrDefault(s => s.Name == $"queue:{_queueName}:connect");
            var subscribeSig = signals.FirstOrDefault(s => s.Name == $"queue:{_queueName}:subscribe");

            if (connectSig is not null && subscribeSig is not null)
            {
                builder.AddSignals(new[] { connectSig, subscribeSig });
                builder.DependsOn(subscribeSig, connectSig);
            }
        });
    }
}

/// <summary>
/// Demonstrates the use of ignition bundles for packaging related signals.
/// Shows both built-in bundles (HttpDependencyBundle, DatabaseTrioBundle) 
/// and custom bundle creation.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Ignition Bundles Sample ===\n");
        Console.WriteLine("This sample demonstrates how bundles package related signals");
        Console.WriteLine("into reusable modules for common startup patterns.\n");

        await RunBuiltInBundlesExample();
        Console.WriteLine();
        await RunCustomBundlesExample();
        Console.WriteLine();
        await RunMixedBundlesExample();
    }

    /// <summary>
    /// Example using built-in HttpDependencyBundle and DatabaseTrioBundle.
    /// </summary>
    private static async Task RunBuiltInBundlesExample()
    {
        Console.WriteLine("🏗️  Example 1: Built-in Bundles");
        Console.WriteLine("   Using HttpDependencyBundle and DatabaseTrioBundle\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                });

                // Register database trio bundle (connect → validate → warmup)
                services.AddIgnitionBundle(
                    new DatabaseTrioBundle(
                        "primary-db",
                        connectFactory: async ct =>
                        {
                            Console.WriteLine("🔌 Connecting to primary database...");
                            await Task.Delay(1000, ct);
                            Console.WriteLine("✅ Primary database connected");
                        },
                        validateSchemaFactory: async ct =>
                        {
                            Console.WriteLine("🔍 Validating database schema...");
                            await Task.Delay(600, ct);
                            Console.WriteLine("✅ Schema validation passed");
                        },
                        warmupFactory: async ct =>
                        {
                            Console.WriteLine("🔥 Warming up database cache...");
                            await Task.Delay(800, ct);
                            Console.WriteLine("✅ Database cache warmed");
                        },
                        defaultTimeout: TimeSpan.FromSeconds(10)));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await ExecuteAndReport(host, "Built-in Bundles");
    }

    /// <summary>
    /// Example using custom bundles (RedisStarterBundle and MessageQueueBundle).
    /// </summary>
    private static async Task RunCustomBundlesExample()
    {
        Console.WriteLine("🏗️  Example 2: Custom Bundles");
        Console.WriteLine("   Using RedisStarterBundle and MessageQueueBundle\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                });

                // Register custom Redis bundle
                services.AddIgnitionBundle(
                    new RedisStarterBundle("localhost:6379"),
                    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(5));

                // Register custom message queue bundles (multiple instances)
                services.AddIgnitionBundle(
                    new MessageQueueBundle("orders"),
                    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(4));

                services.AddIgnitionBundle(
                    new MessageQueueBundle("notifications"),
                    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(4));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await ExecuteAndReport(host, "Custom Bundles");
    }

    /// <summary>
    /// Example mixing built-in and custom bundles.
    /// </summary>
    private static async Task RunMixedBundlesExample()
    {
        Console.WriteLine("🏗️  Example 3: Mixed Bundles");
        Console.WriteLine("   Combining built-in and custom bundles\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.Parallel;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                    options.MaxDegreeOfParallelism = 4;
                });

                // Mix of built-in and custom bundles
                services.AddIgnitionBundle(
                    new DatabaseTrioBundle(
                        "app-db",
                        connectFactory: async ct =>
                        {
                            Console.WriteLine("�� Connecting to application database...");
                            await Task.Delay(900, ct);
                            Console.WriteLine("✅ Application database connected");
                        },
                        warmupFactory: async ct =>
                        {
                            Console.WriteLine("🔥 Warming application cache...");
                            await Task.Delay(500, ct);
                            Console.WriteLine("✅ Application cache warmed");
                        },
                        defaultTimeout: TimeSpan.FromSeconds(8)));

                services.AddIgnitionBundle(new RedisStarterBundle("cache.local:6379"));
                services.AddIgnitionBundle(new MessageQueueBundle("events"));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        await ExecuteAndReport(host, "Mixed Bundles");
    }

    private static async Task ExecuteAndReport(IHost host, string exampleName)
    {
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
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

                if (signalResult.SkippedDueToDependencies && signalResult.FailedDependencies?.Any() == true)
                {
                    Console.Write($" - Skipped due to: {string.Join(", ", signalResult.FailedDependencies)}");
                }
                else if (signalResult.Exception is not null)
                {
                    Console.Write($" - {signalResult.Exception.Message}");
                }

                Console.WriteLine();
            }

            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            Console.WriteLine($"\n{(overallSuccess ? "✅" : "⚠️ ")} Overall Status: {(overallSuccess ? "SUCCESS" : "COMPLETED WITH ISSUES")}");

            if (overallSuccess)
            {
                Console.WriteLine("\n🎉 All bundle signals completed successfully!");
                Console.WriteLine("   Bundles simplify registration by grouping related signals.");
                Console.WriteLine("   Each bundle can have its own timeout and dependency configuration.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute {ExampleName}", exampleName);
        }

        Console.WriteLine(new string('=', 60));
    }
}
