# Timeout Management Deep Dive

This guide provides comprehensive coverage of Veggerby.Ignition's two-layer timeout system, including global and per-signal timeouts, soft vs hard semantics, and best practices for production use.

## Overview

Veggerby.Ignition implements a sophisticated timeout system that balances flexibility with safety:

- **Global timeout**: Application-wide deadline for startup completion
- **Per-signal timeout**: Individual signal deadlines
- **Soft vs hard semantics**: Choose between graceful degradation and strict enforcement
- **Cooperative cancellation**: CancellationToken-based timeout handling

## Two-Layer Timeout System

### Layer 1: Global Timeout

The global timeout sets an application-wide deadline for all signals to complete.

#### Soft Global Timeout (Default)

By default, the global timeout is **soft**: execution continues beyond the deadline, and the result is only marked as timed out if individual signals time out.

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.CancelOnGlobalTimeout = false; // Default
});
```

**Behavior**:

- Timer starts when `WaitAllAsync()` is called
- If all signals complete before 30 seconds: ✓ No timeout
- If some signals exceed their own timeout: ⚠ Timed out (per-signal)
- If all signals complete successfully but after 30 seconds: ✓ No timeout (soft)

**Use case**: Development, graceful degradation, non-critical startup tasks

#### Hard Global Timeout

When `CancelOnGlobalTimeout = true`, the global timeout becomes **hard**: outstanding signals receive cancellation, and the result is marked as timed out.

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.CancelOnGlobalTimeout = true; // Hard timeout
});
```

**Behavior**:

- Timer starts when `WaitAllAsync()` is called
- If 30 seconds elapse with signals still running:
  - Cancellation tokens are signaled
  - Result is marked `TimedOut = true`
  - Unfinished signals report `Status = TimedOut`

**Use case**: Production, strict SLA enforcement, critical startup deadlines

### Layer 2: Per-Signal Timeout

Each signal can specify its own timeout via the `Timeout` property:

```csharp
public class DatabaseSignal : IIgnitionSignal
{
    public string Name => "database";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10); // Signal-specific timeout

    public async Task WaitAsync(CancellationToken ct)
    {
        await _connection.OpenAsync(ct);
    }
}
```

#### Per-Signal Timeout Behavior

When a signal's timeout elapses:

- Signal is marked as `Status = TimedOut`
- Overall result is marked `TimedOut = true`
- Optionally, the signal receives cancellation (if `CancelIndividualOnTimeout = true`)

#### Cancellation Control

```csharp
builder.Services.AddIgnition(options =>
{
    options.CancelIndividualOnTimeout = true; // Cancel signals on their timeout
});
```

**When enabled**:

- Signal's `CancellationToken` is signaled when its timeout elapses
- Signal should honor cancellation and exit promptly

**When disabled** (default):

- Signal continues running past its timeout
- Useful for observing how long tasks actually take

### Timeout Interaction Rules

Both timeout layers can be active simultaneously. Understanding their interaction is key:

| Global Timeout | Signal Timeout | CancelOnGlobalTimeout | CancelIndividualOnTimeout | Outcome |
|----------------|----------------|----------------------|--------------------------|---------|
| 30s | 10s | false | false | Signal times out at 10s (no cancel), global soft deadline at 30s |
| 30s | 10s | false | true | Signal canceled at 10s, global soft deadline at 30s |
| 30s | 10s | true | false | Signal times out at 10s (no cancel), global hard cancel at 30s |
| 30s | 10s | true | true | Signal canceled at 10s, global hard cancel at 30s |
| 30s | null | false | - | No per-signal timeout, global soft deadline at 30s |
| 30s | null | true | - | No per-signal timeout, global hard cancel at 30s |

## Timeout Classification Matrix

Understanding when `result.TimedOut` is `true`:

| Scenario | Result.TimedOut | Signal Statuses |
|----------|-----------------|-----------------|
| All signals complete before global timeout | `false` | All `Succeeded` |
| Soft global timeout elapses, but all signals eventually succeed | `false` | All `Succeeded` |
| Soft global timeout elapses, one signal times out individually | `true` | Mix of `TimedOut` and `Succeeded` |
| Hard global timeout elapses (cancel triggered) | `true` | Mix of `TimedOut` (unfinished) and completed |
| Per-signal timeout occurs (no global timeout) | `true` | Mix of `TimedOut` and `Succeeded` |

