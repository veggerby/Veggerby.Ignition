# Integration Recipes

This guide provides **copy-paste-ready integration patterns** for the most common .NET hosting models. These are opinionated, production-tested recipes that demonstrate how to cleanly integrate Veggerby.Ignition as a startup gate.

## Quick Reference

| Hosting Model | Policy | Timeout | Pattern | Sample |
|--------------|--------|---------|---------|--------|
| **ASP.NET Core Web API** | BestEffort | 30s | `await coordinator.WaitAllAsync()` before `app.Run()` | [WebApi Sample](../samples/WebApi/) |
| **Generic Host / Worker** | FailFast | 60s | `IHostedService` blocks in `StartAsync` | [Worker Sample](../samples/Worker/) |
| **Console Application** | FailFast | 15s | Direct `await coordinator.WaitAllAsync()` | [Simple Sample](../samples/Simple/) |

---

## Recipe 1: ASP.NET Core Web API

**Use Case**: Production web applications, REST APIs, gRPC services
**Policy**: BestEffort (tolerates non-critical failures)
**Blocking**: Between `app.Build()` and `app.Run()`

### Pattern

```csharp
using Veggerby.Ignition;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddHealthChecks(); // ignition-readiness auto-registered

// Configure Ignition for Web API
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort; // Continue despite non-critical failures
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.CancelOnGlobalTimeout = false; // Soft timeout
    options.CancelIndividualOnTimeout = true;
    options.EnableTracing = true;
    options.MaxDegreeOfParallelism = 4;
});

// Register startup signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<CacheWarmupSignal>();
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle("https://api.partner.com/health", TimeSpan.FromSeconds(5)));

var app = builder.Build();

// Configure middleware
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// CRITICAL: Wait for ignition BEFORE starting the web server
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();

try
{
    logger.LogInformation("Starting application initialization...");
    
    await coordinator.WaitAllAsync();
    
    var result = await coordinator.GetResultAsync();
    logger.LogInformation("Initialization completed in {Duration}ms", 
        result.TotalDuration.TotalMilliseconds);
    
    // Log warnings for failed signals (BestEffort policy allows continuing)
    foreach (var signal in result.Results.Where(r => r.Status != IgnitionSignalStatus.Succeeded))
    {
        logger.LogWarning("Signal {Name} finished with status {Status}", 
            signal.Name, signal.Status);
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize application");
    throw; // Prevent application from starting
}

logger.LogInformation("Web API is ready to accept requests");
app.Run();
```

### Simplified with Simple Mode

```csharp
using Veggerby.Ignition;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// Use pre-configured Web API profile
builder.Services.AddSimpleIgnition(ignition => ignition
    .UseWebApiProfile() // 30s timeout, BestEffort, Parallel, Tracing enabled
    .AddSignal("database", async ct => await db.ConnectAsync(ct))
    .AddSignal("cache", async ct => await cache.WarmAsync(ct))
    .AddSignal("external-api", async ct => await api.HealthCheckAsync(ct)));

var app = builder.Build();

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health");

// Wait for readiness
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

app.Run();
```

### Key Characteristics

✅ **BestEffort Policy**: Tolerates failures in non-critical services (cache, external APIs)
✅ **Soft Global Timeout**: Logs warnings but doesn't force cancellation
✅ **Health Check Integration**: `/health/ready` reflects ignition status
✅ **Parallel Execution**: Fast startup with concurrent initialization
✅ **Activity Tracing**: Integrates with OpenTelemetry/APM

### When to Use

- Production web applications
- REST APIs and GraphQL services
- Applications with optional external dependencies
- Services behind load balancers with health checks

### Kubernetes Integration

```yaml
apiVersion: v1
kind: Pod
spec:
  containers:
  - name: web-api
    image: my-api:latest
    ports:
    - containerPort: 8080
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 5
      periodSeconds: 10
    livenessProbe:
      httpGet:
        path: /health
        port: 8080
      initialDelaySeconds: 30
      periodSeconds: 30
```

---

## Recipe 2: Generic Host / Worker Service

**Use Case**: Background workers, message processors, scheduled jobs
**Policy**: FailFast (critical dependencies must succeed)
**Blocking**: IHostedService blocks in `StartAsync`

### Pattern: IgnitionHostedService

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition;

