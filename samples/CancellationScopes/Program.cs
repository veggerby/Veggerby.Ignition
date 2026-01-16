using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace CancellationScopes;

/// <summary>
/// Demonstrates hierarchical cancellation scopes with a database cluster scenario.
/// Shows how primary database failure can cancel dependent replica initialization.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Cancellation Scopes Sample ===\n");
        Console.WriteLine("This sample demonstrates hierarchical cancellation with");
        Console.WriteLine("a database cluster where primary failure cancels replicas.\n");

        await RunSuccessScenario();
        Console.WriteLine();
        await RunPrimaryFailureScenario();
    }

    /// <summary>
    /// Scenario 1: All signals succeed - no cancellation.
    /// </summary>
    private static async Task RunSuccessScenario()
    {
        Console.WriteLine("🏗️  Scenario 1: Success - Primary and Replicas Initialize");
        Console.WriteLine(new string('=', 60));

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                });

                // Create cancellation scopes
                var primaryScope = new CancellationScope("primary-db");
                var replicaScope = primaryScope.CreateChildScope("replicas");

                // Primary database signal - if it fails, replicas should be cancelled
                services.AddIgnitionSignalWithScope(
                    IgnitionSignal.FromTaskFactory(
                        "primary-db:connect",
                        async ct =>
                        {
                            Console.WriteLine("   📡 Connecting to primary database...");
                            await Task.Delay(1000, ct);
                            Console.WriteLine("   ✅ Primary database connected");
                        },
                        TimeSpan.FromSeconds(10)),
                    primaryScope,
                    cancelScopeOnFailure: true);

                // Replica 1 - depends on primary scope
                services.AddIgnitionSignalWithScope(
                    IgnitionSignal.FromTaskFactory(
                        "replica-1:connect",
                        async ct =>
                        {
                            Console.WriteLine("   📡 Connecting to replica 1...");
                            await Task.Delay(800, ct);
                            Console.WriteLine("   ✅ Replica 1 connected");
                        },
                        TimeSpan.FromSeconds(10)),
                    replicaScope,
                    cancelScopeOnFailure: false);

                // Replica 2 - depends on primary scope
                services.AddIgnitionSignalWithScope(
                    IgnitionSignal.FromTaskFactory(
                        "replica-2:connect",
                        async ct =>
                        {
                            Console.WriteLine("   📡 Connecting to replica 2...");
                            await Task.Delay(800, ct);
                            Console.WriteLine("   ✅ Replica 2 connected");
                        },
                        TimeSpan.FromSeconds(10)),
                    replicaScope,
                    cancelScopeOnFailure: false);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        await ExecuteAndReport(host, "Success Scenario");
    }

    /// <summary>
    /// Scenario 2: Primary fails - replicas should be cancelled.
    /// </summary>
    private static async Task RunPrimaryFailureScenario()
    {
        Console.WriteLine("🏗️  Scenario 2: Primary Failure - Replicas Should Cancel");
        Console.WriteLine(new string('=', 60));

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                });

                // Create cancellation scopes
                var primaryScope = new CancellationScope("primary-db");
                var replicaScope = primaryScope.CreateChildScope("replicas");

                // Primary database signal - FAILS to demonstrate cancellation
                services.AddIgnitionSignalWithScope(
                    IgnitionSignal.FromTaskFactory(
                        "primary-db:connect",
                        async ct =>
                        {
                            Console.WriteLine("   📡 Connecting to primary database...");
                            await Task.Delay(500, ct);
                            Console.WriteLine("   ❌ Primary database connection failed!");
                            throw new InvalidOperationException("Primary database unavailable");
                        },
                        TimeSpan.FromSeconds(10)),
                    primaryScope,
                    cancelScopeOnFailure: true);

                // Replica 1 - should be cancelled when primary fails
                services.AddIgnitionSignalWithScope(
                    IgnitionSignal.FromTaskFactory(
                        "replica-1:connect",
                        async ct =>
                        {
                            Console.WriteLine("   📡 Connecting to replica 1...");
                            await Task.Delay(2000, ct);
                            Console.WriteLine("   ✅ Replica 1 connected");
                        },
                        TimeSpan.FromSeconds(10)),
                    replicaScope,
                    cancelScopeOnFailure: false);

                // Replica 2 - should be cancelled when primary fails
                services.AddIgnitionSignalWithScope(
                    IgnitionSignal.FromTaskFactory(
                        "replica-2:connect",
                        async ct =>
                        {
                            Console.WriteLine("   📡 Connecting to replica 2...");
                            await Task.Delay(2000, ct);
                            Console.WriteLine("   ✅ Replica 2 connected");
                        },
                        TimeSpan.FromSeconds(10)),
                    replicaScope,
                    cancelScopeOnFailure: false);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        await ExecuteAndReport(host, "Primary Failure Scenario");
    }

    private static async Task ExecuteAndReport(IHost host, string scenarioName)
    {
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            await coordinator.WaitAllAsync();
        }
        catch (AggregateException)
        {
            // Expected in failure scenario
        }

        var result = await coordinator.GetResultAsync();

        Console.WriteLine($"\n📊 {scenarioName} Results:");
        Console.WriteLine($"   Total Duration: {result.TotalDuration.TotalMilliseconds:F0}ms");

        var succeeded = result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded);
        var failed = result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed);
        var cancelled = result.Results.Count(r => r.Status == IgnitionSignalStatus.Cancelled);

        Console.WriteLine($"   Succeeded: {succeeded}");
        Console.WriteLine($"   Failed: {failed}");
        Console.WriteLine($"   Cancelled: {cancelled}");

        Console.WriteLine("\n📋 Signal Details:");
        foreach (var signalResult in result.Results.OrderBy(r => r.Name))
        {
            var icon = signalResult.Status switch
            {
                IgnitionSignalStatus.Succeeded => "✅",
                IgnitionSignalStatus.Failed => "❌",
                IgnitionSignalStatus.Cancelled => "🚫",
                _ => "❓"
            };

            Console.Write($"   {icon} {signalResult.Name}: {signalResult.Status}");

            if (signalResult.Exception != null)
            {
                Console.Write($" - {signalResult.Exception.Message}");
            }

            Console.WriteLine();
        }

        var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
        Console.WriteLine($"\n{(overallSuccess ? "✅" : "⚠️ ")} Overall Status: {(overallSuccess ? "SUCCESS" : "PARTIAL SUCCESS / FAILED")}");

        if (!overallSuccess && cancelled > 0)
        {
            Console.WriteLine("\n📚 Cancellation Scope Behavior:");
            Console.WriteLine("   • Primary failure triggered scope cancellation");
            Console.WriteLine("   • Replica signals were cancelled before completion");
            Console.WriteLine("   • This prevents wasted work on dependent resources");
        }
    }
}
