# Ignition Cookbook

**Battle-tested recipes for real startup problems.** This cookbook provides documented, production-ready patterns that solve common application startup challenges using Veggerby.Ignition.

Each recipe includes:
- **Problem statement**: The challenge being solved
- **Configuration pattern**: Copy-paste-ready code
- **Expected behavior**: What happens at runtime
- **When to use**: Guidance on applicability

---

## Table of Contents

1. [External Dependency Readiness (Multi-Stage Warmup)](#recipe-1-external-dependency-readiness-multi-stage-warmup)
2. [Cache Warmup Strategies](#recipe-2-cache-warmup-strategies)
3. [Background Worker Orchestration](#recipe-3-background-worker-orchestration)
4. [Kubernetes Readiness & Liveness Probes](#recipe-4-kubernetes-readiness--liveness-probes)
5. [Multi-Stage Startup Pipelines](#recipe-5-multi-stage-startup-pipelines)
6. [Recording/Replay for Production Diagnosis](#recipe-6-recordingreplay-for-production-diagnosis)
7. [OpenTelemetry Metrics Integration](#recipe-7-opentelemetry-metrics-integration)
8. [DAG vs Stages: Choosing Your Execution Model](#recipe-8-dag-vs-stages-choosing-your-execution-model)
9. [Graceful Degradation Patterns](#recipe-9-graceful-degradation-patterns)
10. [Testing Startup Sequences](#recipe-10-testing-startup-sequences)

---

## Recipe 1: External Dependency Readiness (Multi-Stage Warmup)

### Problem Statement

Your application depends on Redis, SQL Server, and Elasticsearch. You want to:
- Connect to infrastructure first (databases)
- Warm caches only after databases are ready
- Build search indexes only after caches are populated
- Ensure clear ordering without manual orchestration

### Configuration Pattern

```csharp
using Veggerby.Ignition;

var builder = WebApplication.CreateBuilder(args);

// Configure Staged execution for sequential phases
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed;
    options.GlobalTimeout = TimeSpan.FromSeconds(60);
    options.EnableTracing = true;
    options.MaxDegreeOfParallelism = 4; // Limit concurrency within each stage
});

// Stage 0: Infrastructure (databases connect in parallel)
builder.Services.AddIgnitionFromTaskWithStage(
    "sql-connection",
    async ct =>
    {
        var db = builder.Services.BuildServiceProvider()
            .GetRequiredService<SqlConnection>();
        await db.OpenAsync(ct);
        Console.WriteLine("SQL connected");
    },
    stage: 0,
    timeout: TimeSpan.FromSeconds(15));

builder.Services.AddIgnitionFromTaskWithStage(
    "redis-connection",
    async ct =>
    {
        var redis = builder.Services.BuildServiceProvider()
            .GetRequiredService<IConnectionMultiplexer>();
        await redis.GetDatabase().PingAsync();
        Console.WriteLine("Redis connected");
    },
    stage: 0,
    timeout: TimeSpan.FromSeconds(10));

// Stage 1: Cache warmup (executes after Stage 0 completes)
builder.Services.AddIgnitionFromTaskWithStage(
    "user-cache-warmup",
    async ct =>
    {
        var cache = builder.Services.BuildServiceProvider()
            .GetRequiredService<IUserCache>();
        await cache.WarmupAsync(ct);
        Console.WriteLine("User cache warmed");
    },
    stage: 1,
    timeout: TimeSpan.FromSeconds(20));

builder.Services.AddIgnitionFromTaskWithStage(
    "product-cache-warmup",
    async ct =>
    {
        var cache = builder.Services.BuildServiceProvider()
            .GetRequiredService<IProductCache>();
        await cache.WarmupAsync(ct);
        Console.WriteLine("Product cache warmed");
    },
    stage: 1,
    timeout: TimeSpan.FromSeconds(20));

// Stage 2: Search index building (executes after Stage 1 completes)
builder.Services.AddIgnitionFromTaskWithStage(
    "elasticsearch-index",
    async ct =>
    {
        var elastic = builder.Services.BuildServiceProvider()
            .GetRequiredService<IElasticClient>();
        await elastic.Indices.RefreshAsync(ct: ct);
        Console.WriteLine("Elasticsearch index refreshed");
    },
    stage: 2,
    timeout: TimeSpan.FromSeconds(30));

var app = builder.Build();

// Wait for all stages to complete
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

var result = await coordinator.GetResultAsync();
if (result.HasStageResults)
{
    foreach (var stage in result.StageResults)
    {
        Console.WriteLine($"Stage {stage.StageNumber}: {stage.SucceededCount}/{stage.TotalSignals} succeeded in {stage.Duration.TotalMilliseconds:F0}ms");
    }
}

app.Run();
```

### Expected Behavior

1. **Stage 0**: SQL and Redis connect in parallel (both start simultaneously)
2. **Stage 1 waits**: Does not start until Stage 0 fully succeeds
3. **Stage 1**: User cache and product cache warm in parallel
4. **Stage 2 waits**: Does not start until Stage 1 fully succeeds
5. **Stage 2**: Elasticsearch index builds

If any signal in a stage fails and `StagePolicy.AllMustSucceed` is configured, subsequent stages are skipped.

### When to Use

- **Clear sequential dependencies**: "X must happen before Y"
- **Infrastructure layering**: databases → caches → workers
- **Simpler than full DAG**: You don't need complex interdependencies, just phases
- **Predictable warmup sequences**: Always the same order every time

**Don't use** when signals within a "stage" have complex dependencies on each other—use DAG mode instead.

---

## Recipe 2: Cache Warmup Strategies

### Problem Statement

You have multiple caches (Redis, in-memory, distributed) that need to be pre-populated at startup. You want to:
- Warm critical caches first (fail-fast if they fail)
- Tolerate failures in non-critical caches (best-effort)
- Limit concurrency to avoid overwhelming data sources
- Log which caches succeeded/failed for diagnostics

### Configuration Pattern

#### Strategy A: Critical + Optional Caches (BestEffort)

```csharp
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort; // Continue despite failures
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.MaxDegreeOfParallelism = 3; // Limit concurrent warmups
    options.GlobalTimeout = TimeSpan.FromSeconds(45);
});

// Critical cache (short timeout)
builder.Services.AddIgnitionFromTask(
    "user-cache-warmup",
    async ct =>
    {
        var cache = sp.GetRequiredService<IUserCache>();
        await cache.LoadActiveUsersAsync(ct); // Must succeed
    },
    timeout: TimeSpan.FromSeconds(15));

// Optional cache (longer timeout, tolerates failure)
builder.Services.AddIgnitionFromTask(
    "product-recommendations-cache",
    async ct =>
    {
        var cache = sp.GetRequiredService<IRecommendationCache>();
        await cache.WarmTopProductsAsync(ct); // Nice to have
    },
    timeout: TimeSpan.FromSeconds(30));

// Optional cache (analytics data)
builder.Services.AddIgnitionFromTask(
    "analytics-cache-warmup",
    async ct =>
    {
        var cache = sp.GetRequiredService<IAnalyticsCache>();
        await cache.LoadDashboardDataAsync(ct); // Nice to have
    },
    timeout: TimeSpan.FromSeconds(30));
```

After startup, inspect which caches succeeded:

```csharp
await coordinator.WaitAllAsync();
var result = await coordinator.GetResultAsync();

var failed = result.Results.Where(r => r.Status != IgnitionSignalStatus.Succeeded);
if (failed.Any())
{
    logger.LogWarning("Some caches failed to warm: {Names}",
        string.Join(", ", failed.Select(r => r.Name)));
}
```

#### Strategy B: Staged Critical-First Warmup

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed; // Stage 0 must succeed
});

// Stage 0: Critical caches (must succeed)
builder.Services.AddIgnitionFromTaskWithStage(
    "user-cache",
    ct => userCache.WarmAsync(ct),
    stage: 0,
    timeout: TimeSpan.FromSeconds(10));

// Stage 1: Optional caches (run even if Stage 0 has warnings)
builder.Services.AddIgnitionFromTaskWithStage(
    "product-cache",
    ct => productCache.WarmAsync(ct),
    stage: 1,
    timeout: TimeSpan.FromSeconds(20));

builder.Services.AddIgnitionFromTaskWithStage(
    "analytics-cache",
    ct => analyticsCache.WarmAsync(ct),
    stage: 1,
    timeout: TimeSpan.FromSeconds(20));
```

### Expected Behavior

**Strategy A (BestEffort)**: All caches warm in parallel (up to 3 concurrent). Application continues even if optional caches fail. Logs clearly indicate which succeeded.

**Strategy B (Staged)**: Critical caches warm first. If they succeed, optional caches warm in parallel. If critical cache fails, application startup aborts (controlled failure).

### When to Use

- **BestEffort**: When most caches are "nice to have" and you prefer availability over completeness
- **Staged**: When critical caches must succeed (authentication, authorization) but optional caches can fail
- **MaxDegreeOfParallelism**: When warmup queries could overwhelm your database (limit concurrent reads)

---

## Recipe 3: Background Worker Orchestration

### Problem Statement

Your application has multiple `BackgroundService` workers (message consumers, job processors, health monitors). You want to:
- Wait for all workers to signal they're ready before accepting traffic
- Coordinate startup order (e.g., health monitor starts after consumers)
- Handle worker initialization failures gracefully

### Configuration Pattern

#### Pattern A: Using TaskCompletionSource in BackgroundService

**Note**: This pattern uses Veggerby.Ignition's `Ignited()` and `IgnitionFailed()` extension methods for `TaskCompletionSource`. These provide semantic sugar for `SetResult()` and `SetException()`.

```csharp
public class MessageConsumerWorker : BackgroundService
{
    private readonly IMessageQueue _queue;
    private readonly TaskCompletionSource _readyTcs = new();
    private readonly ILogger<MessageConsumerWorker> _logger;

    public Task ReadyTask => _readyTcs.Task; // Exposed for Ignition coordination

    public MessageConsumerWorker(IMessageQueue queue, ILogger<MessageConsumerWorker> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Connect to message queue
            await _queue.ConnectAsync(stoppingToken);
            await _queue.SubscribeAsync("orders", stoppingToken);

            _logger.LogInformation("Message consumer ready");
            _readyTcs.Ignited(); // Veggerby.Ignition extension: marks ready

            // Continue processing messages
            await ProcessMessagesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start message consumer");
            _readyTcs.IgnitionFailed(ex); // Veggerby.Ignition extension: marks failed
            throw;
        }
    }

    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        await foreach (var message in _queue.ReadAllAsync(ct))
        {
            // Process messages...
        }
    }
}

// Registration
builder.Services.AddHostedService<MessageConsumerWorker>();
builder.Services.AddIgnitionFor<MessageConsumerWorker>(
    w => w.ReadyTask,
    name: "message-consumer");

builder.Services.AddHostedService<HealthMonitorWorker>();
builder.Services.AddIgnitionFor<HealthMonitorWorker>(
    w => w.ReadyTask,
    name: "health-monitor");
```

#### Pattern B: Dependency-Aware Worker Startup (DAG)

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});

// Define dependency graph: health monitor depends on message consumer
builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    var consumer = signals.First(s => s.Name == "message-consumer");
    var monitor = signals.First(s => s.Name == "health-monitor");

    graphBuilder.AddSignals(new[] { consumer, monitor });
    graphBuilder.DependsOn(monitor, consumer); // Monitor depends on consumer
});

var app = builder.Build();

// Wait for workers to be ready
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

app.Run(); // Workers are ready, start accepting traffic
```

### Expected Behavior

1. `BackgroundService.StartAsync` triggers immediately (hosted services start)
2. Each worker signals readiness via `TaskCompletionSource.Ignited()` (Veggerby.Ignition extension)
3. Ignition coordinator waits for all workers to signal ready
4. If DAG mode: workers start in dependency order (consumer first, then monitor)
5. Application traffic starts only after all workers are ready

### When to Use

- **Long-running services**: Background message processors, scheduled jobs, health monitors
- **Readiness gates**: Don't accept traffic until workers are subscribed/connected
- **Ordered startup**: Use DAG when workers depend on each other (e.g., metrics collector depends on data ingestion worker)

---

## Recipe 4: Kubernetes Readiness & Liveness Probes

### Problem Statement

Your application runs in Kubernetes. You need:
- **Readiness probe**: Indicates the app is ready to accept traffic (ignition passed)
- **Liveness probe**: Indicates the app is healthy (not deadlocked or crashed)
- Clear distinction between startup readiness and ongoing health

### Configuration Pattern

```csharp
using Veggerby.Ignition;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add ignition (automatically registers 'ignition-readiness' health check)
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort;
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.EnableTracing = true;
});

// Register startup signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<CacheWarmupSignal>();
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle("https://api.external.com/health"));

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready", "live" })
    .AddCheck<CacheHealthCheck>("cache", tags: new[] { "live" });
// Note: 'ignition-readiness' is auto-registered by AddIgnition

var app = builder.Build();

// Map health check endpoints for Kubernetes probes
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready") || check.Name == "ignition-readiness"
});

// Wait for ignition before starting
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

app.Run();
```

#### Kubernetes Deployment YAML

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
spec:
  replicas: 3
  template:
    spec:
      containers:
      - name: myapp
        image: myapp:1.0.0
        ports:
        - containerPort: 8080
        
        # Startup probe: Allow up to 60 seconds for ignition to complete
        startupProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 12  # 12 * 5s = 60s max startup time
        
        # Readiness probe: Check if app can accept traffic
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 10
          timeoutSeconds: 3
          failureThreshold: 3
        
        # Liveness probe: Check if app is still responsive
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
          timeoutSeconds: 5
          failureThreshold: 3
```

### Expected Behavior

1. **Startup**: Pod starts, startup probe checks `/health/ready` every 5 seconds
2. **Ignition running**: `ignition-readiness` returns `Unhealthy` (ignition in progress)
3. **Ignition completes**: `ignition-readiness` returns `Healthy` or `Degraded`
4. **Readiness succeeds**: Pod added to service load balancer
5. **Ongoing liveness**: `/health/live` monitors runtime health (database connectivity, etc.)

**Key distinction**:
- `/health/ready`: Includes `ignition-readiness` (startup gate + ongoing readiness checks)
- `/health/live`: Excludes ignition (only runtime health, not startup)

### When to Use

- **All Kubernetes deployments**: Leverage native Kubernetes health probes
- **Zero-downtime deployments**: Ensure new pods signal readiness before receiving traffic
- **Graceful degradation**: Use `BestEffort` policy so ignition returns `Degraded` for partial failures (still serves traffic)

---

## Recipe 5: Multi-Stage Startup Pipelines

### Problem Statement

Your application has a complex startup sequence:
1. Connect to infrastructure (databases, queues)
2. Load configuration from remote sources (Consul, Azure App Config)
3. Warm caches and indexes
4. Start background workers
5. Enable metrics/telemetry exporters

You want clear separation between stages with policy control at each stage.

### Configuration Pattern

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed; // Strict: each stage must succeed
    options.GlobalTimeout = TimeSpan.FromSeconds(120);
    options.MaxDegreeOfParallelism = 4;
    options.EnableTracing = true;
});

// ========== Stage 0: Infrastructure ===========
builder.Services.AddIgnitionFromTaskWithStage(
    "postgres-connection",
    ct => postgresClient.ConnectAsync(ct),
    stage: 0,
    timeout: TimeSpan.FromSeconds(15));

builder.Services.AddIgnitionFromTaskWithStage(
    "rabbitmq-connection",
    ct => rabbitMqClient.ConnectAsync(ct),
    stage: 0,
    timeout: TimeSpan.FromSeconds(15));

// ========== Stage 1: Configuration ===========
builder.Services.AddIgnitionFromTaskWithStage(
    "remote-config-load",
    async ct =>
    {
        var configClient = sp.GetRequiredService<IConfigurationClient>();
        await configClient.LoadAsync(ct);
        Console.WriteLine("Remote config loaded");
    },
    stage: 1,
    timeout: TimeSpan.FromSeconds(10));

// ========== Stage 2: Data Warmup ===========
builder.Services.AddIgnitionFromTaskWithStage(
    "user-cache-warmup",
    ct => userCache.WarmAsync(ct),
    stage: 2,
    timeout: TimeSpan.FromSeconds(20));

builder.Services.AddIgnitionFromTaskWithStage(
    "product-index-build",
    ct => searchIndex.BuildAsync(ct),
    stage: 2,
    timeout: TimeSpan.FromSeconds(30));

// ========== Stage 3: Workers ===========
builder.Services.AddIgnitionFromTaskWithStage(
    "order-processor-ready",
    ct => orderProcessor.InitializeAsync(ct),
    stage: 3,
    timeout: TimeSpan.FromSeconds(10));

builder.Services.AddIgnitionFromTaskWithStage(
    "email-worker-ready",
    ct => emailWorker.InitializeAsync(ct),
    stage: 3,
    timeout: TimeSpan.FromSeconds(10));

// ========== Stage 4: Telemetry ===========
builder.Services.AddIgnitionFromTaskWithStage(
    "metrics-exporter-ready",
    ct => metricsExporter.StartAsync(ct),
    stage: 4,
    timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

var result = await coordinator.GetResultAsync();

// Log stage-by-stage results
if (result.HasStageResults)
{
    foreach (var stage in result.StageResults)
    {
        var stageName = stage.StageNumber switch
        {
            0 => "Infrastructure",
            1 => "Configuration",
            2 => "Data Warmup",
            3 => "Workers",
            4 => "Telemetry",
            _ => $"Stage {stage.StageNumber}"
        };

        logger.LogInformation("{StageName}: {Succeeded}/{Total} in {Duration}ms",
            stageName, stage.SucceededCount, stage.TotalSignals, stage.Duration.TotalMilliseconds);
    }
}

app.Run();
```

### Expected Behavior

Execution order (sequential across stages, parallel within stages):

```
[Stage 0: Infrastructure] 
  postgres-connection ━━━━━━━━━━━━━━━┓
  rabbitmq-connection ━━━━━━━━━━━━━━━┛
  → Stage 0 completes

[Stage 1: Configuration]
  remote-config-load ━━━━━━━━
  → Stage 1 completes

[Stage 2: Data Warmup]
  user-cache-warmup ━━━━━━━━━━━━━━━━━┓
  product-index-build ━━━━━━━━━━━━━━┛
  → Stage 2 completes

[Stage 3: Workers]
  order-processor-ready ━━━━━━━━┓
  email-worker-ready ━━━━━━━━━━┛
  → Stage 3 completes

[Stage 4: Telemetry]
  metrics-exporter-ready ━━━━
  → Stage 4 completes

Application ready!
```

If any signal in a stage fails, subsequent stages are skipped.

### When to Use

- **Complex multi-tier applications**: Microservices with many initialization steps
- **Clear phase boundaries**: Each stage has a distinct purpose (infra → config → data → workers)
- **Fail-fast on critical stages**: Use `AllMustSucceed` so failures abort cleanly
- **Simpler than DAG**: No complex cross-dependencies; just ordered phases

---

## Recipe 6: Recording/Replay for Production Diagnosis

### Problem Statement

Your application is slow to start in production. You want to:
- Record startup timing with full signal execution details
- Compare production recordings to baseline (detect regressions)
- Simulate "what if" scenarios offline (e.g., "what if cache warmup times out?")
- Identify the critical path (slowest chain blocking total duration)

### Configuration Pattern

#### Step 1: Record Production Startup

```csharp
var app = builder.Build();

var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

// Export recording with metadata
var result = await coordinator.GetResultAsync();
var options = app.Services.GetRequiredService<IOptions<IgnitionOptions>>().Value;

var recording = result.ExportRecording(
    options: options,
    metadata: new Dictionary<string, string>
    {
        ["environment"] = "production",
        ["version"] = "1.5.2",
        ["hostname"] = Environment.MachineName,
        ["pod"] = Environment.GetEnvironmentVariable("POD_NAME") ?? "unknown"
    });

var json = result.ExportRecordingJson(options: options, indented: true);

// Save to blob storage or logging system
await File.WriteAllTextAsync($"/logs/ignition-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json", json, Encoding.UTF8);
Console.WriteLine($"Recording saved: {recording.RecordingId}");
```

#### Step 2: Analyze Recording Offline

```csharp
// Load recording from file or blob storage
var json = await File.ReadAllTextAsync("ignition-20250109-103000.json", Encoding.UTF8);
var recording = IgnitionRecording.FromJson(json);
var replayer = new IgnitionReplayer(recording);

// Validate recording for consistency issues
var validation = replayer.Validate();
if (!validation.IsValid)
{
    Console.WriteLine($"⚠️ Found {validation.ErrorCount} errors:");
    foreach (var issue in validation.Issues.Where(i => i.Severity == "Error"))
    {
        Console.WriteLine($"  [{issue.Code}] {issue.Message}");
    }
}

// Identify slow signals (over 200ms)
var slowSignals = replayer.IdentifySlowSignals(minDurationMs: 200);
Console.WriteLine($"\n🐌 Slow signals ({slowSignals.Count}):");
foreach (var slow in slowSignals.OrderByDescending(s => s.DurationMs))
{
    Console.WriteLine($"  {slow.SignalName}: {slow.DurationMs:F0}ms");
}

// Find critical path (signals blocking total duration)
var criticalPath = replayer.IdentifyCriticalPath();
Console.WriteLine($"\n🔴 Critical path:");
Console.WriteLine($"  {string.Join(" → ", criticalPath.Select(s => $"{s.SignalName} ({s.DurationMs:F0}ms)"))}");

// Get execution order
var order = replayer.GetExecutionOrder();
Console.WriteLine($"\n📋 Execution order: {string.Join(", ", order)}");
```

#### Step 3: Compare Recordings (Regression Detection)

```csharp
// Load baseline (previous production run)
var baseline = IgnitionRecording.FromJson(
    await File.ReadAllTextAsync("baseline-prod-20250108.json", Encoding.UTF8));

// Load current run
var current = IgnitionRecording.FromJson(
    await File.ReadAllTextAsync("current-prod-20250109.json", Encoding.UTF8));

var baselineReplayer = new IgnitionReplayer(baseline);
var comparison = baselineReplayer.CompareTo(current);

Console.WriteLine($"📊 Comparison: Baseline vs Current");
Console.WriteLine($"  Total duration: {comparison.DurationDifferenceMs:+0;-0}ms ({comparison.DurationChangePercent:+0.0;-0.0}%)");

// Detect regressions (signals that got slower)
var regressions = comparison.SignalComparisons
    .Where(c => c.DurationChangePercent > 20)
    .OrderByDescending(c => c.DurationChangePercent);

if (regressions.Any())
{
    Console.WriteLine($"\n⚠️ Regressions detected:");
    foreach (var regression in regressions)
    {
        Console.WriteLine($"  {regression.SignalName}: " +
            $"+{regression.DurationDifferenceMs:F0}ms ({regression.DurationChangePercent:+0}%)");
    }
}

// Detect status changes
var statusChanges = comparison.SignalComparisons.Where(c => c.StatusChanged);
if (statusChanges.Any())
{
    Console.WriteLine($"\n🔄 Status changes:");
    foreach (var change in statusChanges)
    {
        Console.WriteLine($"  {change.SignalName}: {change.Status1} → {change.Status2}");
    }
}
```

#### Step 4: What-If Simulations

```csharp
var replayer = new IgnitionReplayer(recording);

// Simulate earlier timeout for a slow signal
var timeoutSim = replayer.SimulateEarlierTimeout(
    signalName: "elasticsearch-index-build",
    newTimeoutMs: 5000); // Simulate 5s timeout instead of actual 30s

Console.WriteLine($"\n🧪 Simulating timeout at 5s for 'elasticsearch-index-build':");
Console.WriteLine($"  Affected signals: {string.Join(", ", timeoutSim.AffectedSignals)}");
Console.WriteLine($"  Total duration: {timeoutSim.TotalDurationMs:F0}ms");

// Simulate failure
var failureSim = replayer.SimulateFailure("redis-connection");
Console.WriteLine($"\n💥 Simulating failure for 'redis-connection':");
Console.WriteLine($"  Affected signals: {string.Join(", ", failureSim.AffectedSignals)}");

foreach (var signal in failureSim.SimulatedSignals.Where(s => s.Status != "Succeeded"))
{
    Console.WriteLine($"  {signal.SignalName}: {signal.Status}");
}
```

### Expected Behavior

1. **Recording**: Every startup execution produces a JSON recording with full timing and dependency data
2. **Comparison**: Automated regression detection between releases or environments
3. **Simulation**: Offline what-if analysis without re-running production systems
4. **Critical Path Analysis**: Identifies the slowest chain of dependencies

### When to Use

- **Production performance tuning**: Record slow startups, analyze offline
- **CI regression detection**: Compare every build's startup to baseline; fail if >20% slower
- **Capacity planning**: Simulate changes to timeouts or failures to understand impact
- **Post-incident analysis**: Replay recorded failures to understand dependency cascades

---

## Recipe 7: OpenTelemetry Metrics Integration

### Problem Statement

You want to emit startup metrics to OpenTelemetry for monitoring and alerting:
- Signal-level duration histograms
- Status counters (succeeded/failed/timed out)
- Total startup duration
- Integration with existing OTEL infrastructure

### Configuration Pattern

#### Step 1: Implement IIgnitionMetrics for OpenTelemetry

```csharp
using System.Diagnostics.Metrics;
using Veggerby.Ignition;

public sealed class OpenTelemetryIgnitionMetrics : IIgnitionMetrics
{
    private readonly Histogram<double> _signalDuration;
    private readonly Counter<int> _signalStatus;
    private readonly Histogram<double> _totalDuration;

    public OpenTelemetryIgnitionMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Veggerby.Ignition");

        _signalDuration = meter.CreateHistogram<double>(
            "ignition.signal.duration",
            unit: "ms",
            description: "Duration of individual ignition signals");

        _signalStatus = meter.CreateCounter<int>(
            "ignition.signal.status",
            unit: "{signal}",
            description: "Count of signals by status");

        _totalDuration = meter.CreateHistogram<double>(
            "ignition.total.duration",
            unit: "ms",
            description: "Total ignition coordinator duration");
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("signal.name", name));
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatus.Add(1,
            new KeyValuePair<string, object?>("signal.name", name),
            new KeyValuePair<string, object?>("status", status.ToString()));
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Record(duration.TotalMilliseconds);
    }
}
```

#### Step 2: Register with Ignition and OTEL

```csharp
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Veggerby.Ignition"); // Register Ignition meter
        metrics.AddPrometheusExporter(); // Export to Prometheus
        // Or use OTLP exporter for other backends
        // metrics.AddOtlpExporter(options => { ... });
    });

// Configure Ignition with OpenTelemetry metrics
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.EnableTracing = true; // Also emit Activity traces
});

// Register the metrics adapter
builder.Services.AddIgnitionMetrics<OpenTelemetryIgnitionMetrics>();

// Register signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<CacheWarmupSignal>();

var app = builder.Build();

// Expose Prometheus metrics endpoint
app.MapPrometheusScrapingEndpoint(); // Typically /metrics

var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

app.Run();
```

#### Step 3: Query Metrics (Prometheus/Grafana)

**Query 1: Average signal duration by name**
```promql
rate(ignition_signal_duration_sum[5m]) / rate(ignition_signal_duration_count[5m])
```

**Query 2: Count of failed signals**
```promql
sum by (signal_name) (ignition_signal_status{status="Failed"})
```

**Query 3: 95th percentile startup duration**
```promql
histogram_quantile(0.95, rate(ignition_total_duration_bucket[10m]))
```

### Expected Behavior

1. **Automatic recording**: Metrics emitted every startup without manual instrumentation
2. **OTEL integration**: Works with any OpenTelemetry-compatible backend (Prometheus, Jaeger, Datadog, etc.)
3. **Zero code changes after setup**: Add new signals, metrics are automatically recorded
4. **Alerting**: Create alerts on slow signals or high failure rates

### When to Use

- **Production monitoring**: Track startup performance over time
- **SLO tracking**: Alert when startup duration exceeds SLO (e.g., p95 > 10s)
- **Service mesh integration**: Combine with Istio/Linkerd metrics for full observability
- **Multi-environment comparison**: Compare startup metrics between dev/staging/prod

**Alternative**: Use `result.ExportTimeline()` for one-off analysis; use metrics for continuous monitoring.

---

## Recipe 8: DAG vs Stages: Choosing Your Execution Model

### Problem Statement

You're unsure whether to use **Dependency-Aware (DAG)** execution or **Staged** execution. Both handle ordering, but they solve different problems.

### Decision Matrix

| Criterion | Use DAG | Use Stages |
|-----------|---------|------------|
| **Dependencies are complex** (A depends on B and C; D depends on B) | ✅ Yes | ❌ No |
| **Dependencies are simple** (just sequential phases) | ⚠️ Overkill | ✅ Yes |
| **Parallel execution within "layers"** | ✅ Automatic | ✅ Within stage |
| **Need cycle detection** | ✅ Built-in | ❌ N/A |
| **Want policy control per phase** | ❌ No | ✅ Yes (StagePolicy) |
| **Failure should skip dependents** | ✅ Automatic | ⚠️ Manual |
| **Clear conceptual "stages"** (infra → config → workers) | ⚠️ Works but awkward | ✅ Perfect fit |
| **Dynamic dependencies** (determined at runtime) | ✅ Flexible | ❌ Static stages |

### Example: When DAG is Better

**Scenario**: Cache depends on database and config. Search index depends on cache. Worker depends on search index.

```
database ────┐
             ├─→ cache ─→ search-index ─→ worker
config ──────┘
```

**Why DAG?**
- Cache has **two dependencies** (database and config)
- Dependencies are not aligned into simple sequential stages
- DAG automatically parallelizes `database` and `config`, then proceeds

#### DAG Configuration

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
});

builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<ConfigSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<SearchIndexSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();

builder.Services.AddIgnitionGraph((graph, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    var db = signals.First(s => s.Name == "database");
    var config = signals.First(s => s.Name == "config");
    var cache = signals.First(s => s.Name == "cache");
    var search = signals.First(s => s.Name == "search-index");
    var worker = signals.First(s => s.Name == "worker");

    graph.AddSignals(new[] { db, config, cache, search, worker });
    graph.DependsOn(cache, db);       // cache depends on database
    graph.DependsOn(cache, config);   // cache also depends on config
    graph.DependsOn(search, cache);   // search depends on cache
    graph.DependsOn(worker, search);  // worker depends on search
});
```

**Execution order**:
```
[Parallel]
  database ━━━━━━━━━━┓
  config ━━━━━━━━━━━┛
  → Both complete

cache ━━━━━━━━━━━━━━
  → Waits for both database AND config

search-index ━━━━━━━━
  → Waits for cache

worker ━━━━━━━━━━━━━
  → Waits for search-index
```

### Example: When Stages is Better

**Scenario**: Infrastructure → Services → Workers (simple sequential phases)

```
Stage 0: database, redis, rabbitmq (all parallel)
Stage 1: cache, search-index (all parallel)
Stage 2: worker, scheduler (all parallel)
```

**Why Stages?**
- Clear conceptual phases (infrastructure → services → workers)
- No complex interdependencies within phases
- Want policy control: "Stage 0 must succeed, but Stage 1 can tolerate failures"

#### Staged Configuration

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed;
});

// Stage 0: Infrastructure
builder.Services.AddIgnitionFromTaskWithStage("database", ct => db.ConnectAsync(ct), stage: 0);
builder.Services.AddIgnitionFromTaskWithStage("redis", ct => redis.ConnectAsync(ct), stage: 0);
builder.Services.AddIgnitionFromTaskWithStage("rabbitmq", ct => rabbitmq.ConnectAsync(ct), stage: 0);

// Stage 1: Services
builder.Services.AddIgnitionFromTaskWithStage("cache", ct => cache.WarmAsync(ct), stage: 1);
builder.Services.AddIgnitionFromTaskWithStage("search-index", ct => search.BuildAsync(ct), stage: 1);

// Stage 2: Workers
builder.Services.AddIgnitionFromTaskWithStage("worker", ct => worker.StartAsync(ct), stage: 2);
builder.Services.AddIgnitionFromTaskWithStage("scheduler", ct => scheduler.StartAsync(ct), stage: 2);
```

**Execution order**:
```
[Stage 0]
  database ━━━━━━┓
  redis ━━━━━━━━━┫ All parallel
  rabbitmq ━━━━━┛
  → All must succeed

[Stage 1]
  cache ━━━━━━━━━━┓ All parallel
  search-index ━━━┛
  → All must succeed

[Stage 2]
  worker ━━━━━━━━┓ All parallel
  scheduler ━━━━━┛
```

### When to Use Each

**Use DAG when**:
- Complex cross-dependencies (A depends on B and C; D depends on B and E)
- Need automatic cycle detection
- Want automatic skipping of dependents on failure
- Dependencies determined at runtime

**Use Stages when**:
- Simple sequential phases (infrastructure → services → workers)
- Want policy control per stage (e.g., Stage 0 must succeed, Stage 1 is best-effort)
- Clear conceptual layering
- Easier mental model for team

**Use Parallel when**:
- No dependencies at all (all signals independent)
- Simplest configuration, fastest execution

**Use Sequential when**:
- Rare: only when strict sequential order is required and you cannot use DAG or Stages
- Example: migrations that must run one at a time

---

## Recipe 9: Graceful Degradation Patterns

### Problem Statement

Your application can function in a degraded state when non-critical services fail. You want to:
- Start the application even if optional services (analytics, recommendations) are unavailable
- Clearly log which services succeeded/failed
- Expose degraded state via health checks
- Retry failed services in the background (optional)

### Configuration Pattern

#### Pattern A: BestEffort with Status Inspection

```csharp
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort; // Continue despite failures
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});

// Critical signals (must succeed for basic functionality)
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<AuthenticationServiceSignal>();

// Optional signals (nice to have)
builder.Services.AddIgnitionFromTask(
    "analytics-service",
    ct => analyticsClient.InitializeAsync(ct),
    timeout: TimeSpan.FromSeconds(10));

builder.Services.AddIgnitionFromTask(
    "recommendation-engine",
    ct => recommendationEngine.WarmAsync(ct),
    timeout: TimeSpan.FromSeconds(15));

var app = builder.Build();

var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    // BestEffort should not throw, but handle just in case
    logger.LogError(ex, "Ignition completed with errors");
}

var result = await coordinator.GetResultAsync();

// Inspect failures
var criticalFailed = result.Results
    .Where(r => r.Name.Contains("database") || r.Name.Contains("authentication"))
    .Where(r => r.Status != IgnitionSignalStatus.Succeeded);

if (criticalFailed.Any())
{
    logger.LogCritical("Critical services failed: {Names}",
        string.Join(", ", criticalFailed.Select(r => r.Name)));
    throw new InvalidOperationException("Cannot start without critical services");
}

// Log optional failures (degraded state)
var optionalFailed = result.Results
    .Where(r => r.Name.Contains("analytics") || r.Name.Contains("recommendation"))
    .Where(r => r.Status != IgnitionSignalStatus.Succeeded);

if (optionalFailed.Any())
{
    logger.LogWarning("⚠️ Starting in degraded mode. Failed optional services: {Names}",
        string.Join(", ", optionalFailed.Select(r => r.Name)));
}

app.Run(); // Start even with optional failures
```

#### Pattern B: Separate Critical and Optional with Staged Execution

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Staged;
    options.StagePolicy = IgnitionStagePolicy.AllMustSucceed; // Stage 0 must succeed
    options.GlobalTimeout = TimeSpan.FromSeconds(45);
});

// Stage 0: Critical services (must succeed)
builder.Services.AddIgnitionFromTaskWithStage(
    "database",
    ct => database.ConnectAsync(ct),
    stage: 0,
    timeout: TimeSpan.FromSeconds(15));

builder.Services.AddIgnitionFromTaskWithStage(
    "authentication",
    ct => authService.InitializeAsync(ct),
    stage: 0,
    timeout: TimeSpan.FromSeconds(10));

// Stage 1: Optional services (can fail)
builder.Services.AddIgnitionFromTaskWithStage(
    "analytics",
    ct => analyticsClient.InitializeAsync(ct),
    stage: 1,
    timeout: TimeSpan.FromSeconds(10));

builder.Services.AddIgnitionFromTaskWithStage(
    "recommendations",
    ct => recommendationEngine.WarmAsync(ct),
    stage: 1,
    timeout: TimeSpan.FromSeconds(15));

var app = builder.Build();
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();

await coordinator.WaitAllAsync(); // Throws if Stage 0 fails

var result = await coordinator.GetResultAsync();
if (result.HasStageResults)
{
    var stage1 = result.StageResults.FirstOrDefault(s => s.StageNumber == 1);
    if (stage1?.FailedCount > 0)
    {
        logger.LogWarning("⚠️ Starting in degraded mode. Stage 1 had {Failed} failures",
            stage1.FailedCount);
    }
}

app.Run();
```

#### Pattern C: Custom Health Check Reflecting Degraded State

```csharp
public class IgnitionDegradedHealthCheck : IHealthCheck
{
    private readonly IIgnitionCoordinator _coordinator;

    public IgnitionDegradedHealthCheck(IIgnitionCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await _coordinator.GetResultAsync();

        var criticalSignals = new[] { "database", "authentication" };
        var criticalFailed = result.Results
            .Where(r => criticalSignals.Any(c => r.Name.Contains(c)))
            .Where(r => r.Status != IgnitionSignalStatus.Succeeded);

        if (criticalFailed.Any())
        {
            return HealthCheckResult.Unhealthy(
                $"Critical services failed: {string.Join(", ", criticalFailed.Select(r => r.Name))}");
        }

        var anyFailed = result.Results.Any(r => r.Status != IgnitionSignalStatus.Succeeded);
        if (anyFailed)
        {
            var failedNames = string.Join(", ",
                result.Results.Where(r => r.Status != IgnitionSignalStatus.Succeeded).Select(r => r.Name));

            return HealthCheckResult.Degraded(
                $"Optional services failed: {failedNames}");
        }

        return HealthCheckResult.Healthy("All services ready");
    }
}

// Register
builder.Services.AddHealthChecks()
    .AddCheck<IgnitionDegradedHealthCheck>("ignition-with-degradation");
```

### Expected Behavior

1. **Critical services fail**: Application aborts startup (logs error, exits)
2. **Optional services fail**: Application starts in degraded mode (logs warning, continues)
3. **Health check reports degraded**: Kubernetes/load balancer can detect partial failures
4. **Clear observability**: Logs and health checks indicate which services failed

### When to Use

- **Microservices with optional dependencies**: Analytics, caching, recommendations
- **High availability over completeness**: Better to serve traffic with missing features than be down
- **Kubernetes deployments**: Use `Degraded` health status to signal partial readiness

---

## Recipe 10: Testing Startup Sequences

### Problem Statement

You want to test your startup sequence logic without running the full application. Goals:
- Unit test signal implementations
- Integration test coordinator behavior (policies, timeouts)
- Simulate failures and timeouts
- Verify dependency ordering (DAG)

### Configuration Pattern

#### Pattern A: Unit Testing Individual Signals

```csharp
using Xunit;
using NSubstitute;

public class DatabaseConnectionSignalTests
{
    [Fact]
    public async Task WaitAsync_Succeeds_WhenDatabaseConnects()
    {
        // arrange
        var mockClient = Substitute.For<IDatabaseClient>();
        mockClient.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var signal = new DatabaseConnectionSignal(mockClient);

        // act
        await signal.WaitAsync(CancellationToken.None);

        // assert
        await mockClient.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_ThrowsException_WhenDatabaseFails()
    {
        // arrange
        var mockClient = Substitute.For<IDatabaseClient>();
        mockClient.ConnectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var signal = new DatabaseConnectionSignal(mockClient);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => signal.WaitAsync(CancellationToken.None));

        Assert.Equal("Connection failed", ex.Message);
    }

    [Fact]
    public async Task WaitAsync_RespectsCancellation()
    {
        // arrange
        var mockClient = Substitute.For<IDatabaseClient>();
        mockClient.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(async ci => await Task.Delay(10000, ci.Arg<CancellationToken>()));

        var signal = new DatabaseConnectionSignal(mockClient);
        var cts = new CancellationTokenSource();

        // act
        var task = signal.WaitAsync(cts.Token);
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => task);
    }
}
```

#### Pattern B: Integration Testing Coordinator Policies

```csharp
public class CoordinatorIntegrationTests
{
    [Fact]
    public async Task FailFast_ThrowsOnFirstFailure()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition(options =>
        {
            options.Policy = IgnitionPolicy.FailFast;
            options.ExecutionMode = IgnitionExecutionMode.Sequential;
            options.GlobalTimeout = TimeSpan.FromSeconds(10);
        });

        var failingSignal = new FaultingSignal("failing", new InvalidOperationException("boom"));
        var successSignal = new SuccessfulSignal("success");

        services.AddSingleton<IIgnitionSignal>(failingSignal);
        services.AddSingleton<IIgnitionSignal>(successSignal);

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act & assert
        var ex = await Assert.ThrowsAsync<AggregateException>(() => coordinator.WaitAllAsync());
        Assert.Contains(ex.InnerExceptions, e => e is InvalidOperationException);
    }

    [Fact]
    public async Task BestEffort_ContinuesDespiteFailures()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition(options =>
        {
            options.Policy = IgnitionPolicy.BestEffort;
            options.GlobalTimeout = TimeSpan.FromSeconds(10);
        });

        services.AddSingleton<IIgnitionSignal>(new FaultingSignal("failing", new Exception("error")));
        services.AddSingleton<IIgnitionSignal>(new SuccessfulSignal("success"));

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync(); // Should not throw

        // assert
        var result = await coordinator.GetResultAsync();
        Assert.Equal(1, result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded));
        Assert.Equal(1, result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed));
    }
}

