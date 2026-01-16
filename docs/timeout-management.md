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

## Category-Based Timeout Strategies

Group signals by category and apply category-specific timeout strategies using `IIgnitionTimeoutStrategy`.

### Timeout Strategy Interface

```csharp
public interface IIgnitionTimeoutStrategy
{
    /// <summary>
    /// Determines the effective timeout and cancellation behavior for a signal.
    /// </summary>
    /// <param name="signal">The signal to evaluate.</param>
    /// <param name="options">The current ignition options providing global configuration context.</param>
    /// <returns>A tuple of (timeout duration, whether to cancel on timeout).</returns>
    (TimeSpan? timeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options);
}
```

### Example: Category-Based Strategy

```csharp
using Veggerby.Ignition;

public sealed class CategoryBasedTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly Dictionary<string, (TimeSpan timeout, bool cancel)> _categoryTimeouts;
    private readonly (TimeSpan timeout, bool cancel) _defaultTimeout;

    public CategoryBasedTimeoutStrategy()
    {
        _categoryTimeouts = new Dictionary<string, (TimeSpan, bool)>
        {
            ["critical"] = (TimeSpan.FromSeconds(5), true),       // Short, hard cancel
            ["infrastructure"] = (TimeSpan.FromSeconds(15), true), // Medium, hard cancel
            ["warmup"] = (TimeSpan.FromSeconds(30), false),        // Long, soft timeout
            ["optional"] = (TimeSpan.FromMinutes(2), false)        // Very long, soft
        };

        _defaultTimeout = (TimeSpan.FromSeconds(10), true);
    }

    public (TimeSpan? timeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        // Categorize by signal name prefix
        var category = GetCategory(signal.Name);

        if (_categoryTimeouts.TryGetValue(category, out var config))
        {
            return (config.timeout, config.cancel);
        }

        // Fall back to signal's own timeout if specified
        if (signal.Timeout.HasValue)
        {
            return (signal.Timeout, options.CancelIndividualOnTimeout);
        }

        // Use default
        return (_defaultTimeout.timeout, _defaultTimeout.cancel);
    }

    private string GetCategory(string signalName)
    {
        if (signalName.StartsWith("critical-") || signalName.EndsWith("-critical"))
        {
            return "critical";
        }

        if (signalName.Contains("database") || signalName.Contains("auth"))
        {
            return "critical";
        }

        if (signalName.Contains("cache") || signalName.Contains("redis"))
        {
            return "infrastructure";
        }

        if (signalName.Contains("warmup") || signalName.Contains("preload"))
        {
            return "warmup";
        }

        if (signalName.Contains("optional") || signalName.Contains("analytics"))
        {
            return "optional";
        }

        return "infrastructure"; // Default category
    }
}

// Usage
builder.Services.AddIgnition(opts =>
{
    opts.TimeoutStrategy = new CategoryBasedTimeoutStrategy();
    opts.GlobalTimeout = TimeSpan.FromSeconds(60);
});
```

### Example: Attribute-Based Strategy

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class SignalCategoryAttribute : Attribute
{
    public string Category { get; }

    public SignalCategoryAttribute(string category)
    {
        Category = category ?? throw new ArgumentNullException(nameof(category));
    }
}

[SignalCategory("critical")]
public sealed class DatabaseSignal : IIgnitionSignal
{
    public string Name => "database";
    public TimeSpan? Timeout => null; // Defer to strategy

    public async Task WaitAsync(CancellationToken ct)
    {
        // Implementation
    }
}

