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
- Execution modes: Parallel (default) or Sequential
- Optional parallelism limiting via MaxDegreeOfParallelism
- Cooperative cancellation on global or per-signal timeout

## Quick Start

```csharp
// Program.cs or hosting setup
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(10);
    options.Policy = IgnitionPolicy.BestEffort; // or FailFast / ContinueOnTimeout
    options.EnableTracing = true; // emits Activity if diagnostics consumed
    options.ExecutionMode = IgnitionExecutionMode.Parallel; // or Sequential
    options.MaxDegreeOfParallelism = 4; // limit concurrency (Parallel mode only)
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

## Installation

Package reference (after publishing to NuGet):

```sh
dotnet add package Veggerby.Ignition
```

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