### Example: Soft Global Timeout

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(10); // Soft
    options.CancelOnGlobalTimeout = false;
});

// Signal takes 15 seconds, no per-signal timeout
builder.Services.AddIgnitionFromTask(
    "slow-task",
    async ct => await Task.Delay(15000, ct),
    timeout: null);

var result = await coordinator.GetResultAsync();

// result.TimedOut = false (soft global timeout, signal succeeded)
// result.TotalDuration ≈ 15 seconds
// Signal status: Succeeded
```

### Example: Hard Global Timeout

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(10); // Hard
    options.CancelOnGlobalTimeout = true;
});

// Signal takes 15 seconds
builder.Services.AddIgnitionFromTask(
    "slow-task",
    async ct => await Task.Delay(15000, ct),
    timeout: null);

var result = await coordinator.GetResultAsync();

// result.TimedOut = true (hard global timeout triggered)
// result.TotalDuration ≈ 10 seconds (capped by global timeout)
// Signal status: TimedOut (was canceled at 10s)
```

### Example: Per-Signal Timeout Only

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30); // Soft
    options.CancelIndividualOnTimeout = true;
});

// Signal has 5s timeout
builder.Services.AddIgnitionFromTask(
    "database",
    async ct => await Task.Delay(10000, ct), // Takes 10s
    timeout: TimeSpan.FromSeconds(5));

var result = await coordinator.GetResultAsync();

// result.TimedOut = true (per-signal timeout occurred)
// Signal status: TimedOut (canceled at 5s)
```

## When to Use CancelOnGlobalTimeout = true

### Use Hard Global Timeout When:

✅ **Strict SLA requirements**: Startup must complete within a fixed deadline

```csharp
// Production: must be ready in 30s or fail
options.GlobalTimeout = TimeSpan.FromSeconds(30);
options.CancelOnGlobalTimeout = true;
options.Policy = IgnitionPolicy.FailFast;
```

✅ **Health check probes**: Kubernetes/orchestrator liveness probes have timeouts

```csharp
// Container must respond to health checks within startup period
options.GlobalTimeout = TimeSpan.FromSeconds(60);
options.CancelOnGlobalTimeout = true;
```

✅ **Resource constraints**: Long-running startup consumes resources needed for serving traffic

```csharp
// Free up resources if startup takes too long
options.GlobalTimeout = TimeSpan.FromSeconds(45);
options.CancelOnGlobalTimeout = true;
```

### Use Soft Global Timeout When:

✅ **Development/testing**: Allow slow signals to complete for diagnostics

```csharp
// Dev: see how long signals actually take
options.GlobalTimeout = TimeSpan.FromSeconds(60);
options.CancelOnGlobalTimeout = false;
options.SlowHandleLogCount = 10; // Log all slow signals
```

✅ **Graceful degradation**: Prefer partial startup over failure

```csharp
// Optional cache warmup can exceed deadline
options.GlobalTimeout = TimeSpan.FromSeconds(30);
options.CancelOnGlobalTimeout = false;
options.Policy = IgnitionPolicy.BestEffort;
```

✅ **Monitoring**: Collect metrics on actual startup duration

```csharp
// Observe real-world performance
options.GlobalTimeout = TimeSpan.FromSeconds(60);
options.CancelOnGlobalTimeout = false;
options.EnableTracing = true; // Capture in telemetry
```

## CancelIndividualOnTimeout Flag Usage

### Use CancelIndividualOnTimeout = true When:

✅ **Signal respects cancellation**: Task properly honors `CancellationToken`

```csharp
public async Task WaitAsync(CancellationToken ct)
{
    await _httpClient.GetAsync(_healthUrl, ct); // Respects cancellation
}
```

✅ **Resource cleanup needed**: Signal should release resources on timeout

```csharp
public async Task WaitAsync(CancellationToken ct)
{
    using var connection = await OpenConnectionAsync(ct);
    // Connection disposed when canceled
}
```

✅ **Avoid hanging**: Signal might block indefinitely without cancellation

```csharp
options.CancelIndividualOnTimeout = true; // Prevent hangs
```

### Use CancelIndividualOnTimeout = false When:

✅ **Observing actual duration**: Want to see how long signals really take

```csharp
// Dev: let signals run to completion to measure actual timing
options.CancelIndividualOnTimeout = false;
options.SlowHandleLogCount = 10;
```

✅ **Signal ignores cancellation**: Task doesn't honor `CancellationToken`

```csharp
// Legacy code that doesn't respect cancellation
public async Task WaitAsync(CancellationToken ct)
{
    await Task.Delay(5000); // Doesn't pass ct
}
```

✅ **Non-critical signals**: Let optional tasks complete in background

```csharp
// Cache warmup can continue past timeout
options.CancelIndividualOnTimeout = false;
options.Policy = IgnitionPolicy.BestEffort;
```

## Best Practices for Timeout Values

### Setting Global Timeout

**Development**:

```csharp
options.GlobalTimeout = TimeSpan.FromMinutes(2); // Generous for debugging
options.CancelOnGlobalTimeout = false;
```

**Staging/Production**:

```csharp
// Based on p95 observed startup time + buffer
var baseline = TimeSpan.FromSeconds(25); // p95 observed
var buffer = TimeSpan.FromSeconds(10);   // Safety margin
options.GlobalTimeout = baseline + buffer; // 35 seconds
options.CancelOnGlobalTimeout = true;
```

**Formula**:

```text
Global Timeout = (p95 Startup Time) × 1.5
```

### Setting Per-Signal Timeout

Base on signal characteristics:

```csharp
// Fast: Configuration loading
public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

