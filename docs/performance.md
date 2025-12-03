# Performance Guide

This guide covers performance characteristics, optimization techniques, and tuning recommendations for Veggerby.Ignition.

## Performance Characteristics

### Execution Mode Performance

| Mode | Initialization | Parallelism | Best For |
|------|----------------|-------------|----------|
| Parallel | Fast | Maximum | Independent signals |
| Sequential | Slow | None | Resource-constrained environments |
| DependencyAware | Medium | Within branches | Complex dependencies |

### Parallel Mode

**Characteristics**:

- All signals start simultaneously
- Fastest when signals are independent
- Completion time ≈ slowest signal duration

**Example**:

```csharp
// 3 signals: 1s, 2s, 3s
// Total time: ~3s (all run in parallel)
```

### Sequential Mode

**Characteristics**:

- Signals execute one at a time
- Predictable resource usage
- Completion time = sum of all signal durations

**Example**:

```csharp
// 3 signals: 1s, 2s, 3s
// Total time: ~6s (1+2+3)
```

### DependencyAware Mode

**Characteristics**:

- Independent branches run in parallel
- Dependent chains run sequentially
- Optimal balance of correctness and performance

**Example**:

```csharp
// Branch 1: A(1s) → B(2s)
// Branch 2: C(3s)
// Total time: ~3s (A and C parallel, then B)
```

## Concurrency Limiting with MaxDegreeOfParallelism

Control maximum concurrent signal execution to manage resource usage.

### Configuration

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.MaxDegreeOfParallelism = 4; // Max 4 concurrent signals
});
```

### When to Limit Concurrency

✅ **Limit when**:

- Database connection pool size is limited
- HTTP client has connection limits
- Memory usage is a concern
- CPU saturation impacts other processes

❌ **Don't limit when**:

- Signals are I/O bound and lightweight
- System has ample resources
- Startup speed is critical

### Finding Optimal Value

```csharp
// Start with number of CPU cores
var optimalParallelism = Environment.ProcessorCount;

// Adjust based on signal characteristics
if (signalsAreCpuIntensive)
{
    optimalParallelism = Environment.ProcessorCount;
}
else if (signalsAreIoBound)
{
    optimalParallelism = Environment.ProcessorCount * 2; // Or higher
}

builder.Services.AddIgnition(options =>
{
    options.MaxDegreeOfParallelism = optimalParallelism;
});
```

## Idempotent Execution Benefits

Signals are executed at most once per coordinator instance, with results cached.

### Memory Benefits

```csharp
var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

// First call: executes signals
await coordinator.WaitAllAsync();

// Subsequent calls: returns cached result (no re-execution)
await coordinator.WaitAllAsync();
await coordinator.WaitAllAsync();

// Same cached result
var result = await coordinator.GetResultAsync();
```

**Benefits**:

- No duplicate side-effects (database connections, HTTP requests)
- Safe to call multiple times
- Consistent result inspection

### Cache Overhead

The coordinator stores:

- Per-signal `Task` instances
- Aggregated `IgnitionResult`
- Per-signal `IgnitionSignalResult`

**Typical overhead**: < 1 KB per signal

## Lazy Task Execution

Signal tasks are created lazily on first `WaitAllAsync()` call.

### Benefits

```csharp
// Registration: tasks NOT created yet
builder.Services.AddIgnitionFromTask(
    "expensive",
    ct => ExpensiveOperationAsync(ct)); // Not invoked yet

var app = builder.Build();

// Tasks created here (on first WaitAllAsync)
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();
```

**Benefits**:

- No startup overhead during DI container setup
- Service provider fully constructed before signal execution
- Signals can safely resolve services from DI

## Memory Allocation Considerations

### Coordinator Allocations

Per `WaitAllAsync()` invocation:

1. Task array for signal tasks
2. Stopwatch instances
3. CancellationTokenSource (if timeout configured)
4. Result aggregation structures

**Typical allocation**: < 10 KB for 10 signals

### Signal Allocations

Signal implementations control their own allocations. Optimize by:

- Reusing buffers
- Avoiding LINQ in hot paths
- Using `ValueTask` for synchronous cases
- Pooling connections/clients

### Example: Optimized Signal

```csharp
public class OptimizedDatabaseSignal : IIgnitionSignal
{
    private static readonly DbConnection s_sharedConnection = /* ... */;