/// <summary>
/// IHostedService that blocks Generic Host startup until all Ignition signals complete.
/// Copy this class into your worker project.
/// </summary>
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

            var allSucceeded = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            if (!allSucceeded)
            {
                _logger.LogWarning("Some signals failed or timed out");
                foreach (var signal in result.Results.Where(r => r.Status != IgnitionSignalStatus.Succeeded))
                {
                    _logger.LogWarning("Signal {Name}: {Status}", signal.Name, signal.Status);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Startup was cancelled");
            _lifetime.StopApplication();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure during startup");
            _lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down");
        return Task.CompletedTask;
    }
}
```

### Program.cs Integration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Veggerby.Ignition;

var builder = Host.CreateApplicationBuilder(args);

// Configure Ignition for Worker scenarios
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.FailFast; // Workers should fail fast
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.GlobalTimeout = TimeSpan.FromSeconds(60);
    options.CancelOnGlobalTimeout = true; // Hard timeout
    options.CancelIndividualOnTimeout = true;
    options.EnableTracing = true;
});

// Register startup readiness signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<MessageQueueConnectionSignal>();
builder.Services.AddIgnitionSignal<DistributedCacheSignal>();

// Register background worker
builder.Services.AddSingleton<MessageProcessorWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageProcessorWorker>());

// Wait for background worker to signal readiness
builder.Services.AddIgnitionFor<MessageProcessorWorker>(
    w => w.ReadyTask, 
    name: "worker-ready");

// CRITICAL: Register IgnitionHostedService to block startup
builder.Services.AddHostedService<IgnitionHostedService>();

var host = builder.Build();
await host.RunAsync();
```

### Background Worker with Readiness Signal

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class MessageProcessorWorker : BackgroundService
{
    private readonly ILogger<MessageProcessorWorker> _logger;
    private readonly TaskCompletionSource _readyTcs = new();

    public Task ReadyTask => _readyTcs.Task;

    public MessageProcessorWorker(ILogger<MessageProcessorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Initializing worker...");

            // Perform initialization (subscribe to queue, setup handlers, etc.)
            await InitializeAsync(stoppingToken);

            _logger.LogInformation("Worker initialization complete");

            // Signal readiness
            _readyTcs.SetResult();

            // Main processing loop
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessBatchAsync(stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in worker");
            _readyTcs.TrySetException(ex);
            throw;
        }
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        // Queue subscription, channel setup, etc.
        await Task.Delay(500, ct);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Message processing logic
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
    }
}
```

### Simplified with Simple Mode

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Veggerby.Ignition;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSimpleIgnition(ignition => ignition
    .UseWorkerProfile() // 60s timeout, FailFast, Parallel, Tracing enabled
    .AddSignal("database", async ct => await db.ConnectAsync(ct))
    .AddSignal("queue", async ct => await queue.ConnectAsync(ct))
    .AddSignal("cache", async ct => await cache.ConnectAsync(ct)));

builder.Services.AddSingleton<MyWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MyWorker>());
builder.Services.AddIgnitionFor<MyWorker>(w => w.ReadyTask, name: "worker-ready");

// Add IgnitionHostedService to block startup
builder.Services.AddHostedService<IgnitionHostedService>();

var host = builder.Build();
await host.RunAsync();
```

### Key Characteristics

✅ **FailFast Policy**: Critical dependencies must succeed
✅ **Hard Global Timeout**: Forces cancellation on timeout
✅ **IHostedService Blocking**: Host doesn't reach "running" state until ready
✅ **Worker Readiness**: Coordinates with BackgroundService initialization
✅ **Clean Shutdown**: Triggers graceful shutdown on startup failures

### When to Use

- Background workers and message processors
- Scheduled job workers
- Event stream processors (Kafka, Event Hub)
- Services that require critical dependencies
- Long-running batch processors

### Kubernetes Integration

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: message-processor
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: worker
        image: my-worker:latest
        readinessProbe:
          exec:
            command: ["/bin/grpc_health_probe", "-addr=:8080"]
          initialDelaySeconds: 10
          periodSeconds: 10
        livenessProbe:
          exec:
            command: ["/bin/grpc_health_probe", "-addr=:8080"]
          initialDelaySeconds: 30
          periodSeconds: 30