public sealed class AttributeBasedTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly Dictionary<string, (TimeSpan timeout, bool cancel)> _categoryTimeouts;

    public AttributeBasedTimeoutStrategy()
    {
        _categoryTimeouts = new Dictionary<string, (TimeSpan, bool)>
        {
            ["critical"] = (TimeSpan.FromSeconds(5), true),
            ["infrastructure"] = (TimeSpan.FromSeconds(15), true),
            ["warmup"] = (TimeSpan.FromSeconds(30), false),
            ["optional"] = (TimeSpan.FromMinutes(2), false)
        };
    }

    public (TimeSpan? timeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        var attribute = signal.GetType().GetCustomAttribute<SignalCategoryAttribute>();
        if (attribute != null && _categoryTimeouts.TryGetValue(attribute.Category, out var config))
        {
            return (config.timeout, config.cancel);
        }

        return (signal.Timeout, options.CancelIndividualOnTimeout);
    }
}
```

**Performance Warning**: This example uses reflection (`GetCustomAttribute`) in the `GetTimeout` method which may be invoked for every signal. For production use, consider caching attribute lookups in a dictionary during strategy initialization, or use a name-based categorization strategy instead to avoid reflection overhead in the hot path.

```csharp
// Production-optimized version with cached attributes
public sealed class CachedAttributeBasedTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly Dictionary<Type, string> _signalCategories = new();
    private readonly Dictionary<string, (TimeSpan timeout, bool cancel)> _categoryTimeouts;

    public CachedAttributeBasedTimeoutStrategy()
    {
        _categoryTimeouts = new Dictionary<string, (TimeSpan, bool)>
        {
            ["critical"] = (TimeSpan.FromSeconds(5), true),
            ["infrastructure"] = (TimeSpan.FromSeconds(15), true),
            ["warmup"] = (TimeSpan.FromSeconds(30), false),
            ["optional"] = (TimeSpan.FromMinutes(2), false)
        };
    }

    public (TimeSpan? timeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        var signalType = signal.GetType();
        
        // Cache lookup on first encounter of this signal type
        if (!_signalCategories.TryGetValue(signalType, out var category))
        {
            var attribute = signalType.GetCustomAttribute<SignalCategoryAttribute>();
            category = attribute?.Category ?? "default";
            _signalCategories[signalType] = category;
        }

        if (_categoryTimeouts.TryGetValue(category, out var config))
        {
            return (config.timeout, config.cancel);
        }

        return (signal.Timeout, options.CancelIndividualOnTimeout);
    }
}
```

## Environment-Specific Timeout Strategies

Adapt timeouts based on deployment environment (Development, Staging, Production).

### Development vs Production

```csharp
public sealed class EnvironmentAwareTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly IWebHostEnvironment _environment;
    private readonly double _developmentMultiplier;
    private readonly double _productionMultiplier;

    public EnvironmentAwareTimeoutStrategy(
        IWebHostEnvironment environment,
        double developmentMultiplier = 2.0,
        double productionMultiplier = 1.0)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _developmentMultiplier = developmentMultiplier;
        _productionMultiplier = productionMultiplier;
    }

    public (TimeSpan? timeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        var baseTimeout = signal.Timeout ?? TimeSpan.FromSeconds(10);

        var multiplier = _environment.IsDevelopment()
            ? _developmentMultiplier
            : _productionMultiplier;

        var adjustedTimeout = TimeSpan.FromMilliseconds(baseTimeout.TotalMilliseconds * multiplier);

        // Production: hard cancel; Development: soft timeout for debugging
        var cancelImmediately = _environment.IsProduction();

        return (adjustedTimeout, cancelImmediately);
    }
}