// Test helper signals
public class FaultingSignal : IIgnitionSignal
{
    private readonly Exception _exception;

    public string Name { get; }
    public TimeSpan? Timeout => null;

    public FaultingSignal(string name, Exception exception)
    {
        Name = name;
        _exception = exception;
    }

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        throw _exception;
    }
}

public class SuccessfulSignal : IIgnitionSignal
{
    public string Name { get; }
    public TimeSpan? Timeout => null;

    public SuccessfulSignal(string name)
    {
        Name = name;
    }

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

#### Pattern C: Testing DAG Dependency Ordering

```csharp
public class DependencyGraphTests
{
    [Fact]
    public async Task DAG_ExecutesInCorrectOrder()
    {
        // arrange
        var executionOrder = new ConcurrentBag<string>();

        var services = new ServiceCollection();
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            options.GlobalTimeout = TimeSpan.FromSeconds(10);
        });

        var signalA = new OrderTrackingSignal("A", executionOrder, delay: TimeSpan.FromMilliseconds(10));
        var signalB = new OrderTrackingSignal("B", executionOrder, delay: TimeSpan.FromMilliseconds(10));
        var signalC = new OrderTrackingSignal("C", executionOrder, delay: TimeSpan.FromMilliseconds(10));

        services.AddSingleton<IIgnitionSignal>(signalA);
        services.AddSingleton<IIgnitionSignal>(signalB);
        services.AddSingleton<IIgnitionSignal>(signalC);

        services.AddIgnitionGraph((graph, sp) =>
        {
            var signals = sp.GetServices<IIgnitionSignal>();
            var a = signals.First(s => s.Name == "A");
            var b = signals.First(s => s.Name == "B");
            var c = signals.First(s => s.Name == "C");

            graph.AddSignals(new[] { a, b, c });
            graph.DependsOn(b, a); // B depends on A
            graph.DependsOn(c, b); // C depends on B
        });

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var order = executionOrder.ToArray();
        Assert.Equal("A", order[0]);
        Assert.Equal("B", order[1]);
        Assert.Equal("C", order[2]);
    }
}

public class OrderTrackingSignal : IIgnitionSignal
{
    private readonly ConcurrentBag<string> _executionOrder;
    private readonly TimeSpan _delay;

    public string Name { get; }
    public TimeSpan? Timeout => null;

    public OrderTrackingSignal(string name, ConcurrentBag<string> executionOrder, TimeSpan delay)
    {
        Name = name;
        _executionOrder = executionOrder;
        _delay = delay;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _executionOrder.Add(Name);
        await Task.Delay(_delay, cancellationToken);
    }
}
```

#### Pattern D: Simulating Timeouts

```csharp
public class TimeoutTests
{
    [Fact]
    public async Task Signal_MarkedTimedOut_WhenExceedsTimeout()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition(options =>
        {
            options.Policy = IgnitionPolicy.BestEffort;
            options.CancelIndividualOnTimeout = true;
        });

        var slowSignal = new SlowSignal("slow", delay: TimeSpan.FromSeconds(10), timeout: TimeSpan.FromMilliseconds(100));
        services.AddSingleton<IIgnitionSignal>(slowSignal);

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        var slowResult = result.Results.First(r => r.Name == "slow");
        Assert.Equal(IgnitionSignalStatus.TimedOut, slowResult.Status);
    }
}

public class SlowSignal : IIgnitionSignal
{
    private readonly TimeSpan _delay;

    public string Name { get; }
    public TimeSpan? Timeout { get; }

    public SlowSignal(string name, TimeSpan delay, TimeSpan? timeout)
    {
        Name = name;
        _delay = delay;
        Timeout = timeout;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(_delay, cancellationToken);
    }
}
```

### Expected Behavior

1. **Unit tests**: Verify individual signal logic (connection, error handling, cancellation)
2. **Integration tests**: Verify coordinator policies (FailFast, BestEffort) and execution modes (Parallel, DAG, Staged)
3. **Ordering tests**: Verify DAG dependencies execute in correct order
4. **Timeout tests**: Verify signals are marked `TimedOut` when exceeding configured timeout

### When to Use

- **CI/CD pipelines**: Automated testing of startup logic
- **Refactoring safety**: Ensure changes don't break startup behavior
- **Policy validation**: Verify FailFast aborts on first failure, BestEffort continues
- **Dependency graph correctness**: Verify DAG executes in topological order

---

## Summary

These recipes provide **copy-paste-ready patterns** for solving real startup challenges:

1. **External Dependency Readiness**: Multi-stage warmup with clear ordering
2. **Cache Warmup**: Critical vs optional caches with concurrency limiting
3. **Background Workers**: Coordinate worker readiness with TaskCompletionSource
4. **Kubernetes Integration**: Readiness/liveness probes with ignition health checks
5. **Multi-Stage Pipelines**: Complex startup sequences with policy control per stage
6. **Recording/Replay**: Production diagnosis, regression detection, what-if simulation
7. **OpenTelemetry Metrics**: Zero-dependency metrics integration
8. **DAG vs Stages**: Decision guide for choosing execution model
9. **Graceful Degradation**: Continue startup with optional service failures
10. **Testing**: Unit and integration testing patterns

Each recipe is **battle-tested** and **production-ready**. Choose the pattern that fits your scenario, customize as needed, and ship with confidence.

---

## Next Steps

- **Explore samples**: See [samples/](../samples/) for working examples
- **Read core docs**: [Getting Started](getting-started.md), [Integration Recipes](integration-recipes.md)
- **Ask questions**: [GitHub Discussions](https://github.com/veggerby/Veggerby.Ignition/discussions)
