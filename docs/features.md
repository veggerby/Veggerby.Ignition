# Veggerby.Ignition Features

This document provides a comprehensive overview of all features in Veggerby.Ignition, a lightweight startup readiness coordination library for .NET applications.

## Table of Contents

- [Core Concepts](#core-concepts)
- [Signal Management](#signal-management)
- [Execution Modes](#execution-modes)
- [Timeout Management](#timeout-management)
- [Policies](#policies)
- [Dependency-Aware Execution (DAG)](#dependency-aware-execution-dag)
- [Ignition Bundles](#ignition-bundles)
- [Diagnostics & Observability](#diagnostics--observability)
- [Health Checks](#health-checks)
- [Advanced Adapters](#advanced-adapters)
- [Performance Features](#performance-features)

## Core Concepts

### IIgnitionSignal

The fundamental abstraction representing an asynchronous readiness operation. Each signal has:

- **Name**: Human-friendly identifier for diagnostics
- **Timeout**: Optional per-signal timeout override
- **WaitAsync**: The actual initialization/readiness task

```csharp
public class CustomSignal : IIgnitionSignal
{
    public string Name => "my-service";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        // Perform initialization
    }
}
```

### IIgnitionCoordinator

Orchestrates all registered signals, applying timeouts, policies, and execution strategies. Provides:

- **WaitAllAsync()**: Execute all signals and await completion
- **GetResultAsync()**: Retrieve cached aggregated results
- **Idempotent execution**: Signals run exactly once, results cached

## Signal Management

### Registration Methods

**Basic Signal Registration**

```csharp
services.AddIgnitionSignal<MySignal>();
services.AddIgnitionSignal(new MySignal());
```

**Task-Based Registration**

```csharp
// From existing Task
services.AddIgnitionFromTask("warmup", warmupTask, timeout: TimeSpan.FromSeconds(5));

// From cancellable task factory
services.AddIgnitionFromTask(
    name: "index-build",
    readyTaskFactory: ct => indexBuilder.BuildAsync(ct),
    timeout: TimeSpan.FromSeconds(30));
```

**Service Adapter Registration**

```csharp
// Single service instance
services.AddIgnitionFor<MyWorker>(w => w.ReadyTask, name: "worker");

// Multiple service instances (composite signal)
services.AddIgnitionForAll<KafkaConsumer>(c => c.ReadyTask, groupName: "kafka[*]");

// Custom factory composition
services.AddIgnitionFromFactory(
    taskFactory: sp => Task.WhenAll(
        sp.GetRequiredService<DbConnection>().OpenAsync(),
        sp.GetRequiredService<CacheWarmer>().WarmAsync()),
    name: "datastore");
```

## Execution Modes

Veggerby.Ignition supports three execution strategies:

### Parallel (Default)

All signals execute concurrently. Fastest startup when signals are independent.

```csharp
options.ExecutionMode = IgnitionExecutionMode.Parallel;
options.MaxDegreeOfParallelism = 4; // Optional concurrency limit
```

**Features:**

- Maximum parallelism by default
- Optional concurrency limiting via `MaxDegreeOfParallelism`
- Best for independent initialization tasks

### Sequential

Signals execute one-by-one in registration order.

```csharp
options.ExecutionMode = IgnitionExecutionMode.Sequential;
```

**Use Cases:**

- Resource-constrained environments
- Explicit initialization ordering requirements
- Reducing startup resource spikes

### Dependency-Aware (DAG)

Signals execute based on explicit dependency relationships with automatic parallelism of independent branches.

```csharp
options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
```

**Features:**

- Topological sort execution
- Automatic parallel execution of independent branches
- Cycle detection with diagnostic paths
- Failure propagation (failed signals skip dependents)
- Supports both programmatic and attribute-based dependency declaration

## Timeout Management

### Two-Layer Timeout System

#### 1. Global Timeout

Application-wide deadline with soft vs. hard behavior:

**Soft Global Timeout (Default)**

```csharp
options.GlobalTimeout = TimeSpan.FromSeconds(10);
options.CancelOnGlobalTimeout = false; // Default
```

- Execution continues beyond deadline
- Result marked timed out only if individual signals time out
- Avoids penalizing slow but successful initialization

**Hard Global Timeout**

```csharp
options.GlobalTimeout = TimeSpan.FromSeconds(10);
options.CancelOnGlobalTimeout = true;
```

- Outstanding signals receive cancellation when deadline elapses
- Result marked timed out immediately
- Unfinished signals reported with `TimedOut` status

#### 2. Per-Signal Timeout

Individual signal deadlines:

```csharp
public class MySignal : IIgnitionSignal
{
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);
    // ...
}
```

**Cancellation Control**

```csharp
options.CancelIndividualOnTimeout = true; // Cancel signal on its timeout
```

### Timeout Classification Matrix

| Scenario | Result.TimedOut | Signal Statuses |
|----------|-----------------|-----------------|
| Soft global timeout, all succeed | `false` | All `Succeeded` |
| Soft global timeout, signal timeout | `true` | `TimedOut` + `Succeeded` |
| Hard global timeout (cancel) | `true` | `TimedOut` (unfinished) + completed |
| Per-signal timeout only | `true` | `TimedOut` + `Succeeded` |

## Policies

Control failure handling behavior:

### FailFast

Throws `AggregateException` on first signal failure. Halts remaining signals in sequential mode.

```csharp
options.Policy = IgnitionPolicy.FailFast;
```

**Use Case:** Critical initialization where any failure should abort startup

### BestEffort (Default)

Logs failures but continues startup. All signals complete regardless of individual failures.

```csharp
options.Policy = IgnitionPolicy.BestEffort;
```

**Use Case:** Resilient startup with optional components

### ContinueOnTimeout

Proceeds when global timeout elapses, logs partial results.

```csharp
options.Policy = IgnitionPolicy.ContinueOnTimeout;
```

**Use Case:** Startup with optional slow components

## Dependency-Aware Execution (DAG)

### Graph Builder API

Programmatic dependency declaration:

```csharp
services.AddIgnitionGraph((builder, sp) =>
{
    var db = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "database");
    var cache = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "cache");
    var worker = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "worker");

    builder.AddSignals(new[] { db, cache, worker });
    builder.DependsOn(cache, db);      // Cache depends on Database
    builder.DependsOn(worker, cache);  // Worker depends on Cache
});
```

### Attribute-Based Dependencies

Declarative dependency specification:

```csharp
public class DatabaseSignal : IIgnitionSignal { /* ... */ }

[SignalDependency("database")]
public class CacheSignal : IIgnitionSignal { /* ... */ }

[SignalDependency("cache")]
public class WorkerSignal : IIgnitionSignal { /* ... */ }

// Auto-wire dependencies
services.AddIgnitionGraph((builder, sp) =>
{
    builder.AddSignals(sp.GetServices<IIgnitionSignal>());
    builder.ApplyAttributeDependencies(); // Discovers and applies attributes
});
```

**Supports:**

- Dependency by signal name: `[SignalDependency("signal-name")]`
- Dependency by signal type: `[SignalDependency(typeof(OtherSignal))]`
- Multiple dependencies via multiple attributes

### Graph Features

**Topological Sort**

- Signals ordered by dependencies automatically
- Preserves execution correctness

**Cycle Detection**

- Validates acyclic graph during construction
- Clear diagnostic with exact cycle path:

  ```txt
  Ignition graph contains a cycle: s1 -> s2 -> s3 -> s1.
  Dependency-aware execution requires an acyclic graph.
  ```

**Failure Propagation**

- Failed signal automatically skips all dependents
- Results include `FailedDependencies` property showing prerequisites that failed
- Status: `IgnitionSignalStatus.Skipped`

**Graph Queries**

- `GetDependencies(signal)`: Returns prerequisite signals
- `GetDependents(signal)`: Returns dependent signals
- `GetRootSignals()`: Returns signals with no dependencies
- `GetLeafSignals()`: Returns signals with no dependents

## Ignition Bundles

Reusable, packaged sets of related signals.

### Built-in Bundles

#### HttpDependencyBundle

Verifies HTTP endpoint readiness:

```csharp
// Single endpoint
services.AddIgnitionBundle(
    new HttpDependencyBundle("https://api.example.com/health",
        TimeSpan.FromSeconds(10)));

// Multiple endpoints
services.AddIgnitionBundle(
    new HttpDependencyBundle(
        new[] { "https://api1.example.com", "https://api2.example.com" },
        TimeSpan.FromSeconds(5)));
```

#### DatabaseTrioBundle

Database initialization sequence (connect → schema validation → warmup):

```csharp
services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        databaseName: "primary-db",
        connectFactory: ct => db.OpenAsync(ct),
        validateSchemaFactory: ct => validator.ValidateAsync(ct),
        warmupFactory: ct => cache.WarmAsync(ct),
        defaultTimeout: TimeSpan.FromSeconds(15)));
```

**Features:**

- Automatic dependency configuration
- Optional schema validation step
- Flexible factory-based initialization

### Custom Bundle Creation

Implement `IIgnitionBundle`:

```csharp
public sealed class RedisStarterBundle : IIgnitionBundle
{
    public string Name => "RedisStarter";

    public void ConfigureBundle(IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        // Register signals
        services.AddIgnitionFromTask("redis:connect",
            ct => ConnectAsync(ct), options.DefaultTimeout);

        // Optional dependency graph
        services.AddIgnitionGraph((builder, sp) =>
        {
            // Define dependencies
        });
    }
}
```

### Bundle Registration

```csharp
// With options
services.AddIgnitionBundle(new MyBundle(), opts =>
{
    opts.DefaultTimeout = TimeSpan.FromSeconds(20);
});

// By type
services.AddIgnitionBundle<MyBundle>();

// Multiple bundles
services.AddIgnitionBundles(bundle1, bundle2, bundle3);
```

## Diagnostics & Observability

### Result Inspection

```csharp
var result = await coordinator.GetResultAsync();

// Overall status
Console.WriteLine($"Timed Out: {result.TimedOut}");
Console.WriteLine($"Total Duration: {result.TotalDuration}");

// Per-signal results
foreach (var r in result.Results)
{
    Console.WriteLine($"{r.Name}: {r.Status} in {r.Duration.TotalMilliseconds:F0} ms");

    if (r.Status == IgnitionSignalStatus.Failed)
    {
        Console.WriteLine($"  Error: {r.Exception?.Message}");
    }

    if (r.Status == IgnitionSignalStatus.Skipped)
    {
        Console.WriteLine($"  Failed deps: {string.Join(", ", r.FailedDependencies)}");
    }
}
```

### Signal Status Values

- `Succeeded`: Signal completed successfully
- `Failed`: Signal threw exception
- `TimedOut`: Signal exceeded timeout
- `Skipped`: Signal skipped due to failed dependencies (DAG mode only)

### Slow Signal Logging

Automatically logs top N slowest signals:

```csharp
options.SlowHandleLogCount = 5; // Log 5 slowest signals
```

Output includes:

- Signal name
- Duration
- Status
- Relative percentage of total time

### Activity Tracing

OpenTelemetry-compatible distributed tracing:

```csharp
options.EnableTracing = true;
```

**Features:**

- Activity named `Ignition.WaitAll`
- ActivitySource: `Veggerby.Ignition.IgnitionCoordinator`
- Integrates with standard .NET diagnostics pipeline
- Compatible with OpenTelemetry collectors

## Health Checks

Automatic health check registration:

```csharp
services.AddIgnition(options => { /* ... */ });

// Health check named "ignition-readiness" automatically registered
```

### Health Status Mapping

| Condition | Health Status |
|-----------|--------------|
| All signals succeeded | `Healthy` |
| Soft global timeout (no signal failures) | `Degraded` |
| Hard global timeout (cancellation) | `Unhealthy` |
| One or more signal failures | `Unhealthy` |
| Exception during evaluation | `Unhealthy` |

**Features:**

- Non-blocking (uses cached result)
- Does not trigger re-evaluation
- Integrates with ASP.NET Core health check middleware

## Advanced Adapters

### TaskCompletionSource Extensions

Semantic sugar for signal completion:

```csharp
private readonly TaskCompletionSource _readyTcs = new();

// Success
_readyTcs.Ignited();

// Failure
_readyTcs.IgnitionFailed(new InvalidOperationException("startup failed"));

// Register with coordinator
services.AddIgnitionFor<MyService>(s => s._readyTcs.Task, name: "my-service");
```

### Composite Signal Patterns

**Service Collection Composition**

```csharp
// All instances must complete
services.AddIgnitionForAll<ShardIndexer>(i => i.ReadyTask, groupName: "indexers[*]");
```

**Custom Factory Composition**

```csharp
services.AddIgnitionFromFactory(
    taskFactory: sp => Task.WhenAll(
        sp.GetRequiredService<Primary>().InitAsync(),
        sp.GetRequiredService<Replica>().InitAsync()),
    name: "multi-db");
```

## Performance Features

### Concurrency Limiting

Control maximum parallel tasks:

```csharp
options.MaxDegreeOfParallelism = 4;
```

**Applies to:**

- `Parallel` execution mode
- `DependencyAware` execution mode (within independent branches)

**Not applicable to:**

- `Sequential` execution mode (already serial)

### Idempotent Execution

Signals execute exactly once per coordinator instance:

```csharp
var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

await coordinator.WaitAllAsync(); // Executes signals
await coordinator.WaitAllAsync(); // Returns cached result (no re-execution)

var result = await coordinator.GetResultAsync(); // Same cached result
```

**Benefits:**

- Prevents duplicate initialization side-effects
- Safe multiple awaits
- Consistent result inspection

### Lazy Task Execution

Signal tasks created lazily on first `WaitAllAsync()`:

```csharp
services.AddIgnitionFromTask("expensive",
    ct => ExpensiveInitAsync(ct)); // Not invoked until WaitAllAsync()
```

**Benefits:**

- No initialization overhead during DI container setup
- Service provider fully constructed before signal execution
- Supports service resolution within signal factories

## Configuration Options Summary

Complete `IgnitionOptions` reference:

```csharp
services.AddIgnition(options =>
{
    // Execution strategy
    options.ExecutionMode = IgnitionExecutionMode.Parallel; // or Sequential, DependencyAware
    options.MaxDegreeOfParallelism = 4; // null = unlimited (Parallel/DAG modes)

    // Timeouts
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.CancelOnGlobalTimeout = false; // true = hard timeout
    options.CancelIndividualOnTimeout = false; // true = cancel signals on their timeout

    // Policies
    options.Policy = IgnitionPolicy.BestEffort; // or FailFast, ContinueOnTimeout

    // Diagnostics
    options.EnableTracing = false; // true = emit Activity
    options.SlowHandleLogCount = 0; // > 0 logs N slowest signals
});
```

## Summary

Veggerby.Ignition provides:

✅ **Simple Abstraction**: `IIgnitionSignal` for any async readiness operation
✅ **Flexible Execution**: Parallel, Sequential, or Dependency-Aware (DAG)
✅ **Robust Timeout Management**: Global + per-signal with soft/hard semantics
✅ **Configurable Policies**: FailFast, BestEffort, ContinueOnTimeout
✅ **Dependency Graphs**: Programmatic or attribute-based with cycle detection
✅ **Composable Bundles**: Reusable signal packages for common patterns
✅ **Rich Diagnostics**: Result inspection, slow signal logging, failure propagation
✅ **Observability**: Activity tracing, health checks, structured logging
✅ **Performance**: Concurrency limiting, idempotent execution, lazy evaluation
✅ **Zero Dependencies**: Minimal surface using only BCL + Microsoft.Extensions.*

Perfect for coordinating startup readiness in modern .NET applications with minimal overhead and maximum flexibility.
