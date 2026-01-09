# Veggerby.Ignition

![Build](https://img.shields.io/github/actions/workflow/status/veggerby/Veggerby.Ignition/ci-release.yml?label=build&style=flat-square)
![Coverage](https://img.shields.io/codecov/c/github/veggerby/Veggerby.Ignition?style=flat-square)
![NuGet](https://img.shields.io/nuget/v/Veggerby.Ignition?label=nuget&style=flat-square)
![License](https://img.shields.io/github/license/veggerby/Veggerby.Ignition?style=flat-square)

Veggerby.Ignition is a lightweight, extensible startup readiness ("ignition") coordination library for .NET applications. Register ignition signals representing asynchronous initialization tasks (cache warmers, external connections, background services) and await them collectively with rich diagnostics, configurable policies, timeouts, tracing, and health checks.

## Simple Mode (Recommended for Most Apps)

For 80-90% of use cases, use the **Simple Mode API** to get production-ready startup coordination in fewer than 10 lines:

```csharp
// Web API Application
builder.Services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile()
    .AddSignal("database", async ct => await db.ConnectAsync(ct))
    .AddSignal("cache", async ct => await cache.WarmAsync(ct))
    .AddSignal("external-api", async ct => await api.HealthCheckAsync(ct)));

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

**Pre-configured Profiles:**

- **`.UseWebApiProfile()`**: 30s timeout, BestEffort policy, Parallel execution, Tracing enabled
- **`.UseWorkerProfile()`**: 60s timeout, FailFast policy, Parallel execution, Tracing enabled
- **`.UseCliProfile()`**: 15s timeout, FailFast policy, Sequential execution, Tracing disabled

**Customization:** Override any profile defaults or access advanced features:

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile()
    .WithGlobalTimeout(TimeSpan.FromSeconds(45))
    .WithDefaultSignalTimeout(TimeSpan.FromSeconds(15))
    .WithTracing(false)
    .AddSignal("my-signal", ...)
    .ConfigureAdvanced(options =>
    {
        options.MaxDegreeOfParallelism = 5;
        options.CancelOnGlobalTimeout = true;
    }));
```

📚 **[Simple Mode Sample](samples/SimpleMode/README.md)** | 🚀 **[Full API Documentation](#quick-start)**

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
- Execution modes: Parallel (default), Sequential, **Dependency-Aware (DAG)**, or **Staged (multi-phase)**
- Optional parallelism limiting via MaxDegreeOfParallelism
- Cooperative cancellation on global or per-signal timeout
- **Pluggable timeout strategies** via `IIgnitionTimeoutStrategy` for advanced timeout behavior
- **Pluggable metrics adapter** via `IIgnitionMetrics` for zero-dependency observability integration
- **Dependency-aware execution graph (DAG)** with topological sort and cycle detection
- **Declarative dependency declaration** via `[SignalDependency]` attribute
- **Automatic parallel execution** of independent branches in dependency graphs
- **State machine with lifecycle events** for real-time observability (`NotStarted`, `Running`, `Completed`, `Failed`, `TimedOut`)
- **Event hooks** for signal-level and coordinator-level progress monitoring
- **Staged execution (multi-phase startup pipeline)** with configurable cross-stage policies
- **Timeline export** for Gantt-like startup visualization and analysis (`result.ExportTimeline()`)
- **Recording and replay** for diagnosing startup issues, CI regression detection, and what-if simulations (`result.ExportRecording()`, `IgnitionReplayer`)

📚 **[Full Documentation](docs/README.md)** | 🚀 **[Getting Started Guide](docs/getting-started.md)** | 📖 **[Features Overview](docs/features.md)**

## Integration Packages

Extend Ignition with ready-made signals for popular infrastructure components:

### Message Brokers

- **[Veggerby.Ignition.RabbitMq](src/Veggerby.Ignition.RabbitMq/README.md)** - RabbitMQ connection and topology verification
  - Connection and channel readiness
  - Optional queue/exchange verification
  - Publish/consume round-trip test
  - ```dotnet add package Veggerby.Ignition.RabbitMq```

- **[Veggerby.Ignition.MassTransit](src/Veggerby.Ignition.MassTransit/README.md)** - MassTransit bus readiness via health checks
  - Transport-agnostic (RabbitMQ, Azure Service Bus, in-memory, etc.)
  - Leverages MassTransit's built-in health checks
  - Works with existing `IBus` instance
  - ```dotnet add package Veggerby.Ignition.MassTransit```

**Example:** Verify RabbitMQ and MassTransit readiness:

```csharp
builder.Services.AddIgnition();

// Direct RabbitMQ verification
builder.Services.AddRabbitMqReadiness("amqp://localhost", options =>
{
    options.WithQueue("orders");
    options.Timeout = TimeSpan.FromSeconds(5);
});

// MassTransit bus readiness
builder.Services.AddMassTransit(/* configure transport */);
builder.Services.AddMassTransitReadiness();

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

📚 **[Messaging Sample](samples/Messaging/README.md)** demonstrates both packages with Docker setup guide.

### Databases

- **[Veggerby.Ignition.SqlServer](src/Veggerby.Ignition.SqlServer/README.md)** - SQL Server connection and schema readiness
  - Connection establishment verification
  - Optional validation query execution
  - Activity tracing and structured logging
  - ```dotnet add package Veggerby.Ignition.SqlServer```

- **[Veggerby.Ignition.Postgres](src/Veggerby.Ignition.Postgres/README.md)** - PostgreSQL connection and schema readiness
  - Connection establishment verification
  - Optional validation query execution
  - Activity tracing and structured logging
  - ```dotnet add package Veggerby.Ignition.Postgres```

- **[Veggerby.Ignition.Marten](src/Veggerby.Ignition.Marten/README.md)** - Marten document store readiness
  - Document store connectivity verification
  - Integrates with existing `IDocumentStore`
  - Activity tracing and structured logging
  - ```dotnet add package Veggerby.Ignition.Marten```

- **[Veggerby.Ignition.MongoDb](src/Veggerby.Ignition.MongoDb/README.md)** - MongoDB cluster connection readiness
  - Cluster connectivity verification (ping)
  - Optional collection existence validation
  - Activity tracing and structured logging
  - ```dotnet add package Veggerby.Ignition.MongoDb```

**Example:** Verify database readiness before startup:

```csharp
builder.Services.AddIgnition();

// SQL Server
builder.Services.AddSqlServerReadiness(
    "Server=localhost;Database=MyDb;Trusted_Connection=True;",
    options => options.ValidationQuery = "SELECT 1");

// PostgreSQL
builder.Services.AddPostgresReadiness(
    "Host=localhost;Database=mydb;Username=user;Password=pass",
    options => options.ValidationQuery = "SELECT 1");

// Marten (requires Marten to be configured first)
builder.Services.AddMarten(opts => opts.Connection(connectionString));
builder.Services.AddMartenReadiness();

// MongoDB
builder.Services.AddMongoDbReadiness(
    "mongodb://localhost:27017",
    options =>
    {
        options.DatabaseName = "mydb";
        options.VerifyCollection = "users";
    });

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### HTTP & External Services

- **[Veggerby.Ignition.Http](src/Veggerby.Ignition.Http/README.md)** - HTTP endpoint readiness verification
  - Flexible status code matching (200, 204, etc.)
  - Custom response body validation (JSON, text)
  - Custom headers support (Authorization, User-Agent)
  - Efficient HttpClient reuse via IHttpClientFactory
  - ```dotnet add package Veggerby.Ignition.Http```

- **[Veggerby.Ignition.Grpc](src/Veggerby.Ignition.Grpc/README.md)** - gRPC service readiness via health check protocol
  - Standard grpc.health.v1.Health protocol support
  - Service-specific health queries
  - Channel state verification
  - Efficient gRPC channel reuse
  - ```dotnet add package Veggerby.Ignition.Grpc```

- **[Veggerby.Ignition.Orleans](src/Veggerby.Ignition.Orleans/README.md)** - Orleans cluster client readiness
  - Cluster client availability verification
  - Works with existing registered `IClusterClient`
  - Activity tracing and structured logging
  - ```dotnet add package Veggerby.Ignition.Orleans```

**Example:** Verify external service readiness:

```csharp
builder.Services.AddIgnition();

// HTTP endpoint with response validation
builder.Services.AddHttpReadiness(
    "https://api.example.com/health",
    options =>
    {
        options.ExpectedStatusCodes = [200, 204];
        options.ValidateResponse = async (response) =>
        {
            var content = await response.Content.ReadAsStringAsync();
            return content.Contains("healthy");
        };
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// gRPC service
builder.Services.AddGrpcReadiness(
    "https://grpc.example.com",
    options =>
    {
        options.ServiceName = "myservice";
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// Orleans cluster (requires IClusterClient to be registered)
builder.Services.AddOrleansClient(clientBuilder => clientBuilder.UseLocalhostClustering());
builder.Services.AddOrleansReadiness(options => options.Timeout = TimeSpan.FromSeconds(10));

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

## Quick Start

```csharp
// Program.cs or hosting setup
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(10);
    options.Policy = IgnitionPolicy.BestEffort; // or FailFast / ContinueOnTimeout
    options.EnableTracing = true; // emits Activity if diagnostics consumed
    options.ExecutionMode = IgnitionExecutionMode.Parallel; // or Sequential / DependencyAware / Staged
    options.MaxDegreeOfParallelism = 4; // limit concurrency (Parallel/DependencyAware/Staged modes)
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

## State Machine & Event Hooks

The coordinator exposes a state machine with observable lifecycle events, enabling real-time monitoring without polling.

### Lifecycle States

| State | Description |
| ----- | ----------- |
| `NotStarted` | Coordinator has not yet started executing signals |
| `Running` | Signals are being executed |
| `Completed` | All signals completed successfully |
| `Failed` | One or more signals failed |
| `TimedOut` | Global timeout occurred before completion |

### Event Hooks

Subscribe to lifecycle events for real-time progress monitoring:

```csharp
var coord = provider.GetRequiredService<IIgnitionCoordinator>();

// Signal-level events
coord.SignalStarted += (sender, e) =>
    Console.WriteLine($"Signal '{e.SignalName}' started at {e.Timestamp}");

coord.SignalCompleted += (sender, e) =>
    Console.WriteLine($"Signal '{e.SignalName}' {e.Status} in {e.Duration.TotalMilliseconds:F0} ms");

// Coordinator-level events
coord.GlobalTimeoutReached += (sender, e) =>
    Console.WriteLine($"Global timeout reached! Pending signals: {string.Join(", ", e.PendingSignals)}");

coord.CoordinatorCompleted += (sender, e) =>
    Console.WriteLine($"Coordinator finished: {e.FinalState} in {e.TotalDuration.TotalMilliseconds:F0} ms");

// Check current state
Console.WriteLine($"Current state: {coord.State}");

await coord.WaitAllAsync();
```

### Available Events

| Event | Arguments | When Raised |
| ----- | --------- | ----------- |
| `SignalStarted` | `SignalName`, `Timestamp` | When each signal begins execution |
| `SignalCompleted` | `SignalName`, `Status`, `Duration`, `Timestamp`, `Exception` | When each signal finishes (success, failure, timeout, or skipped) |
| `GlobalTimeoutReached` | `GlobalTimeout`, `Elapsed`, `Timestamp`, `PendingSignals` | When global timeout deadline elapses |
| `CoordinatorCompleted` | `FinalState`, `TotalDuration`, `Timestamp`, `Result` | When coordinator reaches terminal state |

Event handlers are invoked synchronously and should be fast/non-blocking. Exceptions in handlers are caught and logged but don't break coordinator execution.

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

## Timeline Export (Gantt-like Output)

Export a structured timeline of startup events for analysis, visualization, and debugging. The timeline provides a Gantt-like view of signal execution including start/end times, concurrent groups, and timeout boundaries.

```csharp
var result = await coord.GetResultAsync();

// Export to IgnitionTimeline object
var timeline = result.ExportTimeline(
    executionMode: "Parallel",
    globalTimeout: TimeSpan.FromSeconds(30));

// Export directly to JSON
var json = result.ExportTimelineJson(indented: true);
```

### Timeline Data

The exported timeline includes:

- **Events**: Per-signal timing with start/end times relative to ignition start (milliseconds)
- **Concurrent groups**: Signals that executed in parallel are assigned the same group ID
- **Boundaries**: Global timeout markers and completion timestamps
- **Stages**: Stage timing when using staged execution mode
- **Summary**: Statistics including slowest/fastest signals, max concurrency, and status counts

### JSON Schema (v1.0)

```json
{
  "schemaVersion": "1.0",
  "totalDurationMs": 150.5,
  "timedOut": false,
  "executionMode": "Parallel",
  "globalTimeoutMs": 30000,
  "events": [
    {
      "signalName": "db-connection",
      "status": "Succeeded",
      "startMs": 0,
      "endMs": 50.2,
      "durationMs": 50.2,
      "concurrentGroup": 1
    },
    {
      "signalName": "cache-warmup",
      "status": "Succeeded",
      "startMs": 0,
      "endMs": 75.3,
      "durationMs": 75.3,
      "concurrentGroup": 1
    }
  ],
  "boundaries": [
    { "type": "GlobalTimeoutConfigured", "timeMs": 30000 },
    { "type": "IgnitionComplete", "timeMs": 150.5 }
  ],
  "summary": {
    "totalSignals": 2,
    "succeededCount": 2,
    "failedCount": 0,
    "timedOutCount": 0,
    "skippedCount": 0,
    "cancelledCount": 0,
    "maxConcurrency": 2,
    "slowestSignal": "cache-warmup",
    "slowestDurationMs": 75.3,
    "fastestSignal": "db-connection",
    "fastestDurationMs": 50.2,
    "averageDurationMs": 62.75
  }
}
```

### Use Cases

- **Startup debugging**: Identify which signals are slow or blocking others
- **Container warmup analysis**: Visualize startup sequence in Kubernetes/Docker environments
- **CI timing regression detection**: Compare timeline exports between builds
- **Profiling**: Export timeline data for analysis with external visualization tools

## Recording and Replay

Veggerby.Ignition provides comprehensive recording and replay capabilities for diagnosing slow startup, CI regression detection, and offline simulation. Record ignition runs with full timing, dependency, and failure information, then replay them for analysis.

### Recording an Ignition Run

Export a complete recording from any ignition result:

```csharp
var result = await coordinator.GetResultAsync();
var options = serviceProvider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

// Export to recording object
var recording = result.ExportRecording(
    options: options,
    metadata: new Dictionary<string, string>
    {
        ["environment"] = "production",
        ["version"] = "1.2.3",
        ["hostname"] = Environment.MachineName
    });

// Export directly to JSON
var json = result.ExportRecordingJson(options: options, indented: true);

// Save to file
File.WriteAllText($"ignition-{DateTime.UtcNow:yyyy-MM-dd-HHmmss}.json", json);
```

### Recording Data

The recording captures:

- **Signals**: Name, status, start/end times, duration, stage, dependencies, exception info
- **Configuration**: Execution mode, policy, timeout settings, parallelism settings
- **Stages**: Per-stage timing and outcomes when using staged execution
- **Summary**: Total signals, success/failure counts, slowest/fastest signals, max concurrency

### Replaying and Analyzing Recordings

Use `IgnitionReplayer` to analyze recorded ignition runs:

```csharp
// Load a recording
var json = File.ReadAllText("ignition-recording.json");
var recording = IgnitionRecording.FromJson(json);
var replayer = new IgnitionReplayer(recording);

// Or create directly from a result
var replayer = result.ToReplayer(options);
```

### Validating Recordings

Check recordings for invariant violations and consistency issues:

```csharp
var validation = replayer.Validate();

if (!validation.IsValid)
{
    Console.WriteLine($"Found {validation.ErrorCount} errors and {validation.WarningCount} warnings");
    
    foreach (var issue in validation.Issues)
    {
        Console.WriteLine($"[{issue.Severity}] {issue.Code}: {issue.Message}");
        if (issue.SignalName != null)
            Console.WriteLine($"  Signal: {issue.SignalName}");
    }
}
```

Validation checks include:
- **Timing validation**: Negative durations, end before start, duration drift
- **Dependency order**: Signals starting before dependencies complete
- **Stage execution**: Correct stage ordering and timing
- **Configuration consistency**: Timeout vs actual duration consistency
- **Summary accuracy**: Count matches between signals and summary

### What-If Simulations

Simulate scenarios to understand how changes would affect startup:

```csharp
// Simulate what happens if a signal times out earlier
var timeoutSim = replayer.SimulateEarlierTimeout("slow-database", newTimeoutMs: 500);
Console.WriteLine($"Affected signals: {string.Join(", ", timeoutSim.AffectedSignals)}");

foreach (var signal in timeoutSim.SimulatedSignals.Where(s => s.Status != "Succeeded"))
{
    Console.WriteLine($"  {signal.SignalName}: {signal.Status}");
}

// Simulate what happens if a signal fails
var failureSim = replayer.SimulateFailure("cache-connection");
Console.WriteLine($"Failure would affect: {string.Join(", ", failureSim.AffectedSignals)}");
```

### Comparing Recordings

Compare two recordings to detect regressions or differences between environments:

```csharp
var baseline = IgnitionRecording.FromJson(File.ReadAllText("prod-baseline.json"));
var current = IgnitionRecording.FromJson(File.ReadAllText("current-run.json"));

var baselineReplayer = new IgnitionReplayer(baseline);
var comparison = baselineReplayer.CompareTo(current);

Console.WriteLine($"Total duration change: {comparison.DurationDifferenceMs:F0}ms ({comparison.DurationChangePercent:+0.0;-0.0}%)");

if (comparison.AddedSignals.Count > 0)
    Console.WriteLine($"New signals: {string.Join(", ", comparison.AddedSignals)}");

if (comparison.RemovedSignals.Count > 0)
    Console.WriteLine($"Removed signals: {string.Join(", ", comparison.RemovedSignals)}");

// Find signals with status changes
var statusChanges = comparison.SignalComparisons.Where(c => c.StatusChanged);
foreach (var change in statusChanges)
{
    Console.WriteLine($"  {change.SignalName}: {change.Status1} -> {change.Status2}");
}

// Find signals that got significantly slower
var slowdowns = comparison.SignalComparisons.Where(c => c.DurationChangePercent > 20);
foreach (var slow in slowdowns.OrderByDescending(c => c.DurationChangePercent))
{
    Console.WriteLine($"  {slow.SignalName}: +{slow.DurationDifferenceMs:F0}ms ({slow.DurationChangePercent:+0}%)");
}
```

### Analysis Methods

Additional analysis capabilities:

```csharp
// Find slow signals (above threshold)
var slowSignals = replayer.IdentifySlowSignals(minDurationMs: 100);
foreach (var slow in slowSignals)
{
    Console.WriteLine($"Slow: {slow.SignalName} ({slow.DurationMs:F0}ms)");
}

// Find signals on the critical path (blocking total duration)
var criticalPath = replayer.IdentifyCriticalPath();
Console.WriteLine($"Critical path: {string.Join(" -> ", criticalPath.Select(s => s.SignalName))}");

// Get execution order
var order = replayer.GetExecutionOrder();
Console.WriteLine($"Execution order: {string.Join(", ", order)}");

// Find concurrent groups
var groups = replayer.GetConcurrentGroups();
Console.WriteLine($"Found {groups.Count} concurrent groups");
foreach (var group in groups)
{
    Console.WriteLine($"  Parallel: {string.Join(", ", group)}");
}
```

### Recording JSON Schema (v1.0)

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
    "globalTimeoutMs": 30000,
    "cancelOnGlobalTimeout": false,
    "cancelIndividualOnTimeout": true
  },
  "signals": [
    {
      "signalName": "db-connection",
      "status": "Succeeded",
      "startMs": 0,
      "endMs": 150.2,
      "durationMs": 150.2,
      "configuredTimeoutMs": 10000
    },
    {
      "signalName": "cache-warmup",
      "status": "Failed",
      "startMs": 0,
      "endMs": 500,
      "durationMs": 500,
      "exceptionType": "System.TimeoutException",
      "exceptionMessage": "Connection timed out"
    }
  ],
  "summary": {
    "totalSignals": 2,
    "succeededCount": 1,
    "failedCount": 1,
    "maxConcurrency": 2,
    "slowestSignalName": "cache-warmup",
    "slowestDurationMs": 500
  },
  "metadata": {
    "environment": "production",
    "version": "1.2.3"
  }
}
```

### Use Cases

- **Prod vs Dev Comparison**: Record startup in production and development, compare to identify environment-specific slowdowns
- **CI Regression Detection**: Save baseline recordings, compare against new builds to catch startup performance regressions
- **Failure Analysis**: Simulate failures to understand dependency chains and cascading effects
- **Capacity Planning**: Analyze critical path to identify optimization opportunities
- **Incident Post-Mortems**: Record and replay startup issues for offline analysis

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

## Timeout Strategy Plugins

For advanced timeout requirements, Veggerby.Ignition supports pluggable timeout strategies via `IIgnitionTimeoutStrategy`. This enables scenarios such as:

- Exponential scaling based on failure count
- Adaptive timeouts (e.g., slow I/O detection)
- Dynamic per-stage deadlines
- User-defined per-class or per-assembly defaults

### Using a Custom Timeout Strategy

Create a strategy implementing `IIgnitionTimeoutStrategy`:

```csharp
public sealed class ExponentialBackoffTimeoutStrategy : IIgnitionTimeoutStrategy
{
    private readonly TimeSpan _baseTimeout;
    private readonly double _multiplier;

    public ExponentialBackoffTimeoutStrategy(TimeSpan baseTimeout, double multiplier = 2.0)
    {
        _baseTimeout = baseTimeout;
        _multiplier = multiplier;
    }

    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(
        IIgnitionSignal signal, 
        IgnitionOptions options)
    {
        // Apply exponential scaling based on signal characteristics
        var timeout = signal.Name.StartsWith("slow-io") 
            ? TimeSpan.FromTicks((long)(_baseTimeout.Ticks * _multiplier))
            : _baseTimeout;
        
        return (timeout, cancelImmediately: true);
    }
}
```

Register the strategy:

```csharp
// Option 1: Register strategy instance directly
services.AddIgnition();
services.AddIgnitionTimeoutStrategy(new ExponentialBackoffTimeoutStrategy(TimeSpan.FromSeconds(5)));

// Option 2: Register strategy type for DI construction
services.AddIgnition();
services.AddIgnitionTimeoutStrategy<MyDependencyAwareTimeoutStrategy>();

// Option 3: Use factory for complex construction
services.AddIgnition();
services.AddIgnitionTimeoutStrategy(sp => 
{
    var config = sp.GetRequiredService<IOptions<TimeoutConfig>>().Value;
    return new ConfigurableTimeoutStrategy(config);
});
```

Alternatively, configure directly via options:

```csharp
services.AddIgnition(options =>
{
    options.TimeoutStrategy = new ExponentialBackoffTimeoutStrategy(TimeSpan.FromSeconds(5));
});
```

### Built-in Strategy

The `DefaultIgnitionTimeoutStrategy` preserves backward-compatible behavior:
- Returns the signal's own `Timeout` property
- Uses the global `CancelIndividualOnTimeout` setting

When no custom strategy is configured, this behavior is applied automatically.

## Metrics Adapter (Zero-Dependency, Pluggable Metrics)

Veggerby.Ignition provides a minimal metrics abstraction that enables integration with observability systems (OpenTelemetry, Prometheus, App Metrics, etc.) without adding any of them as dependencies. This keeps Ignition small while making it observability-friendly.

### The IIgnitionMetrics Interface

```csharp
public interface IIgnitionMetrics
{
    void RecordSignalDuration(string name, TimeSpan duration);
    void RecordSignalStatus(string name, IgnitionSignalStatus status);
    void RecordTotalDuration(TimeSpan duration);
}
```

### Creating a Custom Metrics Implementation

Implement `IIgnitionMetrics` to integrate with your monitoring stack:

```csharp
public sealed class OpenTelemetryIgnitionMetrics : IIgnitionMetrics
{
    private readonly Histogram<double> _signalDuration;
    private readonly Counter<int> _signalStatus;
    private readonly Histogram<double> _totalDuration;

    public OpenTelemetryIgnitionMetrics(Meter meter)
    {
        _signalDuration = meter.CreateHistogram<double>("ignition.signal.duration", "ms");
        _signalStatus = meter.CreateCounter<int>("ignition.signal.status");
        _totalDuration = meter.CreateHistogram<double>("ignition.total.duration", "ms");
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.Record(duration.TotalMilliseconds, new("signal.name", name));
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatus.Add(1, new("signal.name", name), new("status", status.ToString()));
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Record(duration.TotalMilliseconds);
    }
}
```

### Registering Metrics

```csharp
// Option 1: Register a metrics instance directly
services.AddIgnition();
services.AddIgnitionMetrics(new MyMetricsAdapter());

// Option 2: Register via factory for DI dependencies
services.AddIgnition();
services.AddIgnitionMetrics(sp =>
{
    var meterFactory = sp.GetRequiredService<IMeterFactory>();
    var meter = meterFactory.Create("Veggerby.Ignition");
    return new OpenTelemetryIgnitionMetrics(meter);
});

// Option 3: Register by type for DI construction
services.AddIgnition();
services.AddIgnitionMetrics<OpenTelemetryIgnitionMetrics>();
```

Alternatively, configure directly via options:

```csharp
services.AddIgnition(options =>
{
    options.Metrics = new MyMetricsAdapter();
});
```

### Default Behavior

When no metrics implementation is configured (`options.Metrics = null`), no metrics are recorded and there is zero overhead. A `NullIgnitionMetrics` singleton is available for testing or explicit no-op usage:

```csharp
var noopMetrics = NullIgnitionMetrics.Instance;
```

### Metrics Recorded

| Metric | When Recorded | Parameters |
| ------ | ------------- | ---------- |
| Signal Duration | After each signal completes | Signal name, elapsed time |
| Signal Status | After each signal completes | Signal name, status (Succeeded/Failed/TimedOut/etc.) |
| Total Duration | When coordinator completes | Total elapsed time |

Metrics are recorded for all execution modes (Parallel, Sequential, DependencyAware, Staged) and all signal statuses.

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

## Staged Execution (Multi-Phase Startup Pipeline)

Staged execution provides a middle ground between DAGs and pure parallel execution. Signals are grouped into sequential stages/phases, executing in parallel within each stage but sequentially across stages.

### Defining Stages

Use `IStagedIgnitionSignal` or the `AddIgnitionSignalWithStage` extension method:

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed; // or BestEffort / FailFast / EarlyPromotion
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});

// Register signals with explicit stage assignments
// Stage 0: Infrastructure (executes first)
builder.Services.AddIgnitionFromTaskWithStage("db-connection", ct => dbClient.ConnectAsync(ct), stage: 0);
builder.Services.AddIgnitionFromTaskWithStage("redis-connection", ct => redis.ConnectAsync(ct), stage: 0);

// Stage 1: Services (executes after Stage 0 completes)
builder.Services.AddIgnitionFromTaskWithStage("cache-warmup", ct => cache.WarmAsync(ct), stage: 1);
builder.Services.AddIgnitionFromTaskWithStage("search-index", ct => search.BuildIndexAsync(ct), stage: 1);

// Stage 2: Workers (executes after Stage 1 completes)
builder.Services.AddIgnitionFromTaskWithStage("background-processor", ct => processor.StartAsync(ct), stage: 2);
```

### Creating Staged Signals

Implement `IStagedIgnitionSignal` for explicit stage control:

```csharp
public sealed class DatabaseConnectionSignal : IStagedIgnitionSignal
{
    public string Name => "db-connection";
    public int Stage => 0; // Infrastructure stage
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _dbClient.ConnectAsync(cancellationToken);
    }
}

public sealed class CacheWarmupSignal : IStagedIgnitionSignal
{
    public string Name => "cache-warmup";
    public int Stage => 1; // Services stage
    public TimeSpan? Timeout => TimeSpan.FromSeconds(15);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _cache.WarmAsync(cancellationToken);
    }
}
```

### Stage Policies

| Policy | Behavior |
| ------ | -------- |
| `AllMustSucceed` | All signals in current stage must succeed before proceeding (default) |
| `BestEffort` | Proceed when all signals complete, regardless of status |
| `FailFast` | Stop immediately if any signal fails |
| `EarlyPromotion` | Proceed when X% of signals succeed (configurable via `EarlyPromotionThreshold`) |

### Early Promotion

Enable early promotion to start the next stage before all signals in the current stage complete:

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.EarlyPromotion;
    options.EarlyPromotionThreshold = 0.75; // Proceed when 75% of stage signals succeed
});
```

### Stage Timing and Results

Access per-stage timing and status information:

```csharp
var result = await coordinator.GetResultAsync();

if (result.HasStageResults)
{
    foreach (var stage in result.StageResults)
    {
        Console.WriteLine($"Stage {stage.StageNumber}: " +
            $"{stage.SucceededCount}/{stage.TotalSignals} succeeded in {stage.Duration.TotalMilliseconds:F0} ms");
        
        if (stage.HasFailures)
        {
            Console.WriteLine($"  Failures: {stage.FailedCount}");
        }
        
        if (stage.Promoted)
        {
            Console.WriteLine($"  (early promoted at {stage.SuccessRatio:P0})");
        }
    }
}
```

### Execution Behavior

1. **Sequential Stages**: Stage 0 → Stage 1 → Stage 2 (lower numbers first)
2. **Parallel Within Stage**: All signals in a stage execute concurrently
3. **Policy-Controlled Transitions**: Next stage starts based on `StagePolicy`
4. **Unstaged Signals**: Signals not implementing `IStagedIgnitionSignal` default to Stage 0
5. **Respects `MaxDegreeOfParallelism`**: Limits concurrent signals even within stages

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

## Documentation

### Core Guides

- 📖 **[Getting Started](docs/getting-started.md)** - Installation, first signal, common patterns, ASP.NET Core integration
- 👨‍🍳 **[Cookbook](docs/cookbook.md)** - **Battle-tested recipes for real startup problems** (external dependencies, cache warmup, Kubernetes, OpenTelemetry, DAG vs Stages)
- 🎨 **[Integration Recipes](docs/integration-recipes.md)** - Copy-paste-ready patterns for Web API, Worker, and Console hosting
- 🔀 **[Dependency-Aware Execution](docs/dependency-aware-execution.md)** - DAG mode, topological sort, cycle detection
- ⏱ **[Timeout Management](docs/timeout-management.md)** - Two-layer timeouts, soft vs hard semantics, classification matrix
- 📦 **[Bundles](docs/bundles.md)** - Reusable signal packages, built-in bundles, custom bundle creation
- 🛡️ **[Policies](docs/policies.md)** - FailFast, BestEffort, ContinueOnTimeout failure handling
- 📊 **[Observability](docs/observability.md)** - Logging, tracing, health checks, metrics

### Advanced Topics

- 🎯 **[Advanced Patterns](docs/advanced-patterns.md)** - Composite signals, testing strategies, integration patterns
- ⚡ **[Performance Guide](docs/performance.md)** - Execution modes, concurrency tuning, benchmarks
- 🔄 **[Migration Guide](docs/migration.md)** - Version upgrades, breaking changes
- 📚 **[API Reference](docs/api-reference.md)** - Complete API surface documentation
- ✨ **[Features Overview](docs/features.md)** - Comprehensive feature reference

### Sample Projects

- [Simple](samples/Simple/README.md) - Basic usage
- [Advanced](samples/Advanced/README.md) - Complex scenarios
- [DependencyGraph](samples/DependencyGraph/README.md) - DAG execution
- [Bundles](samples/Bundles/README.md) - Bundle usage
- [WebApi](samples/WebApi/README.md) - ASP.NET Core integration
- [Worker](samples/Worker/README.md) - Generic Host / Worker Service integration

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