// Medium: Database connection
public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

// Slow: Cache warming, index building
public TimeSpan? Timeout => TimeSpan.FromSeconds(30);

// Very slow: Large data migration
public TimeSpan? Timeout => TimeSpan.FromMinutes(2);

// No timeout: Background warmup (best-effort)
public TimeSpan? Timeout => null;
```

### Progressive Timeout Strategy

Use tighter timeouts for critical signals, looser for optional ones:

```csharp
// Critical: Database (tight timeout)
builder.Services.AddIgnitionFromTask(
    "database",
    ct => ConnectAsync(ct),
    timeout: TimeSpan.FromSeconds(10));

// Important: Cache (medium timeout)
builder.Services.AddIgnitionFromTask(
    "cache",
    ct => WarmCacheAsync(ct),
    timeout: TimeSpan.FromSeconds(20));

// Optional: Recommendations (loose/no timeout)
builder.Services.AddIgnitionFromTask(
    "recommendations",
    ct => PrecomputeRecommendationsAsync(ct),
    timeout: null); // Best effort
```

## Real-World Examples

### Example 1: Graceful Degradation with Soft Timeouts

**Scenario**: E-commerce site with optional cache warmup. Site should start even if cache warmup is slow.

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30); // Soft
    options.CancelOnGlobalTimeout = false;
    options.Policy = IgnitionPolicy.BestEffort;
});

// Critical: Database (must succeed)
builder.Services.AddIgnitionFromTask(
    "database",
    ct => db.OpenAsync(ct),
    timeout: TimeSpan.FromSeconds(10));

// Optional: Cache warmup (can be slow or fail)
builder.Services.AddIgnitionFromTask(
    "cache-warmup",
    ct => cache.WarmAsync(ct),
    timeout: null); // No per-signal timeout

var result = await coordinator.GetResultAsync();

if (result.TimedOut)
{
    logger.LogWarning("Startup degraded: some signals timed out");
}

// Start serving traffic regardless
await app.RunAsync();
```

**Outcome**:

- Database connects in 8s: ✓ Success
- Cache warmup takes 45s: ⚠ Exceeds global timeout but completes
- Application starts after 45s, no timeout reported (soft global timeout)

### Example 2: Hard Deadline Enforcement for Critical Startup

**Scenario**: Financial trading system must be ready in 60 seconds to meet regulatory requirements.

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(60); // Hard deadline
    options.CancelOnGlobalTimeout = true;
    options.CancelIndividualOnTimeout = true;
    options.Policy = IgnitionPolicy.FailFast;
});