```

---

## Recipe 3: Console Application

**Use Case**: CLI tools, migration runners, one-time jobs
**Policy**: FailFast (must complete successfully)
**Blocking**: Direct `await` before main logic

### Pattern

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Configure Ignition for CLI
        services.AddIgnition(options =>
        {
            options.Policy = IgnitionPolicy.FailFast;
            options.ExecutionMode = IgnitionExecutionMode.Sequential; // Ordered execution
            options.GlobalTimeout = TimeSpan.FromSeconds(15);
            options.EnableTracing = false; // CLI typically doesn't need tracing
        });

        // Register signals
        services.AddIgnitionSignal<ConfigLoadSignal>();
        services.AddIgnitionSignal<DatabaseConnectionSignal>();
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

try
{
    logger.LogInformation("Starting initialization...");
    
    await coordinator.WaitAllAsync();
    
    logger.LogInformation("Initialization complete");
    
    // Run main application logic
    await RunApplicationAsync(host.Services);
}
catch (AggregateException ex)
{
    logger.LogError("Initialization failed");
    foreach (var inner in ex.InnerExceptions)
    {
        logger.LogError(inner, "Error: {Message}", inner.Message);
    }
    Environment.Exit(1);
}

async Task RunApplicationAsync(IServiceProvider services)
{
    // Main CLI logic here
    logger.LogInformation("Application running...");
}
```

### Simplified with Simple Mode

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Veggerby.Ignition;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddSimpleIgnition(ignition => ignition
            .UseCliProfile() // 15s timeout, FailFast, Sequential, Tracing disabled
            .AddSignal("config", async ct => await LoadConfigAsync(ct))
            .AddSignal("database", async ct => await ConnectDatabaseAsync(ct)));
    })
    .Build();

await host.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

// Run main logic
await RunAsync();
```

### Key Characteristics

✅ **FailFast Policy**: Any failure stops execution
✅ **Sequential Execution**: Ordered, predictable startup
✅ **No Tracing**: Minimal overhead for CLI scenarios
✅ **Short Timeout**: CLI tools should fail fast

### When to Use

- Command-line tools
- Database migration runners
- Batch processing jobs
- One-time setup scripts

---

## Comparison Matrix

| Aspect | Web API | Worker | Console |
|--------|---------|--------|---------|
| **Default Policy** | BestEffort | FailFast | FailFast |
| **Typical Timeout** | 30s | 60s | 15s |
| **Execution Mode** | Parallel | Parallel | Sequential |
| **Cancellation** | Soft | Hard | Hard |
| **Tracing** | Enabled | Enabled | Disabled |
| **Health Checks** | Yes | Optional | No |
| **Blocking Pattern** | Before `app.Run()` | IHostedService | Direct await |
| **Partial Failures** | Tolerated | Not tolerated | Not tolerated |

---

## Advanced Patterns

### Staged Startup (Multi-Phase)

For complex applications with phased initialization:

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed;
});

// Stage 0: Infrastructure
builder.Services.AddIgnitionFromTaskWithStage("db", ct => db.ConnectAsync(ct), stage: 0);
builder.Services.AddIgnitionFromTaskWithStage("redis", ct => redis.ConnectAsync(ct), stage: 0);

// Stage 1: Services (runs after Stage 0 completes)
builder.Services.AddIgnitionFromTaskWithStage("cache-warmup", ct => cache.WarmAsync(ct), stage: 1);

// Stage 2: Workers (runs after Stage 1 completes)
builder.Services.AddIgnitionFromTaskWithStage("processor", ct => processor.StartAsync(ct), stage: 2);
```

### Dependency-Aware Execution (DAG)

For complex dependency graphs:

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
});

builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();

builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    graphBuilder.AddSignals(signals);
    graphBuilder.ApplyAttributeDependencies(); // Uses [SignalDependency] attributes
});
```

---

## Integration Package Recipes

### MySQL Database Readiness

**Package**: `Veggerby.Ignition.MySql`

#### Basic Connection Verification

```csharp
using Veggerby.Ignition.MySql;

builder.Services.AddIgnition();

// Simple ping verification
builder.Services.AddMySqlReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MySqlVerificationStrategy.Ping;
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### Table Existence Validation

```csharp
// Verify critical tables exist before startup
builder.Services.AddMySqlReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MySqlVerificationStrategy.TableExists;
        options.VerifyTables.Add("users");
        options.VerifyTables.Add("orders");
        options.VerifyTables.Add("products");
        options.FailOnMissingTables = true;
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### Custom Query Validation

```csharp
// Verify data availability with custom query
builder.Services.AddMySqlReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.TestQuery = "SELECT COUNT(*) FROM system_status WHERE ready = 1";
        options.ExpectedMinimumRows = 1;
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### Staged Execution with Testcontainers

