# Recording & Replay Sample

This sample demonstrates the **Recording and Replay** features for diagnosing startup issues, CI regression detection, and what-if simulations.

## What This Sample Shows

1. **Recording an Ignition Run** - Capture timing, status, and configuration for later analysis
2. **Validating Recordings** - Check recordings for consistency and invariant violations
3. **Comparing Recordings** - Detect performance regressions between runs
4. **What-If Simulations** - Simulate timeout and failure scenarios
5. **Analysis Methods** - Identify slow signals, critical path, and concurrency patterns

## Running the Sample

```bash
cd samples/Replay
dotnet run
```

## Features Demonstrated

### Recording Export

The `result.ExportRecording()` method captures a complete snapshot of an ignition run:

```csharp
var recording = result.ExportRecording(
    options: options,
    finalState: coordinator.State,
    metadata: new Dictionary<string, string>
    {
        ["environment"] = "production",
        ["version"] = "1.0.0"
    });

// Export to JSON
var json = recording.ToJson(indented: true);
File.WriteAllText("ignition-recording.json", json);
```

### Recording Validation

The `IgnitionReplayer.Validate()` method checks recordings for:

- Timing validation (negative durations, end before start)
- Dependency order violations
- Stage execution correctness
- Configuration consistency
- Summary accuracy

```csharp
var replayer = new IgnitionReplayer(recording);
var validation = replayer.Validate();

if (!validation.IsValid)
{
    foreach (var issue in validation.Issues)
    {
        Console.WriteLine($"[{issue.Severity}] {issue.Code}: {issue.Message}");
    }
}
```

### Recording Comparison

Compare two recordings to detect regressions:

```csharp
var baselineReplayer = new IgnitionReplayer(baseline);
var comparison = baselineReplayer.CompareTo(current);

// Check for slowdowns
var regressions = comparison.SignalComparisons
    .Where(c => c.DurationChangePercent > 20);

foreach (var reg in regressions)
{
    Console.WriteLine($"{reg.SignalName}: +{reg.DurationDifferenceMs}ms");
}
```

### What-If Simulations

Simulate scenarios to understand impact:

```csharp
// What if a signal timed out earlier?
var timeoutSim = replayer.SimulateEarlierTimeout("slow-service", newTimeoutMs: 500);

// What if a signal failed?
var failureSim = replayer.SimulateFailure("database-connection");

// Check which signals would be affected
Console.WriteLine($"Affected: {string.Join(", ", failureSim.AffectedSignals)}");
```

### Analysis Methods

```csharp
// Find slow signals
var slowSignals = replayer.IdentifySlowSignals(minDurationMs: 100);

// Find signals on the critical path
var criticalPath = replayer.IdentifyCriticalPath();

// Get execution order
var order = replayer.GetExecutionOrder();

// Find concurrent groups
var groups = replayer.GetConcurrentGroups();
```

## Use Cases

- **Prod vs Dev Comparison**: Record startup in production and development, compare to identify environment-specific slowdowns
- **CI Regression Detection**: Save baseline recordings, compare against new builds to catch startup performance regressions
- **Failure Analysis**: Simulate failures to understand dependency chains and cascading effects
- **Capacity Planning**: Analyze critical path to identify optimization opportunities
- **Incident Post-Mortems**: Record and replay startup issues for offline analysis

## Sample Output

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                IGNITION RECORDING & REPLAY SAMPLE                            ║
╚══════════════════════════════════════════════════════════════════════════════╝

┌─────────────────────────────────────────────────────────────────────────────┐
│ Example 1: RECORDING an Ignition Run                                        │
└─────────────────────────────────────────────────────────────────────────────┘

📼 RECORDING CAPTURED:
   Recording ID: a1b2c3d4e5f6
   Recorded At:  2024-01-15T10:30:00Z
   Total Duration: 1250.5ms
   Timed Out: False
   Final State: Completed

📊 SIGNAL SUMMARY:
   Total Signals: 4
   ✅ Succeeded:  4
   ❌ Failed:     0
   ⏰ Timed Out:  0
   🐢 Slowest:    cache-warmup (1200ms)
   🚀 Fastest:    configuration-load (400ms)
   📊 Average:    650ms
   🔄 Max Concurrency: 4

📝 RECORDED SIGNALS:
   ✅ database-connection: Succeeded (800ms)
      Start: 0.0ms → End: 800.0ms
   ✅ cache-warmup: Succeeded (1200ms)
      Start: 0.0ms → End: 1200.0ms
   ...
```

## Recording JSON Schema

The recording captures:

```json
{
  "schemaVersion": "1.0",
  "recordingId": "a1b2c3d4e5f6",
  "recordedAt": "2024-01-15T10:30:00Z",
  "totalDurationMs": 1250.5,
  "timedOut": false,
  "finalState": "Completed",
  "configuration": {
    "executionMode": "Parallel",
    "policy": "BestEffort",
    "globalTimeoutMs": 10000
  },
  "signals": [...],
  "summary": {...},
  "metadata": {
    "environment": "production",
    "version": "1.0.0"
  }
}
```

## Related Documentation

- [Recording and Replay Documentation](../../docs/features.md)
- [Timeline Export Sample](../TimelineExport/README.md)
- [Features Overview](../../docs/features.md)
