# Simple Mode API Sample

This sample demonstrates the **Simple Mode API** for Veggerby.Ignition, which provides an opinionated, minimal-configuration experience for 80-90% of use cases.

## Key Features

- **Single fluent builder API**: Configure everything through one chainable interface
- **Pre-configured profiles**: WebApi, Worker, and CLI profiles with sensible defaults
- **Production-ready in < 10 lines**: Minimal boilerplate for common scenarios
- **Full power available**: Access advanced features via `.ConfigureAdvanced()` when needed

## What This Sample Shows

### 1. Minimal Web API Setup

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile()
    .AddSignal("database", async ct => await db.ConnectAsync(ct))
    .AddSignal("cache", async ct => await cache.WarmAsync(ct))
    .AddSignal("external-api", async ct => await api.HealthCheckAsync(ct)));
```

**Web API Profile Defaults:**
- Global timeout: 30 seconds
- Policy: BestEffort (continues even if non-critical signals fail)
- Execution: Parallel
- Tracing: Enabled

### 2. Worker Service (Fail-Fast)

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseWorkerProfile()
    .AddSignal("message-queue", async ct => await queue.ConnectAsync(ct))
    .AddSignal("storage", async ct => await storage.InitAsync(ct)));
```

**Worker Profile Defaults:**
- Global timeout: 60 seconds
- Policy: FailFast (stops immediately on any failure)
- Execution: Parallel
- Tracing: Enabled

### 3. CLI Application (Sequential)

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseCliProfile()
    .AddSignal("config-load", async ct => await LoadConfigAsync(ct))
    .AddSignal("validate-args", async ct => await ValidateAsync(ct))
    .AddSignal("prepare-output", async ct => await PrepareAsync(ct)));
```

**CLI Profile Defaults:**
- Global timeout: 15 seconds
- Policy: FailFast
- Execution: Sequential (deterministic order)
- Tracing: Disabled

## Running the Sample

```bash
dotnet run --project samples/SimpleMode
```

## Customization

### Override Profile Defaults

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile()
    .WithGlobalTimeout(TimeSpan.FromSeconds(45))
    .WithTracing(false)
    .AddSignal("my-signal", ...));
```

### Access Advanced Features

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile()
    .AddSignal("my-signal", ...)
    .ConfigureAdvanced(options =>
    {
        options.MaxDegreeOfParallelism = 5;
        options.CancelOnGlobalTimeout = true;
    }));
```

## Comparison with Full API

**Simple Mode (recommended for most apps):**

```csharp
services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile()
    .AddSignal("db", ct => db.ConnectAsync(ct)));
```

**Full API (for advanced scenarios):**

```csharp
services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.EnableTracing = true;
});
services.AddIgnitionFromTask("db", ct => db.ConnectAsync(ct));
```

The Simple Mode API achieves the same result with less code and more clarity.

## When to Use Full API

Use the full `AddIgnition()` API when you need:

- Dependency-aware execution (DAG mode)
- Staged execution with early promotion
- Custom timeout strategies
- Hierarchical cancellation scopes
- Fine-grained control over every option

For these scenarios, you can still start with Simple Mode and use `.ConfigureAdvanced()` to access specific features.