```csharp
// Stage 0: Start MySQL container
var infrastructure = new InfrastructureManager();
builder.Services.AddSingleton(infrastructure);
builder.Services.AddIgnitionFromTaskWithStage(
    "mysql-container",
    async ct => await infrastructure.StartMySqlAsync(),
    stage: 0);

// Stage 1: Verify MySQL readiness
builder.Services.AddMySqlReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().MySqlConnectionString,
    options =>
    {
        options.Stage = 1;
        options.VerificationStrategy = MySqlVerificationStrategy.TableExists;
        options.VerifyTables.Add("migrations");
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### With Retry Configuration

```csharp
builder.Services.AddMySqlReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MySqlVerificationStrategy.SimpleQuery;
        options.MaxRetries = 10;
        options.RetryDelay = TimeSpan.FromMilliseconds(500);
        options.Timeout = TimeSpan.FromSeconds(60);
    });
```

---

### MariaDB Database Readiness

**Package**: `Veggerby.Ignition.MariaDb`

#### Basic Connection Verification

```csharp
using Veggerby.Ignition.MariaDb;

builder.Services.AddIgnition();

// Simple ping verification
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.Ping;
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### Table Existence Validation

```csharp
// Verify critical tables exist before startup
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.TableExists;
        options.VerifyTables.Add("users");
        options.VerifyTables.Add("orders");
        options.VerifyTables.Add("products");
        options.FailOnMissingTables = true;
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### Custom Query Validation

```csharp
// Verify data availability with custom query
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.TestQuery = "SELECT COUNT(*) FROM system_status WHERE ready = 1";
        options.ExpectedMinimumRows = 1;
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### Staged Execution with Testcontainers

```csharp
// Stage 0: Start MariaDB container
var infrastructure = new InfrastructureManager();
builder.Services.AddSingleton(infrastructure);
builder.Services.AddIgnitionFromTaskWithStage(
    "mariadb-container",
    async ct => await infrastructure.StartMariaDbAsync(),
    stage: 0);

// Stage 1: Verify MariaDB readiness
builder.Services.AddMariaDbReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().MariaDbConnectionString,
    options =>
    {
        options.Stage = 1;
        options.VerificationStrategy = MariaDbVerificationStrategy.TableExists;
        options.VerifyTables.Add("migrations");
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

#### With Retry Configuration

```csharp
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.SimpleQuery;
        options.MaxRetries = 12; // More retries for cloud environments
        options.RetryDelay = TimeSpan.FromMilliseconds(500); // Exponential backoff
        options.Timeout = TimeSpan.FromSeconds(60);
    });
```

---

## Production Checklist

### Web API
- [ ] Use BestEffort policy
- [ ] Set GlobalTimeout to 30s
- [ ] Enable Activity tracing
- [ ] Configure health checks with readiness probe
- [ ] Log warnings for failed signals
- [ ] Test graceful degradation

### Worker
- [ ] Use FailFast policy
- [ ] Set GlobalTimeout to 60s or more
- [ ] Register IgnitionHostedService
- [ ] Use TaskCompletionSource for worker readiness
- [ ] Configure hard timeout cancellation
- [ ] Test startup failure shutdown

### Console
- [ ] Use FailFast policy
- [ ] Use Sequential execution for predictable order
- [ ] Set short timeout (15s)
- [ ] Exit with error code on failure
- [ ] Disable tracing for performance

---

## Apache Pulsar Integration (DotPulsar & Pulsar.Client)

### Overview

Two Apache Pulsar integration packages are available:

- **`Veggerby.Ignition.Pulsar.DotPulsar`** - Uses the official Apache DotPulsar client
- **`Veggerby.Ignition.Pulsar.Client`** - Uses the Pulsar.Client library

Both packages provide identical verification strategies and configuration options. Choose based on your existing Pulsar client preference or organizational standards.

### Installation

```bash
# Option 1: DotPulsar (official Apache client)
dotnet add package Veggerby.Ignition.Pulsar.DotPulsar

# Option 2: Pulsar.Client
dotnet add package Veggerby.Ignition.Pulsar.Client
```

### Verification Strategies

| Strategy | Description | Configuration Required |
|----------|-------------|------------------------|
| `ClusterHealth` | Validates broker connectivity (default) | None |
| `TopicMetadata` | Verifies specified topics exist | `VerifyTopics` |
| `ProducerTest` | Produces test message to verify producer connectivity | `VerifyTopics` (at least one) |
| `SubscriptionCheck` | Verifies subscription exists for a topic | `VerifySubscription`, `SubscriptionTopic` |
| `AdminApiCheck` | Validates broker health via Admin API | `AdminServiceUrl` |

### Basic Cluster Connectivity

```csharp
using Veggerby.Ignition.Pulsar.DotPulsar; // or Veggerby.Ignition.Pulsar.Client

