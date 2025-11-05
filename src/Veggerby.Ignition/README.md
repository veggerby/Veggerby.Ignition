# Veggerby.Ignition

A lightweight, extensible startup readiness ("ignition") coordination library for .NET applications. Register *ignition signals* representing asynchronous initialization tasks (cache warmers, external connections, background services) and await them collectively with rich diagnostics, configurable policies, timeouts, tracing, and health checks.

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
builder.Services.AddIgnitionTask("cache-warm", cacheWarmTask, timeout: TimeSpan.FromSeconds(5));

// Wrap a cancellable task factory (invoked lazily once)
builder.Services.AddIgnitionTask(
    name: "search-index",
    readyTaskFactory: ct => indexBuilder.BuildAsync(ct),
    timeout: TimeSpan.FromSeconds(30));

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

## Design Notes

- Thread-safe lazy execution ensures signals run at most once.
- Global timeout is soft unless cancellation is enabled (see Global Timeout Semantics).
- Factory-based signals honor cancellation tokens for cooperative shutdown scenarios.
- Sequential mode enables early fail-fast behavior and reduces resource contention.
- Per-signal cancellation requires signal implementations to observe passed CancellationToken.

## Installation

Add a package reference (after publishing):

```sh
dotnet nuget add package Veggerby.Ignition
```

## Roadmap Ideas

- Built-in signals (e.g. `HttpEndpointSignal`, `ChannelDrainSignal`)
- OpenTelemetry semantic conventions integration
- Structured metrics export (histograms for durations)

## License

MIT

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

### Configuration Examples

```csharp
// Soft global timeout (default behavior):
services.AddIgnition(o =>
{
    o.GlobalTimeout = TimeSpan.FromSeconds(5); // deadline hint
    o.CancelOnGlobalTimeout = false;           // remain soft (default)
    o.CancelIndividualOnTimeout = false;       // per-signal timeouts won't cancel tasks
});

// Hard global timeout with cancellation:
services.AddIgnition(o =>
{
    o.GlobalTimeout = TimeSpan.FromSeconds(5);
    o.CancelOnGlobalTimeout = true;            // cancel all outstanding signals at deadline
    o.Policy = IgnitionPolicy.ContinueOnTimeout; // choose continuation policy
});

// Mixed: hard global timeout + per-signal timeout cancellation:
services.AddIgnition(o =>
{
    o.GlobalTimeout = TimeSpan.FromSeconds(10);
    o.CancelOnGlobalTimeout = true;            // hard deadline
    o.CancelIndividualOnTimeout = true;        // cancel slow individual signals
    o.ExecutionMode = IgnitionExecutionMode.Parallel;
    o.MaxDegreeOfParallelism = 4;
});

// Defining a per-signal timeout (hard for that signal only):
services.AddIgnitionTask(
    name: "search-index",
    readyTaskFactory: ct => indexBuilder.BuildAsync(ct),
    timeout: TimeSpan.FromSeconds(30) // this signal will be marked TimedOut if exceeded
);
```
