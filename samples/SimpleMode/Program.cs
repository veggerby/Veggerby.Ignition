using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace SimpleMode;

/// <summary>
/// Demonstrates the Simple Mode API for Veggerby.Ignition.
/// This example shows how to configure startup readiness in fewer than 10 lines of code.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Simple Mode API Demo ===\n");

        // Example 1: Minimal Web API setup (< 10 lines)
        await MinimalWebApiExample();

        Console.WriteLine("\n---\n");

        // Example 2: Worker Service with fail-fast
        await WorkerServiceExample();

        Console.WriteLine("\n---\n");

        // Example 3: CLI Application with sequential execution
        await CliApplicationExample();
    }

    private static async Task MinimalWebApiExample()
    {
        Console.WriteLine("Example 1: Minimal Web API Setup");
        Console.WriteLine("==================================\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // Simple Mode API: < 10 lines to production-ready readiness!
                services.AddSimpleIgnition(ignition => ignition
                    .UseWebApiProfile()
                    .AddSignal("database", async ct =>
                    {
                        Console.WriteLine("  [database] Connecting...");
                        await Task.Delay(800, ct);
                        Console.WriteLine("  [database] Connected!");
                    })
                    .AddSignal("cache", async ct =>
                    {
                        Console.WriteLine("  [cache] Warming up...");
                        await Task.Delay(600, ct);
                        Console.WriteLine("  [cache] Ready!");
                    })
                    .AddSignal("external-api", async ct =>
                    {
                        Console.WriteLine("  [external-api] Checking health...");
                        await Task.Delay(400, ct);
                        Console.WriteLine("  [external-api] Healthy!");
                    }));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        Console.WriteLine("Starting Web API initialization...\n");

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();

        var result = await coordinator.GetResultAsync();

        Console.WriteLine($"\n✓ All signals completed in {result.TotalDuration.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Profile: WebApi (Parallel, BestEffort, 30s global timeout)");
        Console.WriteLine($"  Signals: {result.Results.Count} succeeded");
    }

    private static async Task WorkerServiceExample()
    {
        Console.WriteLine("Example 2: Worker Service (Fail-Fast)");
        Console.WriteLine("======================================\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSimpleIgnition(ignition => ignition
                    .UseWorkerProfile()
                    .AddSignal("message-queue", async ct =>
                    {
                        Console.WriteLine("  [message-queue] Connecting...");
                        await Task.Delay(500, ct);
                        Console.WriteLine("  [message-queue] Connected!");
                    })
                    .AddSignal("storage", async ct =>
                    {
                        Console.WriteLine("  [storage] Initializing...");
                        await Task.Delay(700, ct);
                        Console.WriteLine("  [storage] Ready!");
                    })
                    .AddSignal("telemetry", async ct =>
                    {
                        Console.WriteLine("  [telemetry] Setting up...");
                        await Task.Delay(300, ct);
                        Console.WriteLine("  [telemetry] Active!");
                    }));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        Console.WriteLine("Starting Worker Service initialization...\n");

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();

        var result = await coordinator.GetResultAsync();

        Console.WriteLine($"\n✓ All signals completed in {result.TotalDuration.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Profile: Worker (Parallel, FailFast, 60s global timeout)");
        Console.WriteLine($"  Signals: {result.Results.Count} succeeded");
    }

    private static async Task CliApplicationExample()
    {
        Console.WriteLine("Example 3: CLI Application (Sequential)");
        Console.WriteLine("========================================\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSimpleIgnition(ignition => ignition
                    .UseCliProfile()
                    .AddSignal("config-load", async ct =>
                    {
                        Console.WriteLine("  [1/3] Loading configuration...");
                        await Task.Delay(300, ct);
                        Console.WriteLine("        Configuration loaded");
                    })
                    .AddSignal("validate-args", async ct =>
                    {
                        Console.WriteLine("  [2/3] Validating arguments...");
                        await Task.Delay(200, ct);
                        Console.WriteLine("        Arguments valid");
                    })
                    .AddSignal("prepare-output", async ct =>
                    {
                        Console.WriteLine("  [3/3] Preparing output...");
                        await Task.Delay(250, ct);
                        Console.WriteLine("        Output ready");
                    }));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        Console.WriteLine("Starting CLI initialization...\n");

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();

        var result = await coordinator.GetResultAsync();

        Console.WriteLine($"\n✓ All signals completed in {result.TotalDuration.TotalMilliseconds:F0}ms");
        Console.WriteLine($"  Profile: CLI (Sequential, FailFast, 15s global timeout)");
        Console.WriteLine($"  Signals: {result.Results.Count} succeeded");
    }
}