// Simple connection verification
builder.Services.AddPulsarReadiness("pulsar://localhost:6650");
```

### Topic Verification

```csharp
// Verify topics exist (strict mode)
builder.Services.AddPulsarReadiness(
    "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
        options.WithTopic("persistent://public/default/orders");
        options.WithTopic("persistent://public/default/payments");
        options.WithTopic("persistent://public/default/inventory");
        options.FailOnMissingTopics = true; // Fail if any topic is missing
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Producer Test

```csharp
// Test message production (end-to-end verification)
builder.Services.AddPulsarReadiness(
    "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.ProducerTest;
        options.WithTopic("persistent://public/default/test-topic");
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Subscription Verification

```csharp
// Verify consumer subscription exists
builder.Services.AddPulsarReadiness(
    "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.SubscriptionCheck;
        options.SubscriptionTopic = "persistent://public/default/orders";
        options.VerifySubscription = "order-processor-subscription";
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Admin API Health Check

```csharp
// Check broker health via Admin API
builder.Services.AddPulsarReadiness(
    "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.AdminApiCheck;
        options.AdminServiceUrl = "http://localhost:8080";
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Staged Execution with Testcontainers

```csharp
// Stage 0: Start Pulsar container
var infrastructure = new InfrastructureManager();
builder.Services.AddSingleton(infrastructure);
builder.Services.AddIgnitionFromTaskWithStage(
    "pulsar-container",
    async ct => await infrastructure.StartPulsarAsync(),
    stage: 0);

// Stage 3: Verify Pulsar readiness
builder.Services.AddPulsarReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().PulsarServiceUrl,
    options =>
    {
        options.Stage = 3;
        options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
        options.WithTopic("persistent://public/default/orders");
        options.WithTopic("persistent://public/default/events");
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### With Retry Configuration

```csharp
builder.Services.AddPulsarReadiness(
    "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.ClusterHealth;
        options.MaxRetries = 5;
        options.RetryDelay = TimeSpan.FromMilliseconds(200);
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Multi-Topic Verification with Tolerance

```csharp
// Verify topics but don't fail if some are missing
builder.Services.AddPulsarReadiness(
    "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
        options.WithTopic("persistent://public/default/orders");
        options.WithTopic("persistent://public/default/inventory");
        options.WithTopic("persistent://public/default/shipping");
        options.FailOnMissingTopics = false; // Log warnings instead of failing
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Production Readiness Pattern

```csharp
// Combine multiple verification strategies
builder.Services.AddPulsarReadiness(
    sp => sp.GetRequiredService<IConfiguration>()["Pulsar:ServiceUrl"] 
        ?? "pulsar://localhost:6650",
    options =>
    {
        options.VerificationStrategy = PulsarVerificationStrategy.ProducerTest;
        options.WithTopic("persistent://public/default/health-check");
        options.MaxRetries = 8;
        options.RetryDelay = TimeSpan.FromMilliseconds(500);
        options.Timeout = TimeSpan.FromSeconds(30);
        options.Stage = 3; // After infrastructure setup
    });
```

### Topic Name Format

Pulsar topics use a hierarchical naming structure:

```
{persistent|non-persistent}://{tenant}/{namespace}/{topic}
```

Simple topic names are automatically normalized to the default namespace:

```csharp
options.WithTopic("orders");
// Becomes: persistent://public/default/orders

options.WithTopic("persistent://my-tenant/my-namespace/my-topic");
// Used as-is (fully qualified)
```

### DotPulsar vs Pulsar.Client

Both packages provide identical configuration and functionality. Choose based on:

| Factor | DotPulsar | Pulsar.Client |
|--------|-----------|---------------|
| **Official Support** | Apache Foundation official client | Community-maintained |
| **Package Size** | ~500KB | Larger footprint |
| **API Style** | Modern async/await patterns | Traditional .NET style |
| **Recommendation** | ✅ Preferred for new projects | Use if already in your stack |

---

## See Also

- **[Getting Started Guide](getting-started.md)** - Basic concepts and first signal
- **[Policies Documentation](policies.md)** - FailFast, BestEffort, ContinueOnTimeout
- **[Observability Guide](observability.md)** - Logging, tracing, health checks
- **[WebApi Sample](../samples/WebApi/)** - Full ASP.NET Core example
- **[Worker Sample](../samples/Worker/)** - Full Generic Host example
- **[Simple Sample](../samples/Simple/)** - Console application example
