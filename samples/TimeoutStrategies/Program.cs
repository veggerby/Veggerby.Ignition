using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace TimeoutStrategies;

#region Timeout Strategies

/// <summary>
/// A lenient timeout strategy that gives all signals generous timeouts.
/// Useful for development environments or when network conditions are unpredictable.
/// </summary>
public sealed class LenientTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly TimeSpan _defaultTimeout;

    public LenientTimeoutStrategy(TimeSpan defaultTimeout)
    {
        _defaultTimeout = defaultTimeout;
    }

    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        // Always give generous timeout, don't cancel immediately
        return (_defaultTimeout, cancelImmediately: false);
    }
}

/// <summary>
/// A strict timeout strategy that enforces tight timeouts and cancels immediately.
/// Useful for production environments where fast failures are preferred.
/// </summary>
public sealed class StrictTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly TimeSpan _timeout;

    public StrictTimeoutStrategy(TimeSpan timeout)
    {
        _timeout = timeout;
    }

    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        // Use strict timeout and cancel immediately on timeout
        return (_timeout, cancelImmediately: true);
    }
}

/// <summary>
/// An adaptive timeout strategy that assigns different timeouts based on signal characteristics.
/// Signals with "slow" or "heavy" in their name get longer timeouts.
/// </summary>
public sealed class AdaptiveTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly TimeSpan _fastTimeout;
    private readonly TimeSpan _slowTimeout;

    public AdaptiveTimeoutStrategy(TimeSpan fastTimeout, TimeSpan slowTimeout)
    {
        _fastTimeout = fastTimeout;
        _slowTimeout = slowTimeout;
    }

    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        // Check if signal is known to be slow (by naming convention)
        bool isSlowSignal = signal.Name.Contains("slow", StringComparison.OrdinalIgnoreCase)
            || signal.Name.Contains("heavy", StringComparison.OrdinalIgnoreCase)
            || signal.Name.Contains("warmup", StringComparison.OrdinalIgnoreCase);

        var timeout = isSlowSignal ? _slowTimeout : _fastTimeout;
        return (timeout, cancelImmediately: true);
    }
}

/// <summary>
/// A category-based timeout strategy that assigns timeouts based on signal type prefixes.
/// - Database signals (db:*): 5 seconds
/// - Cache signals (cache:*): 3 seconds
/// - Service signals (svc:*): 2 seconds
/// - Everything else: signal's own timeout
/// </summary>
public sealed class CategoryTimeoutStrategy : IIgnitionTimeoutStrategy
{
    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        TimeSpan? timeout = signal.Name switch
        {
            var name when name.StartsWith("db:", StringComparison.OrdinalIgnoreCase) => TimeSpan.FromSeconds(5),
            var name when name.StartsWith("cache:", StringComparison.OrdinalIgnoreCase) => TimeSpan.FromSeconds(3),
            var name when name.StartsWith("svc:", StringComparison.OrdinalIgnoreCase) => TimeSpan.FromSeconds(2),
            _ => signal.Timeout // Fall back to signal's own timeout
        };

        return (timeout, cancelImmediately: true);
    }
}

#endregion

#region Test Signals

/// <summary>
/// A simulated database signal with configurable delay.
/// </summary>
public class DatabaseSignal : IIgnitionSignal
{
    private readonly int _delayMs;

    public DatabaseSignal(int delayMs = 800)
    {
        _delayMs = delayMs;
    }

    public string Name => "db:connection";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(2); // Signal's own timeout

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"   🔌 [{Name}] Connecting to database...");
        await Task.Delay(_delayMs, cancellationToken);
        Console.WriteLine($"   ✅ [{Name}] Database connected ({_delayMs}ms)");
    }
}

/// <summary>
/// A simulated cache warming signal - intentionally slow.
/// </summary>
public class CacheWarmupSignal : IIgnitionSignal
{
    private readonly int _delayMs;

    public CacheWarmupSignal(int delayMs = 1500)
    {
        _delayMs = delayMs;
    }

    public string Name => "cache:slow-warmup";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(3);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"   🔥 [{Name}] Warming cache (this is slow)...");
        await Task.Delay(_delayMs, cancellationToken);
        Console.WriteLine($"   ✅ [{Name}] Cache warmed ({_delayMs}ms)");
    }
}