// Usage
builder.Services.AddIgnition(opts =>
{
    opts.TimeoutStrategy = new EnvironmentAwareTimeoutStrategy(
        builder.Environment,
        developmentMultiplier: 3.0,  // 3x longer in dev
        productionMultiplier: 1.0);  // Normal in prod

    opts.GlobalTimeout = builder.Environment.IsDevelopment()
        ? TimeSpan.FromMinutes(5)
        : TimeSpan.FromSeconds(30);
});
```

### Configuration-Based Strategy

Load timeout configuration from `appsettings.json`:

**appsettings.Development.json:**

```json
{
  "Ignition": {
    "Timeouts": {
      "database": "00:00:30",
      "cache": "00:01:00",
      "messaging": "00:00:45",
      "default": "00:00:20"
    },
    "CancelOnTimeout": false
  }
}
```

**appsettings.Production.json:**

```json
{
  "Ignition": {
    "Timeouts": {
      "database": "00:00:10",
      "cache": "00:00:15",
      "messaging": "00:00:12",
      "default": "00:00:10"
    },
    "CancelOnTimeout": true
  }
}
```

**Strategy Implementation:**

```csharp
public sealed class ConfigurationBasedTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly IConfiguration _configuration;

    public ConfigurationBasedTimeoutStrategy(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public (TimeSpan? timeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
    {
        var timeoutSection = _configuration.GetSection("Ignition:Timeouts");
        var cancelOnTimeout = _configuration.GetValue<bool>("Ignition:CancelOnTimeout", true);

        // Try signal-specific timeout
        var timeoutStr = timeoutSection[signal.Name];
        if (!string.IsNullOrEmpty(timeoutStr) && TimeSpan.TryParse(timeoutStr, out var timeout))
        {
            return (timeout, cancelOnTimeout);
        }

        // Fall back to default
        var defaultTimeoutStr = timeoutSection["default"];
        if (!string.IsNullOrEmpty(defaultTimeoutStr) && TimeSpan.TryParse(defaultTimeoutStr, out var defaultTimeout))
        {
            return (defaultTimeout, cancelOnTimeout);
        }

        // Use signal's own timeout
        return (signal.Timeout, cancelOnTimeout);
    }
}

// Usage
builder.Services.AddIgnition(opts =>
{
    opts.TimeoutStrategy = new ConfigurationBasedTimeoutStrategy(builder.Configuration);
});
```

## Per-Signal vs Global Timeout Interaction Diagrams

### Diagram 1: Soft Global Timeout (Default)

```text
Timeline (seconds):  0----5----10----15----20----25----30----35----40
Global Timeout (30s, soft): [=====================================|
Signal A (5s):             [====]
Signal B (10s):            [=========]
Signal C (no timeout):     [========================================]

Outcome:
- Signal A: Succeeded (completed at 5s)
- Signal B: Succeeded (completed at 10s)
- Signal C: Succeeded (completed at 40s, exceeds global but soft)
- Result.TimedOut: false (all signals succeeded despite global timeout elapse)
```

### Diagram 2: Hard Global Timeout

```text
Timeline (seconds):  0----5----10----15----20----25----30----35----40
Global Timeout (30s, hard): [============================X
Signal A (5s):             [====]
Signal B (10s):            [=========]
Signal C (no timeout):     [===========================X (cancelled)

Outcome:
- Signal A: Succeeded (completed at 5s)
- Signal B: Succeeded (completed at 10s)
- Signal C: TimedOut (cancelled at 30s by global timeout)
- Result.TimedOut: true (global timeout triggered)
```

### Diagram 3: Per-Signal Timeout Only

```text
Timeline (seconds):  0----5----10----15----20----25----30
Global Timeout (60s, soft): [==============================...
Signal A (timeout=5s):    [====]
Signal B (timeout=10s):   [==X (timed out, not cancelled)
Signal C (no timeout):    [==============================]

Outcome:
- Signal A: Succeeded (completed at 5s)
- Signal B: TimedOut (exceeded 10s timeout but not cancelled)
- Signal C: Succeeded (completed at 30s)
- Result.TimedOut: true (Signal B timed out)
```

### Diagram 4: Combined Global + Per-Signal Timeout

```text
Timeline (seconds):  0----5----10----15----20----25----30----35
Global Timeout (30s, hard): [============================X
Per-signal timeout enabled (CancelIndividualOnTimeout=true)
Signal A (timeout=5s):    [====]
Signal B (timeout=10s):   [==X (cancelled at 10s)
Signal C (timeout=40s):   [===========================X (cancelled at 30s by global)

Outcome:
- Signal A: Succeeded (completed at 5s)
- Signal B: TimedOut (exceeded 10s, cancelled immediately)
- Signal C: TimedOut (global timeout at 30s < signal timeout 40s)
- Result.TimedOut: true (both B and C timed out)
```

### Diagram 5: Best-Effort Policy with Mixed Timeouts

```text
Timeline (seconds):  0----5----10----15----20----25----30
Global Timeout (30s, soft): [==============================|
Policy: BestEffort
Signal A (timeout=5s):    [====]
Signal B (timeout=8s):    [=====X (times out, continues)
Signal C (no timeout):    [==============================]
Signal D (timeout=15s):   [==============]

Outcome:
- Signal A: Succeeded (5s)
- Signal B: TimedOut (exceeded 8s but continued, completed at 25s)
- Signal C: Succeeded (30s)
- Signal D: Succeeded (15s)
- Result.TimedOut: true (Signal B timed out)
- Application continues (BestEffort policy)
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
