using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MongoDB.Driver;

using Npgsql;

using RabbitMQ.Client;

using StackExchange.Redis;

using Veggerby.Ignition;
using Veggerby.Ignition.Http;
using Veggerby.Ignition.MongoDb;
using Veggerby.Ignition.Postgres;
using Veggerby.Ignition.RabbitMq;
using Veggerby.Ignition.Redis;
using Veggerby.Ignition.SqlServer;

namespace TestcontainersDemo;

/// <summary>
/// Comprehensive Testcontainers sample demonstrating:
/// - Multi-stage ignition (container startup → databases → caches → message queues → application services)
/// - Sequential and parallel execution modes
/// - All major integration packages
/// - Real infrastructure via Testcontainers orchestrated by Ignition
/// - Health check integration
/// - Full observability (tracing, logging, metrics)
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Veggerby.Ignition - Testcontainers Multi-Service Demo                     ║");
        Console.WriteLine("║                                                                            ║");
        Console.WriteLine("║  Demonstrates:                                                             ║");
        Console.WriteLine("║    • Multi-stage execution (5 stages)                                      ║");
        Console.WriteLine("║    • Ignition orchestrating container startup itself!                      ║");
        Console.WriteLine("║    • PostgreSQL, Redis, RabbitMQ, MongoDB, SQL Server                      ║");
        Console.WriteLine("║    • Sequential & parallel execution                                       ║");
        Console.WriteLine("║    • Full observability                                                    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Build and run the application
        var builder = Host.CreateApplicationBuilder(args);
        
        // Create infrastructure manager (containers not started yet!)
        var infrastructure = new InfrastructureManager();
        builder.Services.AddSingleton(infrastructure);

        // Configure logging
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        // Configure Ignition with staged execution
        builder.Services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Staged;
            options.Policy = IgnitionPolicy.FailFast;
            options.GlobalTimeout = TimeSpan.FromSeconds(120);
            options.CancelOnGlobalTimeout = true;
            options.EnableTracing = true;
            options.SlowHandleLogCount = 5;
        });

        // Stage 0: Start Infrastructure Containers (parallel within stage)
        Console.WriteLine("📋 Registering Stage 0: Infrastructure Startup (Testcontainers)");
        builder.Services.AddIgnitionStage(0, stage => stage
            .AddTaskSignal("postgres-container", async ct => await infrastructure.StartPostgresAsync())
            .AddTaskSignal("redis-container", async ct => await infrastructure.StartRedisAsync())
            .AddTaskSignal("rabbitmq-container", async ct => await infrastructure.StartRabbitMqAsync())
            .AddTaskSignal("mongodb-container", async ct => await infrastructure.StartMongoDbAsync())
            .AddTaskSignal("sqlserver-container", async ct => await infrastructure.StartSqlServerAsync()));

        // Stage 1: Databases (parallel within stage)
        // Use factory-based extensions for proper DI - signals instantiated when stage executes
        Console.WriteLine("📋 Registering Stage 1: Databases (PostgreSQL, SQL Server, MongoDB)");
        builder.Services.AddPostgresReadiness(
            sp => sp.GetRequiredService<InfrastructureManager>().PostgresConnectionString,
            options =>
            {
                options.Stage = 1;
                options.ValidationQuery = "SELECT 1";
                options.Timeout = TimeSpan.FromSeconds(30);
            });

        builder.Services.AddSqlServerReadiness(
            sp => sp.GetRequiredService<InfrastructureManager>().SqlServerConnectionString,
            options =>
            {
                options.Stage = 1;
                options.ValidationQuery = "SELECT 1";
                options.Timeout = TimeSpan.FromSeconds(30);
                options.MaxRetries = 10; // SQL Server can take longer to fully initialize
                options.RetryDelay = TimeSpan.FromMilliseconds(500);
            });

        builder.Services.AddMongoDbReadiness(
            sp => sp.GetRequiredService<InfrastructureManager>().MongoDbConnectionString,
            options =>
            {
                options.Stage = 1;
                options.DatabaseName = "testdb";
                options.Timeout = TimeSpan.FromSeconds(30);
            });

        // Stage 2: Caches (Redis)
        Console.WriteLine("📋 Registering Stage 2: Caches (Redis)");
        builder.Services.AddRedisReadiness(
            sp => sp.GetRequiredService<InfrastructureManager>().RedisConnectionString,
            options =>
            {
                options.Stage = 2;
                options.VerificationStrategy = RedisVerificationStrategy.Ping;
                options.Timeout = TimeSpan.FromSeconds(30);
            });

        // Stage 3: Message Queues (RabbitMQ)
        Console.WriteLine("📋 Registering Stage 3: Message Queues (RabbitMQ)");
        builder.Services.AddRabbitMqReadiness(
            sp => sp.GetRequiredService<InfrastructureManager>().RabbitMqConnectionString,
            options =>
            {
                options.Stage = 3;
                options.Timeout = TimeSpan.FromSeconds(30);
            });

        // Stage 4: Application Services (simulated)
        Console.WriteLine("📋 Registering Stage 4: Application Services");
        builder.Services.AddIgnitionStage(4, stage => stage
            .AddTaskSignal("app-initialization", async ct =>
            {
                Console.WriteLine("   🚀 Initializing application components...");
                await Task.Delay(500, ct);
                Console.WriteLine("   ✅ Application components ready");
            })
            .AddTaskSignal("cache-warmup", async ct =>
            {
                Console.WriteLine("   🔥 Warming up caches...");
                await Task.Delay(800, ct);
                Console.WriteLine("   ✅ Caches warmed");
            })
            .AddTaskSignal("background-services", async ct =>
            {
                Console.WriteLine("   ⚙️  Starting background services...");
                await Task.Delay(300, ct);
                Console.WriteLine("   ✅ Background services started");
            }));

        var app = builder.Build();

        try
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  STARTING IGNITION SEQUENCE");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
            
            var startTime = DateTime.UtcNow;
            await coordinator.WaitAllAsync();
            var duration = DateTime.UtcNow - startTime;

            var result = await coordinator.GetResultAsync();

            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  IGNITION RESULTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            Console.WriteLine($"  Total Duration:      {result.TotalDuration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"  Timed Out:           {(result.TimedOut ? "YES ⚠️" : "NO ✅")}");
            Console.WriteLine();

            // Group results by stage
            var groupedByStage = result.Results
                .GroupBy(r => GetStageNumber(r.Name))
                .OrderBy(g => g.Key);

            foreach (var stageGroup in groupedByStage)
            {
                var stageName = stageGroup.Key switch
                {
                    0 => "Stage 0: Infrastructure Startup",
                    1 => "Stage 1: Databases",
                    2 => "Stage 2: Caches",
                    3 => "Stage 3: Message Queues",
                    4 => "Stage 4: Application Services",
                    _ => "Other"
                };

                Console.WriteLine($"  {stageName}:");
                foreach (var signal in stageGroup.OrderBy(s => s.Name))
                {
                    var statusIcon = signal.Status switch
                    {
                        IgnitionSignalStatus.Succeeded => "✅",
                        IgnitionSignalStatus.Failed => "❌",
                        IgnitionSignalStatus.TimedOut => "⏱️",
                        _ => "❓"
                    };

                    Console.WriteLine($"    {statusIcon} {signal.Name,-30} {signal.Duration.TotalMilliseconds,6:F0}ms");
                }
                Console.WriteLine();
            }

            var succeeded = result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded);
            var failed = result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed);
            var timedOut = result.Results.Count(r => r.Status == IgnitionSignalStatus.TimedOut);

            Console.WriteLine("  Summary:");
            Console.WriteLine($"    Total Signals:       {result.Results.Count}");
            Console.WriteLine($"    ✅ Succeeded:        {succeeded}");
            Console.WriteLine($"    ❌ Failed:           {failed}");
            Console.WriteLine($"    ⏱️  Timed Out:        {timedOut}");
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════════");

            if (result.TimedOut || result.Results.Any(r => r.Status == IgnitionSignalStatus.Failed))
            {
                Console.WriteLine();
                Console.WriteLine("⚠️  Some signals failed or timed out. Check logs for details.");
                Environment.ExitCode = 1;
            }
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("🧹 Cleaning up Testcontainers...");
            await infrastructure.StopAsync();
            Console.WriteLine("✅ Cleanup complete!");
        }
    }

    private static int GetStageNumber(string signalName)
    {
        return signalName switch
        {
            var name when name.EndsWith("-container") => 0,
            var name when name.Contains("postgres") => 1,
            var name when name.Contains("sqlserver") => 1,
            var name when name.Contains("mongodb") => 1,
            var name when name.Contains("redis") => 2,
            var name when name.Contains("rabbitmq") => 3,
            _ => 4
        };
    }
}