// Market data connection (20s timeout)
builder.Services.AddIgnitionFromTask(
    "market-data",
    ct => marketData.ConnectAsync(ct),
    timeout: TimeSpan.FromSeconds(20));

// Order routing (15s timeout)
builder.Services.AddIgnitionFromTask(
    "order-routing",
    ct => orderRouter.InitializeAsync(ct),
    timeout: TimeSpan.FromSeconds(15));

// Risk engine (25s timeout)
builder.Services.AddIgnitionFromTask(
    "risk-engine",
    ct => riskEngine.LoadRulesAsync(ct),
    timeout: TimeSpan.FromSeconds(25));

try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    logger.LogCritical(ex, "Critical startup failure - cannot trade");
    Environment.Exit(1); // Fail fast
}

var result = await coordinator.GetResultAsync();
if (result.TimedOut)
{
    logger.LogCritical("Startup deadline exceeded - shutting down");
    Environment.Exit(1);
}

logger.LogInformation("Trading system ready in {Duration:F2}s", result.TotalDuration.TotalSeconds);
```

**Outcome**:

- All signals must complete within their individual timeouts
- Any timeout causes FailFast to throw
- If global timeout (60s) elapses, outstanding signals are canceled
- System fails completely rather than starting in degraded state

### Example 3: Mixed Timeout Strategies

**Scenario**: Web application with critical and optional components.

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(45);
    options.CancelOnGlobalTimeout = true; // Hard global deadline
    options.CancelIndividualOnTimeout = true;
    options.Policy = IgnitionPolicy.BestEffort; // Tolerate individual failures
});

// Critical path
builder.Services.AddIgnitionFromTask(
    "database",
    ct => db.ConnectAsync(ct),
    timeout: TimeSpan.FromSeconds(10));

builder.Services.AddIgnitionFromTask(
    "authentication",
    ct => auth.InitializeAsync(ct),
    timeout: TimeSpan.FromSeconds(8));

// Optional enhancements
builder.Services.AddIgnitionFromTask(
    "search-index",
    ct => search.BuildIndexAsync(ct),
    timeout: TimeSpan.FromSeconds(30)); // Long but acceptable

builder.Services.AddIgnitionFromTask(
    "analytics",
    ct => analytics.WarmupAsync(ct),
    timeout: null); // Best effort, no timeout

var result = await coordinator.GetResultAsync();

// Evaluate critical signals
var criticalSignals = new[] { "database", "authentication" };
var criticalFailures = result.Results
    .Where(r => criticalSignals.Contains(r.Name))
    .Where(r => r.Status != IgnitionSignalStatus.Succeeded);

if (criticalFailures.Any())
{
    logger.LogCritical("Critical startup failure");
    Environment.Exit(1);
}

// Log optional failures
var optionalFailures = result.Results
    .Where(r => !criticalSignals.Contains(r.Name))
    .Where(r => r.Status != IgnitionSignalStatus.Succeeded);

foreach (var failure in optionalFailures)
{
    logger.LogWarning("Optional signal {Name} failed: {Status}", failure.Name, failure.Status);
}

// Start in potentially degraded mode
await app.RunAsync();
```

**Outcome**:

- Critical signals (database, auth) must succeed
- Optional signals can fail or timeout
- Global hard deadline prevents indefinite startup
- Application starts even if optional features unavailable

## Handling Timeout Scenarios Gracefully

### Inspecting Timeout Results

```csharp
var result = await coordinator.GetResultAsync();

if (result.TimedOut)
{
    logger.LogWarning("Startup completed with timeouts");

    var timedOutSignals = result.Results
        .Where(r => r.Status == IgnitionSignalStatus.TimedOut)
        .ToList();

    foreach (var signal in timedOutSignals)
    {
        logger.LogWarning(
            "Signal {Name} timed out after {Duration:F2}s (timeout: {Timeout:F2}s)",
            signal.Name,
            signal.Duration.TotalSeconds,
            signal.Timeout?.TotalSeconds ?? 0);
    }

    // Decide whether to continue or fail
    var criticalTimedOut = timedOutSignals.Any(s => IsCritical(s.Name));
    if (criticalTimedOut)
    {
        logger.LogCritical("Critical signal timed out - aborting");
        Environment.Exit(1);
    }
}
```

