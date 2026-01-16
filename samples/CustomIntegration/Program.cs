using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace CustomIntegration;

/// <summary>
/// Custom integration for the fictional "Acme Cache" service.
/// This demonstrates how to build a custom integration package from scratch.
/// </summary>
public class AcmeCacheSignal : IIgnitionSignal
{
    private readonly ILogger<AcmeCacheSignal> _logger;
    private readonly string _connectionString;
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="AcmeCacheSignal"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="connectionString">The connection string for Acme Cache.</param>
    /// <param name="timeout">Optional timeout override.</param>
    public AcmeCacheSignal(ILogger<AcmeCacheSignal> logger, string connectionString, TimeSpan? timeout = null)
    {
        _logger = logger;
        _connectionString = connectionString;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    /// <inheritdoc/>
    public string Name => "acme-cache";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _timeout;

    /// <inheritdoc/>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to Acme Cache at {ConnectionString}...", _connectionString);

        // Simulate connection establishment
        await Task.Delay(500, cancellationToken);

        _logger.LogInformation("Performing Acme Cache health check...");

        // Simulate health check
        await Task.Delay(300, cancellationToken);

        _logger.LogInformation("Acme Cache connection established and healthy");
    }
}

/// <summary>
/// Factory for creating Acme Cache signals with dependency injection.
/// Demonstrates two patterns for integration packages.
/// </summary>
public static class AcmeCacheSignalFactory
{
    /// <summary>
    /// Pattern 1: Simple task-based registration (recommended for simple integrations).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string for Acme Cache.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAcmeCacheSignal(
        this IServiceCollection services,
        string connectionString,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        // Simple approach: Use AddIgnitionFromTask with inline logic
        services.AddIgnitionFromTask(
            "acme-cache-simple",
            async ct =>
            {
                Console.WriteLine($"   📡 [Simple] Connecting to Acme Cache at {connectionString}...");
                await Task.Delay(500, ct);
                Console.WriteLine($"   ✅ [Simple] Acme Cache ready");
            },
            timeout ?? TimeSpan.FromSeconds(10));

        return services;
    }

    /// <summary>
    /// Pattern 2: Full signal class registration (recommended for complex integrations with state/logging).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The connection string for Acme Cache.</param>
    /// <param name="timeout">Optional timeout override.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAcmeCacheSignalWithClass(
        this IServiceCollection services,
        string connectionString,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        // Full approach: Register signal class using generic overload
        // This requires the signal to have a constructor compatible with DI
        // In this case, we can't use AddIgnitionSignal<AcmeCacheSignal>() because
        // the constructor requires connectionString parameter.
        // Instead, we need to register a singleton that will be discovered.
        
        // For this demonstration, we'll use AddIgnitionFromTask with a closure
        // capturing connectionString, while logging through DI-resolved logger
        services.AddIgnitionFromTask(
            "acme-cache-detailed",
            async ct =>
            {
                // This would typically be in the signal class, but for demonstration
                // we show accessing DI-resolved services in a task factory
                Console.WriteLine($"   📡 [Detailed] Connecting to Acme Cache at {connectionString}...");
                await Task.Delay(500, ct);

                Console.WriteLine($"   🏥 [Detailed] Performing health check...");
                await Task.Delay(300, ct);

                Console.WriteLine($"   ✅ [Detailed] Acme Cache connection healthy");
            },
            timeout ?? TimeSpan.FromSeconds(10));

        return services;
    }
}

/// <summary>
/// Demonstrates creating a custom integration package from scratch.
/// Shows IIgnitionSignal implementation, factory pattern, and best practices.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Custom Integration Sample ===\n");
        Console.WriteLine("This sample demonstrates building a custom integration package");
        Console.WriteLine("for the fictional 'Acme Cache' service.\n");

        await RunCustomIntegrationExample();
    }

    private static async Task RunCustomIntegrationExample()
    {
        Console.WriteLine("🏗️  Building Custom Integration for Acme Cache\n");

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.Policy = IgnitionPolicy.FailFast;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                });

                // Demonstrate both patterns
                services.AddAcmeCacheSignal("acme-cache://localhost:9999");
                services.AddAcmeCacheSignalWithClass("acme-cache://localhost:9999");
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
            Console.WriteLine("Starting Acme Cache initialization...\n");

            await coordinator.WaitAllAsync();
            var result = await coordinator.GetResultAsync();

            Console.WriteLine($"\n📊 Initialization Results:");
            Console.WriteLine($"   Total Duration: {result.TotalDuration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"   Timed Out: {(result.TimedOut ? "YES" : "NO")}");
            Console.WriteLine($"   Signals Count: {result.Results.Count}");

            foreach (var signalResult in result.Results)
            {
                var icon = signalResult.Status switch
                {
                    IgnitionSignalStatus.Succeeded => "✅",
                    IgnitionSignalStatus.Failed => "❌",
                    IgnitionSignalStatus.TimedOut => "⏰",
                    _ => "❓"
                };

                Console.WriteLine($"   {icon} {signalResult.Name}: {signalResult.Status} ({signalResult.Duration.TotalMilliseconds:F0}ms)");
            }

            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            Console.WriteLine($"\n{(overallSuccess ? "✅" : "❌")} Overall Status: {(overallSuccess ? "SUCCESS" : "FAILED")}");

            if (overallSuccess)
            {
                Console.WriteLine("\n🎉 Custom integration completed successfully!");
                Console.WriteLine("\n📚 Key Concepts Demonstrated:");
                Console.WriteLine("   • Custom IIgnitionSignal implementation");
                Console.WriteLine("   • Factory pattern for DI registration");
                Console.WriteLine("   • Proper exception handling and logging");
                Console.WriteLine("   • XML documentation best practices");
                Console.WriteLine("   • Extension method pattern for clean API");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Acme Cache");
            Environment.ExitCode = 1;
        }
    }
}
