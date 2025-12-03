using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Veggerby.Ignition;

namespace Replay;

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
/// A slow signal for demonstration of timeouts.
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
/// Demonstrates the Recording and Replay features for diagnosing startup issues,
/// CI regression detection, and what-if simulations.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                IGNITION RECORDING & REPLAY SAMPLE                            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This sample demonstrates how to record ignition runs for later analysis,");
        Console.WriteLine("validate recordings, compare recordings for regression detection, and");
        Console.WriteLine("simulate what-if scenarios.");
        Console.WriteLine();

        // Run all examples
        var firstRecording = await RunRecordingExample();
        Console.WriteLine();
        await RunValidationExample(firstRecording);
        Console.WriteLine();
        var secondRecording = await RunSecondRecordingForComparison();
        await RunComparisonExample(firstRecording, secondRecording);
        Console.WriteLine();
        await RunWhatIfSimulationExample(firstRecording);
        Console.WriteLine();
        await RunAnalysisExample(firstRecording);
    }

    /// <summary>
    /// Example 1: Recording an ignition run.
    /// </summary>
    private static async Task<IgnitionRecording> RunRecordingExample()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 1: RECORDING an Ignition Run                                        │");
        Console.WriteLine("│ Shows how to capture timing, status, and configuration for later analysis   │");
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

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        var options = host.Services.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();

        // Export to recording with metadata
        var recording = result.ExportRecording(
            options: options,
            finalState: coordinator.State,
            metadata: new Dictionary<string, string>
            {
                ["environment"] = "development",
                ["version"] = "1.0.0",
                ["hostname"] = Environment.MachineName,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            });

        Console.WriteLine("📼 RECORDING CAPTURED:");
        Console.WriteLine($"   Recording ID: {recording.RecordingId}");
        Console.WriteLine($"   Recorded At:  {recording.RecordedAt}");
        Console.WriteLine($"   Total Duration: {recording.TotalDurationMs:F1}ms");
        Console.WriteLine($"   Timed Out: {recording.TimedOut}");
        Console.WriteLine($"   Final State: {recording.FinalState}");
        Console.WriteLine();

        Console.WriteLine("📊 SIGNAL SUMMARY:");
        if (recording.Summary is not null)
        {
            Console.WriteLine($"   Total Signals: {recording.Summary.TotalSignals}");
            Console.WriteLine($"   ✅ Succeeded:  {recording.Summary.SucceededCount}");
            Console.WriteLine($"   ❌ Failed:     {recording.Summary.FailedCount}");
            Console.WriteLine($"   ⏰ Timed Out:  {recording.Summary.TimedOutCount}");
            Console.WriteLine($"   🐢 Slowest:    {recording.Summary.SlowestSignalName} ({recording.Summary.SlowestDurationMs:F1}ms)");
            Console.WriteLine($"   🚀 Fastest:    {recording.Summary.FastestSignalName} ({recording.Summary.FastestDurationMs:F1}ms)");
            Console.WriteLine($"   📊 Average:    {recording.Summary.AverageDurationMs:F1}ms");
            Console.WriteLine($"   🔄 Max Concurrency: {recording.Summary.MaxConcurrency}");
        }
        Console.WriteLine();

        Console.WriteLine("📝 RECORDED SIGNALS:");
        foreach (var signal in recording.Signals)
        {
            var statusIcon = signal.Status switch
            {
                "Succeeded" => "✅",
                "Failed" => "❌",
                "TimedOut" => "⏰",
                "Skipped" => "⏭️",
                "Cancelled" => "🚫",
                _ => "❓"
            };
            Console.WriteLine($"   {statusIcon} {signal.SignalName}: {signal.Status} ({signal.DurationMs:F1}ms)");
            Console.WriteLine($"      Start: {signal.StartMs:F1}ms → End: {signal.EndMs:F1}ms");
        }
        Console.WriteLine();

        // Show JSON export capability
        var json = recording.ToJson(indented: true);
        Console.WriteLine("📄 JSON EXPORT (first 600 chars):");
        Console.WriteLine("─".PadRight(60, '─'));
        Console.WriteLine(json.Length > 600 ? json.Substring(0, 600) + "..." : json);
        Console.WriteLine();
        Console.WriteLine($"   Full JSON length: {json.Length} characters");
        Console.WriteLine("   Save to file: File.WriteAllText(\"ignition-recording.json\", json)");
        Console.WriteLine();

        return recording;
    }

    /// <summary>
    /// Example 2: Validating a recording.
    /// </summary>
    private static async Task RunValidationExample(IgnitionRecording recording)
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 2: VALIDATING a Recording                                           │");
        Console.WriteLine("│ Shows how to check recordings for consistency and invariant violations      │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var replayer = new IgnitionReplayer(recording);
        var validation = replayer.Validate();

        Console.WriteLine("🔍 VALIDATION RESULTS:");
        Console.WriteLine($"   Is Valid: {(validation.IsValid ? "✅ Yes" : "❌ No")}");
        Console.WriteLine($"   Errors:   {validation.ErrorCount}");
        Console.WriteLine($"   Warnings: {validation.WarningCount}");
        Console.WriteLine();

        if (validation.Issues.Count == 0)
        {
            Console.WriteLine("   ✅ No issues found! Recording is consistent and valid.");
        }
        else
        {
            Console.WriteLine("📋 ISSUES FOUND:");
            foreach (var issue in validation.Issues)
            {
                var severityIcon = issue.Severity switch
                {
                    ReplayValidationSeverity.Error => "❌",
                    ReplayValidationSeverity.Warning => "⚠️",
                    _ => "ℹ️"
                };
                Console.WriteLine($"   {severityIcon} [{issue.Code}] {issue.Message}");
                if (issue.SignalName is not null)
                {
                    Console.WriteLine($"      Signal: {issue.SignalName}");
                }
                if (issue.Details is not null)
                {
                    Console.WriteLine($"      Details: {issue.Details}");
                }
            }
        }
        Console.WriteLine();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Create a second recording with slightly different timing for comparison.
    /// </summary>
    private static async Task<IgnitionRecording> RunSecondRecordingForComparison()
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Creating second recording for comparison...                                 │");
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

                // Use slightly modified signals to simulate different run
                services.AddTransient<IIgnitionSignal, DatabaseSignal>();
                services.AddTransient<IIgnitionSignal, CacheWarmupSignal>();
                services.AddTransient<IIgnitionSignal, ConfigurationSignal>();
                services.AddTransient<IIgnitionSignal, ExternalServiceSignal>();
            })
            .ConfigureLogging(logging => logging.ClearProviders())
            .Build();

        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
        var options = host.Services.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();

        var recording = result.ExportRecording(
            options: options,
            finalState: coordinator.State,
            metadata: new Dictionary<string, string>
            {
                ["environment"] = "staging",
                ["version"] = "1.0.1",
                ["hostname"] = Environment.MachineName
            });

        Console.WriteLine($"   Second recording captured (ID: {recording.RecordingId})");
        Console.WriteLine($"   Duration: {recording.TotalDurationMs:F1}ms");
        Console.WriteLine();

        return recording;
    }

    /// <summary>
    /// Example 3: Comparing two recordings (regression detection).
    /// </summary>
    private static async Task RunComparisonExample(IgnitionRecording baseline, IgnitionRecording current)
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 3: COMPARING Recordings (Regression Detection)                      │");
        Console.WriteLine("│ Shows how to detect performance regressions between runs                    │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var replayer = new IgnitionReplayer(baseline);
        var comparison = replayer.CompareTo(current);

        Console.WriteLine("📊 COMPARISON RESULTS:");
        Console.WriteLine($"   Baseline Duration:  {baseline.TotalDurationMs:F1}ms");
        Console.WriteLine($"   Current Duration:   {current.TotalDurationMs:F1}ms");
        Console.WriteLine($"   Difference:         {comparison.DurationDifferenceMs:+0.0;-0.0}ms ({comparison.DurationChangePercent:+0.0;-0.0}%)");
        Console.WriteLine();

        if (comparison.AddedSignals.Count > 0)
        {
            Console.WriteLine($"   ➕ Added Signals: {string.Join(", ", comparison.AddedSignals)}");
        }

        if (comparison.RemovedSignals.Count > 0)
        {
            Console.WriteLine($"   ➖ Removed Signals: {string.Join(", ", comparison.RemovedSignals)}");
        }
        Console.WriteLine();

        Console.WriteLine("📈 PER-SIGNAL COMPARISON:");
        foreach (var signalComp in comparison.SignalComparisons.OrderByDescending(c => Math.Abs(c.DurationChangePercent)))
        {
            var changeIcon = signalComp.DurationChangePercent switch
            {
                > 10 => "🔺",  // Significantly slower
                < -10 => "🔻", // Significantly faster
                _ => "➖"      // No significant change
            };

            Console.WriteLine($"   {changeIcon} {signalComp.SignalName}:");
            Console.WriteLine($"      Duration: {signalComp.Duration1Ms:F1}ms → {signalComp.Duration2Ms:F1}ms ({signalComp.DurationChangePercent:+0.0;-0.0}%)");

            if (signalComp.StatusChanged)
            {
                Console.WriteLine($"      ⚠️ Status changed: {signalComp.Status1} → {signalComp.Status2}");
            }
        }
        Console.WriteLine();

        // Identify potential regressions
        var regressions = comparison.SignalComparisons.Where(c => c.DurationChangePercent > 20).ToList();
        if (regressions.Count > 0)
        {
            Console.WriteLine("⚠️  POTENTIAL REGRESSIONS DETECTED:");
            foreach (var reg in regressions.OrderByDescending(r => r.DurationChangePercent))
            {
                Console.WriteLine($"   🔺 {reg.SignalName}: +{reg.DurationDifferenceMs:F0}ms ({reg.DurationChangePercent:+0}%)");
            }
            Console.WriteLine();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Example 4: What-if simulations.
    /// </summary>
    private static async Task RunWhatIfSimulationExample(IgnitionRecording recording)
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 4: WHAT-IF Simulations                                              │");
        Console.WriteLine("│ Shows how to simulate timeout and failure scenarios                         │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var replayer = new IgnitionReplayer(recording);

        // Simulate earlier timeout
        Console.WriteLine("🔮 SIMULATION 1: What if 'cache-warmup' had a 500ms timeout?");
        Console.WriteLine("─".PadRight(60, '─'));

        var cacheSignal = recording.Signals.FirstOrDefault(s => s.SignalName == "cache-warmup");
        if (cacheSignal is not null)
        {
            Console.WriteLine($"   Original duration: {cacheSignal.DurationMs:F1}ms");
            Console.WriteLine($"   Simulated timeout: 500ms");

            var timeoutSim = replayer.SimulateEarlierTimeout("cache-warmup", newTimeoutMs: 500);
            Console.WriteLine($"   Would timeout: {(timeoutSim.AffectedSignals.Count > 0 ? "YES" : "NO")}");

            if (timeoutSim.AffectedSignals.Count > 0)
            {
                Console.WriteLine($"   Affected signals: {string.Join(", ", timeoutSim.AffectedSignals)}");
            }
            Console.WriteLine();

            Console.WriteLine("   Simulated results:");
            foreach (var signal in timeoutSim.SimulatedSignals)
            {
                var statusIcon = signal.Status switch
                {
                    "Succeeded" => "✅",
                    "Failed" => "❌",
                    "TimedOut" => "⏰",
                    "Skipped" => "⏭️",
                    _ => "❓"
                };
                var changed = timeoutSim.AffectedSignals.Contains(signal.SignalName) ? " (changed)" : "";
                Console.WriteLine($"      {statusIcon} {signal.SignalName}: {signal.Status}{changed}");
            }
        }
        Console.WriteLine();

        // Simulate failure
        Console.WriteLine("🔮 SIMULATION 2: What if 'database-connection' failed?");
        Console.WriteLine("─".PadRight(60, '─'));

        var failureSim = replayer.SimulateFailure("database-connection");
        Console.WriteLine($"   Affected signals: {string.Join(", ", failureSim.AffectedSignals)}");
        Console.WriteLine();

        Console.WriteLine("   Simulated results:");
        foreach (var signal in failureSim.SimulatedSignals)
        {
            var statusIcon = signal.Status switch
            {
                "Succeeded" => "✅",
                "Failed" => "❌",
                "TimedOut" => "⏰",
                "Skipped" => "⏭️",
                _ => "❓"
            };
            var changed = failureSim.AffectedSignals.Contains(signal.SignalName) ? " (changed)" : "";
            Console.WriteLine($"      {statusIcon} {signal.SignalName}: {signal.Status}{changed}");
        }
        Console.WriteLine();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Example 5: Analysis methods.
    /// </summary>
    private static async Task RunAnalysisExample(IgnitionRecording recording)
    {
        Console.WriteLine("┌─────────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Example 5: ANALYSIS Methods                                                 │");
        Console.WriteLine("│ Shows how to identify slow signals, critical path, and concurrency          │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();

        var replayer = new IgnitionReplayer(recording);

        // Identify slow signals
        Console.WriteLine("🐢 SLOW SIGNALS (>100ms):");
        var slowSignals = replayer.IdentifySlowSignals(minDurationMs: 100);
        if (slowSignals.Count == 0)
        {
            Console.WriteLine("   No signals slower than 100ms");
        }
        else
        {
            foreach (var signal in slowSignals)
            {
                Console.WriteLine($"   • {signal.SignalName}: {signal.DurationMs:F1}ms");
            }
        }
        Console.WriteLine();

        // Identify critical path
        Console.WriteLine("🎯 CRITICAL PATH (signals that determine total duration):");
        var criticalPath = replayer.IdentifyCriticalPath();
        if (criticalPath.Count == 0)
        {
            Console.WriteLine("   No signals on critical path identified");
        }
        else
        {
            foreach (var signal in criticalPath)
            {
                Console.WriteLine($"   • {signal.SignalName}: {signal.DurationMs:F1}ms (ends at {signal.EndMs:F1}ms)");
            }
        }
        Console.WriteLine();

        // Execution order
        Console.WriteLine("📋 EXECUTION ORDER:");
        var executionOrder = replayer.GetExecutionOrder();
        Console.WriteLine($"   {string.Join(" → ", executionOrder)}");
        Console.WriteLine();

        // Concurrent groups
        Console.WriteLine("🔄 CONCURRENT GROUPS:");
        var concurrentGroups = replayer.GetConcurrentGroups();
        for (int i = 0; i < concurrentGroups.Count; i++)
        {
            Console.WriteLine($"   Group {i + 1}: {string.Join(", ", concurrentGroups[i])}");
        }
        Console.WriteLine();

        // Timeline conversion
        Console.WriteLine("📊 TIMELINE VISUALIZATION:");
        var timeline = recording.ToTimeline();
        timeline.WriteToConsole();

        Console.WriteLine();
        Console.WriteLine("💡 TIP: Use these analysis methods to:");
        Console.WriteLine("   • Identify startup bottlenecks");
        Console.WriteLine("   • Understand concurrency patterns");
        Console.WriteLine("   • Plan optimization strategies");
        Console.WriteLine("   • Debug slow startup issues in CI/CD");
        Console.WriteLine();

        await Task.CompletedTask;
    }
}
