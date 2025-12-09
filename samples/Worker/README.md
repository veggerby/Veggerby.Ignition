# Worker Service / Generic Host Integration Sample

This sample demonstrates the **canonical integration pattern** for using Veggerby.Ignition with .NET Generic Host and Worker Services. It shows how to block host startup until all readiness signals complete.

## What it demonstrates

- **Generic Host integration** with `IHostedService` blocking pattern
- **Worker Service readiness coordination** using `TaskCompletionSource`
- **Startup blocking behavior**: Host does not enter "running" state until ignition completes
- **FailFast policy** for worker scenarios (critical dependency failures stop startup)
- **Real-world worker signals**: message queue, database, distributed cache
- **Background service readiness integration** via `AddIgnitionFor`

## Copy-Paste Ready Pattern

### The Key Pattern: IgnitionHostedService

The core integration pattern is to register an `IHostedService` that blocks in `StartAsync` until ignition completes:

```csharp
public sealed class IgnitionHostedService : IHostedService
{
    private readonly IIgnitionCoordinator _coordinator;
    private readonly ILogger<IgnitionHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public IgnitionHostedService(
        IIgnitionCoordinator coordinator,
        ILogger<IgnitionHostedService> logger,
        IHostApplicationLifetime lifetime)
    {
        _coordinator = coordinator;
        _logger = logger;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Waiting for all ignition signals to complete...");

        try
        {
            await _coordinator.WaitAllAsync(cancellationToken);

            var result = await _coordinator.GetResultAsync();
            _logger.LogInformation("Ignition completed in {Duration}ms", 
                result.TotalDuration.TotalMilliseconds);

            // Handle partial failures based on policy
            var allSucceeded = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            if (!allSucceeded)
            {
                _logger.LogWarning("Some signals failed or timed out");
                // Optionally stop application for FailFast scenarios
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during startup");
            _lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### Full Integration in Program.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Veggerby.Ignition;

var builder = Host.CreateApplicationBuilder(args);

// Configure Ignition for Worker scenarios
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.FailFast; // Workers should fail fast
    options.GlobalTimeout = TimeSpan.FromSeconds(60);
    options.CancelOnGlobalTimeout = true;
    options.EnableTracing = true;
});

// Register your readiness signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<MessageQueueConnectionSignal>();

// Register background workers
builder.Services.AddSingleton<MyWorkerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MyWorkerService>());

// Wait for background worker to be ready
builder.Services.AddIgnitionFor<MyWorkerService>(w => w.ReadyTask, name: "worker-ready");

// CRITICAL: Add IgnitionHostedService to block startup
builder.Services.AddHostedService<IgnitionHostedService>();

var host = builder.Build();
await host.RunAsync();
```

## Signals included

1. **DatabaseConnectionSignal** - Database connection pool initialization (1.3s, 8s timeout)
2. **MessageQueueConnectionSignal** - Message broker connection (1.7s, 10s timeout)
3. **DistributedCacheSignal** - Redis/cache connection (0.9s, 5s timeout)
4. **MessageProcessorWorker ReadyTask** - Background worker initialization (0.5s)

## Configuration

The sample uses **FailFast** policy appropriate for worker services:

- **Policy**: FailFast (critical dependencies must succeed)
- **ExecutionMode**: Parallel (max 4 concurrent)
- **GlobalTimeout**: 60 seconds (workers can take longer to initialize)
- **CancelOnGlobalTimeout**: `true` (hard timeout, cancel remaining signals)
- **EnableTracing**: `true` (Activity-based observability)

## Running the sample

```bash
cd samples/Worker
dotnet run
```

## Expected startup output

```text
info: Worker.IgnitionHostedService[0]
      IgnitionHostedService: Waiting for all ignition signals to complete...
info: Worker.MessageProcessorWorker[0]
      MessageProcessorWorker: Initializing...
info: Worker.Signals.DatabaseConnectionSignal[0]
      Initializing database connection pool...
info: Worker.Signals.MessageQueueConnectionSignal[0]
      Connecting to message queue...
info: Worker.Signals.DistributedCacheSignal[0]
      Connecting to distributed cache (Redis)...
info: Worker.MessageProcessorWorker[0]
      MessageProcessorWorker: Initialization complete, marking ready
info: Worker.MessageProcessorWorker[0]
      MessageProcessorWorker: Starting message processing loop
info: Worker.Signals.DistributedCacheSignal[0]
      Distributed cache connection successful
info: Worker.Signals.DatabaseConnectionSignal[0]
      Database connection pool ready with 20 connections
info: Worker.Signals.MessageQueueConnectionSignal[0]
      Message queue connection established successfully
info: Worker.IgnitionHostedService[0]
      IgnitionHostedService: Ignition completed in 1750ms
info: Worker.IgnitionHostedService[0]
      IgnitionHostedService: All signals succeeded. Host is ready.
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
info: Microsoft.Hosting.Lifetime[0]
      Content root path: /samples/Worker
info: Worker.MessageProcessorWorker[0]
      MessageProcessorWorker: Processed batch of messages
```