/// <summary>
/// A simulated external service check - fast operation.
/// </summary>
public class ServiceCheckSignal : IIgnitionSignal
{
    private readonly int _delayMs;

    public ServiceCheckSignal(int delayMs = 300)
    {
        _delayMs = delayMs;
    }

    public string Name => "svc:health-check";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(1);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"   🏥 [{Name}] Checking service health...");
        await Task.Delay(_delayMs, cancellationToken);
        Console.WriteLine($"   ✅ [{Name}] Service healthy ({_delayMs}ms)");
    }
}

/// <summary>
/// A very slow signal that will timeout with strict strategies.
/// </summary>
public class VerySlowSignal : IIgnitionSignal
{
    private readonly int _delayMs;

    public VerySlowSignal(int delayMs = 3000)
    {
        _delayMs = delayMs;
    }

    public string Name => "svc:heavy-initialization";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"   ⏳ [{Name}] Performing heavy initialization...");
        try
        {
            await Task.Delay(_delayMs, cancellationToken);
            Console.WriteLine($"   ✅ [{Name}] Heavy initialization complete ({_delayMs}ms)");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"   ❌ [{Name}] Cancelled during initialization");
            throw;
        }
    }
}

#endregion

/// <summary>
/// Demonstrates timeout strategy plugins by running the same startup scenario
/// with different timeout strategies to show the different outcomes.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║             Ignition Timeout Strategies Sample                            ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This sample demonstrates how different timeout strategies affect startup");
        Console.WriteLine("outcomes when running the SAME set of signals.");
        Console.WriteLine();
        Console.WriteLine("Signals used in all scenarios:");
        Console.WriteLine("  - db:connection (800ms) - Database connection");
        Console.WriteLine("  - cache:slow-warmup (1500ms) - Cache warming (slow)");
        Console.WriteLine("  - svc:health-check (300ms) - Service health check (fast)");
        Console.WriteLine("  - svc:heavy-initialization (3000ms) - Heavy initialization (very slow)");
        Console.WriteLine();

        // Run all strategy examples
        await RunWithDefaultStrategy();
        await RunWithLenientStrategy();
        await RunWithStrictStrategy();
        await RunWithAdaptiveStrategy();
        await RunWithCategoryStrategy();

        Console.WriteLine();
        Console.WriteLine("═════════════════════════════════════════════════════════════════════════════");
        Console.WriteLine("Summary: The same signals produce different outcomes based on timeout strategy!");
        Console.WriteLine("═════════════════════════════════════════════════════════════════════════════");
    }

    /// <summary>
    /// Scenario 1: No custom timeout strategy (uses signal-defined timeouts).
    /// </summary>
    private static async Task RunWithDefaultStrategy()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Scenario 1: DEFAULT Strategy (no custom strategy)                          │");
        Console.WriteLine("│ Uses each signal's own Timeout property                                    │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = CreateHost(configureStrategy: null);
        await ExecuteAndReport(host);
    }

    /// <summary>
    /// Scenario 2: Lenient strategy - gives all signals 10 seconds.
    /// </summary>
    private static async Task RunWithLenientStrategy()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Scenario 2: LENIENT Strategy                                               │");
        Console.WriteLine("│ All signals get 10 seconds timeout, no immediate cancellation              │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = CreateHost(services =>
        {
            services.AddIgnitionTimeoutStrategy(new LenientTimeoutStrategy(TimeSpan.FromSeconds(10)));
        });
        await ExecuteAndReport(host);
    }

    /// <summary>
    /// Scenario 3: Strict strategy - only 1 second per signal.
    /// </summary>
    private static async Task RunWithStrictStrategy()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Scenario 3: STRICT Strategy                                                │");
        Console.WriteLine("│ All signals get only 1 second timeout, immediate cancellation              │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = CreateHost(services =>
        {
            services.AddIgnitionTimeoutStrategy(new StrictTimeoutStrategy(TimeSpan.FromSeconds(1)));
        });
        await ExecuteAndReport(host);
    }

    /// <summary>
    /// Scenario 4: Adaptive strategy - slow signals get more time.
    /// </summary>
    private static async Task RunWithAdaptiveStrategy()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Scenario 4: ADAPTIVE Strategy                                              │");
        Console.WriteLine("│ Fast signals: 1s timeout | Slow/heavy/warmup signals: 5s timeout           │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = CreateHost(services =>
        {
            services.AddIgnitionTimeoutStrategy(
                new AdaptiveTimeoutStrategy(
                    fastTimeout: TimeSpan.FromSeconds(1),
                    slowTimeout: TimeSpan.FromSeconds(5)));
        });
        await ExecuteAndReport(host);
    }

    /// <summary>
    /// Scenario 5: Category-based strategy - timeouts based on signal type prefix.
    /// </summary>
    private static async Task RunWithCategoryStrategy()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Scenario 5: CATEGORY Strategy                                              │");
        Console.WriteLine("│ db:* = 5s | cache:* = 3s | svc:* = 2s | others = signal's own              │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var host = CreateHost(services =>
        {
            services.AddIgnitionTimeoutStrategy(new CategoryTimeoutStrategy());
        });
        await ExecuteAndReport(host);
    }

    /// <summary>
    /// Creates a host with the specified timeout strategy configuration.
    /// </summary>
    private static IHost CreateHost(Action<IServiceCollection>? configureStrategy)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Configure ignition with consistent settings across all scenarios
                services.AddIgnition(options =>
                {
                    options.ExecutionMode = IgnitionExecutionMode.Parallel;
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(15);
                    options.CancelIndividualOnTimeout = true;
                });

                // Register the same signals for all scenarios
                services.AddTransient<IIgnitionSignal>(_ => new DatabaseSignal(800));
                services.AddTransient<IIgnitionSignal>(_ => new CacheWarmupSignal(1500));
                services.AddTransient<IIgnitionSignal>(_ => new ServiceCheckSignal(300));
                services.AddTransient<IIgnitionSignal>(_ => new VerySlowSignal(3000));

                // Apply custom timeout strategy if provided
                configureStrategy?.Invoke(services);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                // We use Console.WriteLine for demo output, disable standard logging
            })
            .Build();
    }

    /// <summary>
    /// Executes the ignition coordinator and reports results.
    /// </summary>
    private static async Task ExecuteAndReport(IHost host)
    {
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            await coordinator.WaitAllAsync();
        }
        catch (Exception)
        {
            // BestEffort policy handles failures gracefully
        }

        var result = await coordinator.GetResultAsync();

        Console.WriteLine();
        Console.WriteLine("   ════════════════════════════════════════════════════════════════════");
        Console.WriteLine("    RESULTS");
        Console.WriteLine("   ════════════════════════════════════════════════════════════════════");
        Console.WriteLine($"    Total Duration: {result.TotalDuration.TotalMilliseconds,8:F0}ms");
        Console.WriteLine($"    Timed Out:      {(result.TimedOut ? "YES" : "NO ")}");
        Console.WriteLine("   ────────────────────────────────────────────────────────────────────");

        var succeeded = result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded);
        var timedOut = result.Results.Count(r => r.Status == IgnitionSignalStatus.TimedOut);
        var failed = result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed);

        Console.WriteLine($"    ✅ Succeeded:      {succeeded}/{result.Results.Count}");
        if (timedOut > 0)
        {
            Console.WriteLine($"    ⏰ Timed Out:      {timedOut}");
        }
        if (failed > 0)
        {
            Console.WriteLine($"    ❌ Failed:         {failed}");
        }

        Console.WriteLine("   ────────────────────────────────────────────────────────────────────");
        Console.WriteLine("    Signal Details:");
        Console.WriteLine();

        foreach (var signalResult in result.Results.OrderBy(r => r.Name))
        {
            var icon = signalResult.Status switch
            {
                IgnitionSignalStatus.Succeeded => "✅",
                IgnitionSignalStatus.TimedOut => "⏰",
                IgnitionSignalStatus.Failed => "❌",
                _ => "❓"
            };

            var paddedName = signalResult.Name.PadRight(25);
            var paddedStatus = signalResult.Status.ToString().PadRight(10);
            Console.WriteLine($"      {icon} {paddedName} {paddedStatus} ({signalResult.Duration.TotalMilliseconds,6:F0}ms)");
        }

        Console.WriteLine();
        Console.WriteLine("   ════════════════════════════════════════════════════════════════════");
        Console.WriteLine();
    }
}