### Retry on Timeout

For transient timeout scenarios, implement retry logic:

```csharp
int maxRetries = 3;
int attempt = 0;
IgnitionResult? result = null;

while (attempt < maxRetries)
{
    attempt++;
    logger.LogInformation("Startup attempt {Attempt}/{Max}", attempt, maxRetries);

    try
    {
        // Create new coordinator for each attempt
        var coordinator = serviceProvider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();
        result = await coordinator.GetResultAsync();

        if (!result.TimedOut)
        {
            logger.LogInformation("Startup succeeded on attempt {Attempt}", attempt);
            break;
        }

        logger.LogWarning("Startup timed out on attempt {Attempt}, retrying...", attempt);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startup failed on attempt {Attempt}", attempt);
    }

    if (attempt < maxRetries)
    {
        await Task.Delay(TimeSpan.FromSeconds(5)); // Backoff
    }
}

if (result?.TimedOut ?? true)
{
    logger.LogCritical("Startup failed after {Attempts} attempts", maxRetries);
    Environment.Exit(1);
}
```

### Timeout Metrics and Alerting

Export timeout metrics for monitoring:

```csharp
var result = await coordinator.GetResultAsync();

// Emit metrics
metrics.RecordStartupDuration(result.TotalDuration);
metrics.RecordTimedOut(result.TimedOut);

foreach (var r in result.Results)
{
    metrics.RecordSignalDuration(r.Name, r.Duration);

    if (r.Status == IgnitionSignalStatus.TimedOut)
    {
        metrics.IncrementSignalTimeout(r.Name);
        // Trigger alert
        alerting.NotifySignalTimeout(r.Name, r.Duration, r.Timeout);
    }
}
```

## Advanced: Dynamic Timeout Adjustment

Adjust timeouts based on environment or load:

```csharp
var config = builder.Configuration;
var environment = builder.Environment;

builder.Services.AddIgnition(options =>
{
    // Scale timeouts in production vs development
    var multiplier = environment.IsProduction() ? 1.0 : 2.0;

    options.GlobalTimeout = TimeSpan.FromSeconds(30 * multiplier);
    options.CancelOnGlobalTimeout = environment.IsProduction();

    // Load timeout values from configuration
    var dbTimeout = config.GetValue<int>("Ignition:DatabaseTimeout", 10);
    // Apply to signals via configuration binding
});
```

## Troubleshooting Timeout Issues

### Signal Always Times Out

**Check**:

1. Is the timeout too short for the operation?
2. Is the signal blocking on I/O without awaiting?
3. Is the signal deadlocked?

**Debug**:

```csharp
// Increase timeout temporarily
public TimeSpan? Timeout => TimeSpan.FromMinutes(5);

// Add logging to track progress
public async Task WaitAsync(CancellationToken ct)
{
    _logger.LogDebug("Signal starting");
    await Step1Async(ct);
    _logger.LogDebug("Step 1 complete");
    await Step2Async(ct);
    _logger.LogDebug("Step 2 complete");
}
```

### Global Timeout Triggers Unexpectedly

**Check**:

1. Sum of sequential signal times exceeds global timeout
2. Parallel signals not actually running in parallel
3. Concurrency limiting reducing parallelism

**Debug**:

```csharp
// Enable slow signal logging
options.SlowHandleLogCount = 10;

// Check execution mode
Console.WriteLine($"Execution mode: {options.ExecutionMode}");

// Inspect per-signal durations
var result = await coordinator.GetResultAsync();
foreach (var r in result.Results.OrderByDescending(x => x.Duration))
{
    Console.WriteLine($"{r.Name}: {r.Duration.TotalSeconds:F2}s");
}
```

## Related Topics

- [Getting Started](getting-started.md) - Basic timeout configuration
- [Policies](policies.md) - Timeout interaction with failure policies
- [Dependency-Aware Execution](dependency-aware-execution.md) - Timeouts in DAG mode
- [Observability](observability.md) - Monitoring timeout occurrences
- [Performance Guide](performance.md) - Optimizing signal duration