    public string Name => "database";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public async Task WaitAsync(CancellationToken ct)
    {
        // Reuse shared connection instead of creating new one
        if (s_sharedConnection.State != ConnectionState.Open)
        {
            await s_sharedConnection.OpenAsync(ct);
        }
    }
}
```

## Benchmark Results

Performance on a typical developer machine (Intel i7, 16GB RAM):

### Execution Time by Mode

| Signals | Mode | Duration (ms) |
|---------|------|---------------|
| 10 (100ms each) | Parallel | ~110 |
| 10 (100ms each) | Sequential | ~1010 |
| 10 (100ms each) | DAG (5 indep) | ~210 |

### Overhead by Signal Count

| Signal Count | Overhead (ms) |
|--------------|---------------|
| 1 | < 1 |
| 10 | < 5 |
| 100 | < 20 |
| 1000 | < 100 |

**Conclusion**: Coordinator overhead is minimal; signal implementation dominates.

## Performance Tuning Recommendations

### 1. Choose Appropriate Execution Mode

```csharp
// Independent signals: use Parallel
if (signalsAreIndependent)
{
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
}
// Complex dependencies: use DAG
else if (hasDependencies)
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
}
// Resource-constrained: use Sequential
else if (limitedResources)
{
    options.ExecutionMode = IgnitionExecutionMode.Sequential;
}
```

### 2. Optimize Signal Implementations

**Bad** (blocking):

```csharp
public async Task WaitAsync(CancellationToken ct)
{
    var result = _httpClient.GetAsync(_url, ct).Result; // Blocking!
}
```

**Good** (async):

```csharp
public async Task WaitAsync(CancellationToken ct)
{
    var result = await _httpClient.GetAsync(_url, ct);
}
```

### 3. Use Appropriate Timeouts

**Too short**: Unnecessary failures

```csharp
public TimeSpan? Timeout => TimeSpan.FromMilliseconds(10); // Too aggressive
```

**Too long**: Delayed failure detection

```csharp
public TimeSpan? Timeout => TimeSpan.FromMinutes(10); // Too generous
```

**Appropriate**: Based on p95 observed duration + buffer

```csharp
public TimeSpan? Timeout => TimeSpan.FromSeconds(5); // Realistic
```

### 4. Minimize Dependencies in DAG Mode

**Bad** (over-constrained):

```text
A → B → C → D → E  (fully sequential)
```

**Good** (parallelizable):

```text
A → B
C → D
E
```

### 5. Reuse Connections/Clients

**Bad** (new instance per signal):

```csharp
public async Task WaitAsync(CancellationToken ct)
{
    using var client = new HttpClient();
    await client.GetAsync(_url, ct);
}
```

**Good** (injected shared client):

```csharp
private readonly HttpClient _client;

public MySignal(IHttpClientFactory factory)
{
    _client = factory.CreateClient();
}

public async Task WaitAsync(CancellationToken ct)
{
    await _client.GetAsync(_url, ct);
}
```

### 6. Enable Tracing Only When Needed

```csharp
#if DEBUG
options.EnableTracing = true; // Development
#else
options.EnableTracing = false; // Production (unless observability required)
#endif
```

### 7. Tune Concurrency for Workload

**CPU-bound signals**:

```csharp
options.MaxDegreeOfParallelism = Environment.ProcessorCount;
```

**I/O-bound signals**:

```csharp
options.MaxDegreeOfParallelism = Environment.ProcessorCount * 2;
```

**Mixed workload**:

```csharp
options.MaxDegreeOfParallelism = Environment.ProcessorCount + 2;
```

## Profiling Startup Performance

### Using BenchmarkDotNet

```csharp
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.net100)]
public class StartupBenchmarks
{
    private IServiceProvider _serviceProvider;

    [Params(1, 10, 100)]
    public int SignalCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
        });

        for (int i = 0; i < SignalCount; i++)
        {
            var name = $"signal-{i}";
            services.AddIgnitionFromTask(name, ct => Task.Delay(10, ct));
        }

        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark]
    public async Task WaitAllAsync()
    {
        var coordinator = _serviceProvider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();
    }
}
```

### Using Stopwatch

```csharp
var sw = Stopwatch.StartNew();

var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

sw.Stop();
Console.WriteLine($"Ignition completed in {sw.ElapsedMilliseconds}ms");

var result = await coordinator.GetResultAsync();
Console.WriteLine($"Coordinator measured: {result.TotalDuration.TotalMilliseconds:F0}ms");
```

### Identifying Bottlenecks

```csharp
var result = await coordinator.GetResultAsync();

var bottlenecks = result.Results
    .OrderByDescending(r => r.Duration)
    .Take(5);

Console.WriteLine("Top 5 slowest signals:");
foreach (var r in bottlenecks)
{
    var pct = (r.Duration.TotalSeconds / result.TotalDuration.TotalSeconds) * 100;
    Console.WriteLine($"  {r.Name}: {r.Duration.TotalMilliseconds:F0}ms ({pct:F1}%)");
}
```

## Scaling Considerations

### Horizontal Scaling

Ignition is per-instance; each pod/container/instance runs its own ignition:

```yaml
# Kubernetes deployment with 3 replicas
apiVersion: apps/v1
kind: Deployment
metadata:
  name: myapp
spec:
  replicas: 3  # Each runs ignition independently
  template:
    spec:
      containers:
      - name: myapp
        image: myapp:latest
```

**Consideration**: If signals have side-effects (e.g., database migrations), coordinate across instances.

### Vertical Scaling

More CPU/memory improves:

- Parallel signal execution speed
- Higher `MaxDegreeOfParallelism` capacity
- Faster individual signal operations

## Comparison with Alternatives

### vs Manual Task.WhenAll

**Ignition**:

- ✅ Built-in timeout management
- ✅ Per-signal diagnostics
- ✅ Health check integration
- ✅ DAG execution
- ✅ Idempotent execution

**Manual**:

- ✅ Lower overhead (< 1ms)
- ✅ No dependency
- ❌ No timeout management
- ❌ No diagnostics

**Verdict**: Use Ignition for production apps; manual for simple scripts.

### vs Hosted Service Ordering

**Ignition**:

- ✅ Explicit signal completion
- ✅ Failure handling
- ✅ Timeout enforcement
- ✅ Diagnostics

**Hosted Services**:

- ✅ Native ASP.NET Core integration
- ❌ No completion signal
- ❌ No failure handling

**Verdict**: Ignition complements hosted services for readiness coordination.

## Related Topics

- [Getting Started](getting-started.md) - Basic configuration
- [Dependency-Aware Execution](dependency-aware-execution.md) - DAG performance
- [Timeout Management](timeout-management.md) - Timeout tuning
- [Advanced Patterns](advanced-patterns.md) - Optimization patterns