## Key concepts demonstrated

### IHostedService Blocking Pattern

The `IgnitionHostedService` blocks in its `StartAsync` method, preventing the Generic Host from entering the "running" state until all ignition signals complete. This ensures:

- Kubernetes readiness probes don't pass until initialization is done
- Health checks reflect actual readiness
- Worker processing doesn't start until dependencies are available
- Clean shutdown on critical startup failures

### Background Worker Readiness

Workers can signal their own readiness using `TaskCompletionSource`:

```csharp
public sealed class MyWorkerService : BackgroundService
{
    private readonly TaskCompletionSource _readyTcs = new();
    public Task ReadyTask => _readyTcs.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Perform initialization
        await InitializeAsync(stoppingToken);
        
        // Signal readiness
        _readyTcs.SetResult();
        
        // Continue with processing loop
        await ProcessLoopAsync(stoppingToken);
    }
}

// Register as ignition signal
builder.Services.AddIgnitionFor<MyWorkerService>(w => w.ReadyTask, name: "worker-ready");
```

### FailFast vs BestEffort

**Workers typically use FailFast** because:
- Critical dependencies (database, message queue) must be available
- Partial initialization can lead to data corruption or message loss
- Better to fail fast and retry (e.g., in Kubernetes) than run in degraded state

**Web APIs typically use BestEffort** because:
- Non-critical services (external APIs, caches) can fail gracefully
- Partial functionality is better than no functionality
- Health checks can still report degraded state

## Integration with Kubernetes

When running in Kubernetes, configure readiness and liveness probes appropriately:

```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: worker
    image: my-worker:latest
    livenessProbe:
      httpGet:
        path: /health/live
        port: 8080
      initialDelaySeconds: 10
      periodSeconds: 30
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 5
      periodSeconds: 10
      failureThreshold: 3
```

The worker won't be marked "ready" until `IgnitionHostedService.StartAsync` completes, which happens only after all signals succeed.

## Comparison: Worker vs Web API

| Aspect | Worker Pattern | Web API Pattern |
|--------|---------------|-----------------|
| **Policy** | FailFast | BestEffort |
| **Timeout** | Longer (60s+) | Shorter (30s) |
| **Cancellation** | Hard timeout | Soft timeout (optional hard) |
| **Blocking** | IHostedService blocks | await before app.Run() |
| **Health Checks** | Optional | Standard ASP.NET Core |
| **Startup Failure** | Exit immediately | May serve degraded |

## Production Considerations

### Timeout Settings

Workers often need longer timeouts than web APIs:
- Message queue connections may require cluster discovery
- Database migrations might run on startup
- Distributed lock acquisition could have backoff

### Observability

Enable Activity tracing for integration with OpenTelemetry:

```csharp
options.EnableTracing = true;
```

This emits an `Activity` named `Ignition.WaitAll` that can be captured by OpenTelemetry collectors.

### Graceful Shutdown

The `IgnitionHostedService` integrates with `IHostApplicationLifetime` to ensure clean shutdown on startup failures:

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Critical failure during startup");
    _lifetime.StopApplication(); // Trigger graceful shutdown
    throw;
}
```

## Alternative: Simple Mode

For most worker scenarios, the Simple Mode API provides the same pattern with less boilerplate:

```csharp
builder.Services.AddSimpleIgnition(ignition => ignition
    .UseWorkerProfile()
    .AddSignal("database", async ct => await db.ConnectAsync(ct))
    .AddSignal("queue", async ct => await queue.ConnectAsync(ct)));

builder.Services.AddHostedService<IgnitionHostedService>();
```

See the [Simple Mode documentation](../../docs/getting-started.md#simple-mode) for details.

## Related Samples

- **[WebApi](../WebApi/README.md)** - ASP.NET Core integration with BestEffort policy
- **[Simple](../Simple/README.md)** - Basic console application pattern
- **[Advanced](../Advanced/README.md)** - Advanced policy and execution modes

## Additional Resources

- [Integration Recipes Documentation](../../docs/integration-recipes.md)
- [Getting Started Guide](../../docs/getting-started.md)
- [Policies Documentation](../../docs/policies.md)
