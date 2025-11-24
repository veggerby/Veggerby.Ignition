# Veggerby.Ignition

![Build](https://img.shields.io/github/actions/workflow/status/veggerby/Veggerby.Ignition/ci-release.yml?label=build&style=flat-square)
![Coverage](https://img.shields.io/codecov/c/github/veggerby/Veggerby.Ignition?style=flat-square)
![NuGet](https://img.shields.io/nuget/v/Veggerby.Ignition?label=nuget&style=flat-square)
![License](https://img.shields.io/github/license/veggerby/Veggerby.Ignition?style=flat-square)

Veggerby.Ignition is a lightweight, extensible startup readiness ("ignition") coordination library for .NET applications. Register ignition signals representing asynchronous initialization tasks (cache warmers, external connections, background services) and await them collectively with rich diagnostics, configurable policies, timeouts, tracing, and health checks.

## Features

- Simple `IIgnitionSignal` abstraction (name, optional timeout, `WaitAsync`)
- Coordinated waiting via `IIgnitionCoordinator`
- Global timeout (soft by default) with per-signal overrides
- Policies: FailFast, BestEffort, ContinueOnTimeout
- Health check integration (adds `ignition-readiness` check)
- Activity tracing (toggle via options)
- Slow handle logging (top N longest signals)
- Task and cancellable Task factory adapters (`IgnitionSignal.FromTask`, `FromTaskFactory`)
- Idempotent execution (signals evaluated once, result cached)
- Execution modes: Parallel (default), Sequential, or **Dependency-Aware (DAG)**
- Optional parallelism limiting via MaxDegreeOfParallelism
- Cooperative cancellation on global or per-signal timeout
- **Dependency-aware execution graph (DAG)** with topological sort and cycle detection
- **Declarative dependency declaration** via `[SignalDependency]` attribute
- **Automatic parallel execution** of independent branches in dependency graphs

## Quick Start

```csharp
// Program.cs or hosting setup
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(10);
    options.Policy = IgnitionPolicy.BestEffort; // or FailFast / ContinueOnTimeout
    options.EnableTracing = true; // emits Activity if diagnostics consumed
    options.ExecutionMode = IgnitionExecutionMode.Parallel; // or Sequential / DependencyAware
    options.MaxDegreeOfParallelism = 4; // limit concurrency (Parallel/DependencyAware modes)
    options.CancelOnGlobalTimeout = true; // attempt to cancel still-running signals if global timeout hits
    options.CancelIndividualOnTimeout = true; // cancel a signal if its own timeout elapses
});

// Register concrete ignition signals
builder.Services.AddIgnitionSignal(new CustomConnectionSignal());

// Wrap an existing task
builder.Services.AddIgnitionFromTask("cache-warm", cacheWarmTask, timeout: TimeSpan.FromSeconds(5));

// Wrap a cancellable task factory (invoked lazily once)
builder.Services.AddIgnitionFromTask(
    name: "search-index",
    readyTaskFactory: ct => indexBuilder.BuildAsync(ct),
    timeout: TimeSpan.FromSeconds(30));

// Adapt a service instance exposing a readiness Task (e.g. BackgroundService with ReadyTask)
builder.Services.AddIgnitionFor<MyBackgroundWorker>(w => w.ReadyTask);

// Adapt many instances as one composite signal (e.g. multiple Kafka consumers)
builder.Services.AddIgnitionForAll<KafkaConsumer>(c => c.ReadyTask, groupName: "KafkaConsumer[*]");

// TaskCompletionSource helpers (semantic sugar)
_workerReady.Ignited();            // success
// or on failure
_workerReady.IgnitionFailed(ex);

var app = builder.Build();

// Await readiness before starting interactive loop or accepting traffic
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

## Creating a Custom Signal

```csharp
public sealed class CustomConnectionSignal : IIgnitionSignal
{
    public string Name => "db-connection";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(8); // optional override

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        // Perform initialization (e.g. open connection, ping server)
        await DatabaseClient.InitializeAsync(cancellationToken);
    }
}
```

Register it:

```csharp
services.AddIgnitionSignal<CustomConnectionSignal>();
```

## Policies

| Policy | Behavior |
| ------ | -------- |
| FailFast | Throws if any signal fails (aggregate exceptions). |
| BestEffort | Logs failures, continues startup (default). |
| ContinueOnTimeout | Proceeds when global timeout elapses; logs partial results. |

## Diagnostics & Results

After ignition completes:

```csharp
var coord = provider.GetRequiredService<IIgnitionCoordinator>();
var result = await coord.GetResultAsync();
if (result.TimedOut)
{
    // handle degraded startup
}
foreach (var r in result.Results)
{
    Console.WriteLine($"{r.Name}: {r.Status} in {r.Duration.TotalMilliseconds:F0} ms");
}
```

## Health Check

`AddIgnition` automatically registers a health check named `ignition-readiness` returning:

- Healthy: all signals succeeded
- Degraded: soft global timeout elapsed without per-signal timeouts/failures
- Unhealthy: one or more signals failed, a hard global timeout (with cancellation) occurred, or exception during evaluation

## Tracing

Set `EnableTracing = true` to emit an `Activity` named `Ignition.WaitAll`. Attach listeners via `ActivitySource` to integrate with OpenTelemetry or other observability pipelines.

## Global Timeout Semantics

Ignition exposes two timeout layers:

1. Global timeout (`GlobalTimeout`): A soft deadline unless `CancelOnGlobalTimeout = true`.
    - Soft (default): Execution continues beyond the elapsed deadline; final result only marked timed out if any signal itself timed out.
    - Hard (cancelling): Outstanding signals receive cancellation; result marked timed out and unfinished signals reported with their names and `TimedOut` status.
2. Per-signal timeout (`IIgnitionSignal.Timeout`): Always enforced; if elapsed the signal is marked `TimedOut` and optionally cancelled when `CancelIndividualOnTimeout = true`.

Classification summary:

| Scenario | Result.TimedOut | Signal statuses |
|----------|-----------------|-----------------|
| Soft global timeout, all eventually succeed | False | All Succeeded |
| Soft global timeout, a signal timed out | True | TimedOut + Succeeded |
| Hard global timeout (cancel) | True | TimedOut (unfinished) + any completed |
| Per-signal timeout only | True | TimedOut + Succeeded |

This model avoids penalizing slow but successful initialization while still enabling an upper bound via opt-in cancellation.

## Dependency-Aware Execution (DAG)

Veggerby.Ignition supports dependency-aware execution where signals can declare prerequisites. The coordinator automatically:

- Performs topological sort to determine execution order
- Detects cycles with clear diagnostics (cycle path shown in error message)
- Executes independent branches in parallel automatically
- Skips dependent signals when prerequisites fail

### Defining Dependencies

Use the fluent builder API to define a dependency graph:

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});

// Register signals
builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();

// Define dependency graph
builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    var db = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "database");
    var cache = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "cache");
    var worker = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "worker");
    
    graphBuilder.AddSignals(new[] { db, cache, worker });
    graphBuilder.DependsOn(cache, db);      // Cache depends on Database
    graphBuilder.DependsOn(worker, cache);  // Worker depends on Cache
});
```

### Declarative Dependencies with Attributes

Use `[SignalDependency]` for declarative dependency declaration:

```csharp
public class DatabaseSignal : IIgnitionSignal
{
    public string Name => "database";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);
    public Task WaitAsync(CancellationToken ct) => /* connect to DB */;
}

[SignalDependency("database")]
public class CacheSignal : IIgnitionSignal
{
    public string Name => "cache";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);
    public Task WaitAsync(CancellationToken ct) => /* warm cache */;
}

[SignalDependency("cache")]
public class WorkerSignal : IIgnitionSignal
{
    public string Name => "worker";
    public TimeSpan? Timeout => null;
    public Task WaitAsync(CancellationToken ct) => /* start worker */;
}

// Register and apply attribute-based dependencies
builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();

builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    graphBuilder.AddSignals(signals);
    graphBuilder.ApplyAttributeDependencies(); // Automatically wire dependencies from attributes
});
```

### Dependency Execution Behavior

1. **Parallel Independent Branches**: Signals with no dependency relationship execute concurrently
2. **Sequential Chains**: Dependent signals wait for all prerequisites to complete
3. **Failure Propagation**: If a signal fails, all its dependents are automatically skipped
4. **Structured Diagnostics**: Results include `FailedDependencies` showing which prerequisites failed

Example result inspection:

```csharp
var result = await coordinator.GetResultAsync();
foreach (var r in result.Results)
{
    if (r.Status == IgnitionSignalStatus.Skipped)
    {
        Console.WriteLine($"{r.Name} skipped due to failed dependencies: {string.Join(", ", r.FailedDependencies)}");
    }
}
```

### Cycle Detection

The graph builder validates acyclicity during construction. If a cycle is detected, an `InvalidOperationException` is thrown with the exact cycle path:

```
Ignition graph contains a cycle: s1 -> s2 -> s3 -> s1. 
Dependency-aware execution requires an acyclic graph.
```

## Installation

Package reference (after publishing to NuGet):

```sh
dotnet add package Veggerby.Ignition
```

## Ignition Bundles (Composable Signal Modules)

Ignition bundles enable reusable, packaged sets of signals that can be registered as a unit. This eliminates the need to manually add 10+ related signals individually and enables ecosystem modules like `RedisStarterBundle`, `KafkaConsumerBundle`, or custom infrastructure warmup bundles.

### Built-in Bundles

#### HttpDependencyBundle

Verifies HTTP endpoint readiness by performing GET requests to specified URLs:

```csharp
// Single endpoint
services.AddIgnitionBundle(
    new HttpDependencyBundle("https://api.example.com/health", TimeSpan.FromSeconds(10)));

// Multiple endpoints
services.AddIgnitionBundle(
    new HttpDependencyBundle(
        new[] { "https://api1.example.com/ready", "https://api2.example.com/ready" },
        TimeSpan.FromSeconds(5)));

// Override timeout via bundle options
services.AddIgnitionBundle(
    new HttpDependencyBundle("https://slow-api.example.com"),
    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(30));
```

#### DatabaseTrioBundle

Represents a typical database initialization sequence (connect → validate schema → warmup data):

```csharp
services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        databaseName: "primary-db",
        connectFactory: ct => dbConnection.OpenAsync(ct),
        validateSchemaFactory: ct => schemaValidator.ValidateAsync(ct),
        warmupFactory: ct => dataCache.WarmAsync(ct),
        defaultTimeout: TimeSpan.FromSeconds(15)));

// Only connection and warmup (no schema validation)
services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        "replica-db",
        ct => replicaConnection.OpenAsync(ct),
        warmupFactory: ct => replicaCache.WarmAsync(ct)));
```

The bundle automatically configures dependencies: schema validation depends on connection, and warmup depends on schema validation (or connection if no schema validation).

### Creating Custom Bundles

Implement `IIgnitionBundle` to create reusable signal modules:

```csharp
public sealed class RedisStarterBundle : IIgnitionBundle
{
    private readonly string _connectionString;

    public RedisStarterBundle(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string Name => "RedisStarter";

    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        // Register signals for: connection, health check, and cache warmup
        services.AddIgnitionFromTask(
            "redis:connect",
            ct => ConnectAsync(_connectionString, ct),
            options.DefaultTimeout);

        services.AddIgnitionFromTask(
            "redis:health",
            ct => HealthCheckAsync(ct),
            options.DefaultTimeout);

        services.AddIgnitionFromTask(
            "redis:warmup",
            ct => WarmupCacheAsync(ct),
            options.DefaultTimeout);

        // Optionally configure dependency graph
        services.AddIgnitionGraph((builder, sp) =>
        {
            var signals = sp.GetServices<IIgnitionSignal>();
            var connectSig = signals.First(s => s.Name == "redis:connect");
            var healthSig = signals.First(s => s.Name == "redis:health");
            var warmupSig = signals.First(s => s.Name == "redis:warmup");

            builder.AddSignals(new[] { connectSig, healthSig, warmupSig });
            builder.DependsOn(healthSig, connectSig);
            builder.DependsOn(warmupSig, healthSig);
        });
    }

    private Task ConnectAsync(string connStr, CancellationToken ct) { /* ... */ }
    private Task HealthCheckAsync(CancellationToken ct) { /* ... */ }
    private Task WarmupCacheAsync(CancellationToken ct) { /* ... */ }
}

// Register the bundle
services.AddIgnitionBundle(new RedisStarterBundle("localhost:6379"), opts =>
{
    opts.DefaultTimeout = TimeSpan.FromSeconds(10);
});
```

### Bundle Registration Methods

```csharp
// Register a bundle instance
services.AddIgnitionBundle(new MyBundle(), opts => opts.DefaultTimeout = TimeSpan.FromSeconds(5));

// Register a bundle by type (requires parameterless constructor)
services.AddIgnitionBundle<MyBundle>();

// Register multiple bundles
services.AddIgnitionBundles(bundle1, bundle2, bundle3);
```

### Bundle Options

Use `IgnitionBundleOptions` to configure per-bundle defaults:

```csharp
services.AddIgnitionBundle(new MyBundle(), opts =>
{
    opts.DefaultTimeout = TimeSpan.FromSeconds(20);  // Applied to all signals in bundle
    opts.Policy = IgnitionPolicy.BestEffort;         // Reserved for future use
});
```

Individual signal timeouts (via `IIgnitionSignal.Timeout`) override bundle defaults.

## License

MIT License. See [LICENSE](LICENSE).

## Additional Adapters

Alongside `AddIgnitionFromTask` you can map existing service readiness without custom wrappers:

```csharp
// Single service
services.AddIgnitionFor<CachePrimer>(c => c.ReadyTask, name: "cache-primer");

// Composite for many instances (all must complete)
services.AddIgnitionForAll<ShardIndexer>(i => i.ReadyTask, groupName: "ShardIndexer[*]");

// Arbitrary composition across multiple services
services.AddIgnitionFromFactory(
    taskFactory: sp => Task.WhenAll(
        sp.GetRequiredService<PrimaryConnection>().OpenAsync(),
        sp.GetRequiredService<ReplicaConnection>().WarmAsync()),
    name: "datastore-connections");

// TaskCompletionSource helpers
_readyTcs.Ignited();
_readyTcs.IgnitionFailed(new InvalidOperationException("startup failed"));
```

These helpers are lazy and idempotent: service instances are resolved on the first wait and the readiness task(s) are cached.
