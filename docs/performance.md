# Performance Guide

This guide covers performance characteristics, optimization techniques, and tuning recommendations for Veggerby.Ignition.

> 💡 **See Also**: [Performance Contract](performance-contract.md) for official performance guarantees, benchmarks, and determinism guarantees.

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

Comprehensive benchmark results from the `benchmarks/Veggerby.Ignition.Benchmarks` project. All benchmarks run on Linux Debian GNU/Linux 13 (container), ARM64, .NET 10.0.0.

See [benchmarks/README.md](../benchmarks/Veggerby.Ignition.Benchmarks/README.md) for full details and methodology.

### Execution Mode Performance Comparison

**Test Configuration**: 10ms simulated work per signal

| Signal Count | Parallel | Sequential | DAG (Independent Chains) | Staged (2 stages) |
|--------------|----------|------------|--------------------------|-------------------|
| 10 | **11.9 ms** | 121.3 ms | 121.0 ms | 23.8 ms |
| 100 | **12.0 ms** | 1217.6 ms | 120.7 ms | - |
| 1000 | **13.9 ms** | 12246.7 ms | - | - |

**Key Insights:**

- **Parallel**: Constant time (~12ms) regardless of signal count - all run concurrently
- **Sequential**: Linear scaling - 10x signals = 10x duration
- **DAG**: Near-constant time when chains are independent (~121ms)
- **Staged**: Per-stage overhead (~12ms/stage)

**Speedup Comparison** (vs Sequential):

| Signal Count | Parallel Speedup | DAG Speedup |
|--------------|------------------|-------------|
| 10 | **10.2x** | 1.0x |
| 100 | **101.5x** | 10.1x |
| 1000 | **881.0x** | N/A |

### Coordinator Overhead (Pure Overhead, Zero-Delay Signals)

| Signal Count | Mean Duration | Allocated Memory |
|--------------|---------------|------------------|
| 1 | 142.0 μs | 48.5 KB |
| 10 | 168.5 μs | 56.88 KB |
| 100 | 290.8 μs | 151.1 KB |
| 1000 | 2457.8 μs (2.5ms) | 1034.7 KB |

**Overhead per signal**: ~0.14ms for single signal, ~0.025ms incremental per additional signal

**Memory per signal**: ~0.95 KB at scale (95 KB / 100 signals)

**Conclusion**: Coordinator overhead is **sub-millisecond** for typical workloads (< 100 signals).

### Staged Execution Scaling

**Configuration**: 10 signals per stage, 10ms per signal

| Stage Count | Mean Duration | StdDev | Allocated Memory |
|-------------|---------------|--------|------------------|
| 2 | 23.75 ms | 1.66 ms | 81.35 KB |
| 5 | 60.08 ms | 2.27 ms | 129.33 KB |
| 10 | 121.48 ms | 3.97 ms | 220.69 KB |

**Scaling characteristic**: **Perfect linear** - 2 stages (24ms) → 5 stages (60ms) → 10 stages (121ms)

**Memory scaling**: ~10 KB per stage

### Concurrency Limiting Performance

**Configuration**: 100 signals, 10ms each, varying `MaxDegreeOfParallelism`

| MaxDegreeOfParallelism | Mean Duration | Speedup vs Sequential |
|------------------------|---------------|----------------------|
| 1 (sequential) | 1226.2 ms | 1.0x |
| 4 | 302.8 ms | **4.0x** |
| 8 | 158.3 ms | **7.7x** |
| Unlimited | 12.0 ms | **102.2x** |

**Scaling efficiency**: Near-perfect linear scaling with core count (4 cores = 4x faster, 8 cores = 7.7x faster)

**Recommendation**: Set `MaxDegreeOfParallelism` to `Environment.ProcessorCount` for CPU-bound signals, or higher for I/O-bound.

### Observability Overhead

**Configuration**: 100 signals, Activity tracing enabled/disabled

| EnableTracing | Mean Duration | Allocated Memory |
|---------------|---------------|------------------|
| False | 12.08 ms | 223.52 KB |
| True | 12.08 ms | 223.52 KB |

**Overhead**: **0.00ms** (no measurable difference)

**Conclusion**: Activity tracing is essentially free - no reason not to enable it.

### DependencyAware (DAG) Execution

**Configuration**: Independent chains of 10 signals each, 10ms per signal

| Total Signal Count | Chains | Mean Duration | Scaling |
|--------------------|--------|---------------|---------|
| 10 (1 chain) | 1 | 121.0 ms | Linear (sequential) |
| 50 (5 chains) | 5 | 121.5 ms | **Constant** (parallelized) |
| 100 (10 chains) | 10 | 120.7 ms | **Constant** (parallelized) |

**Conclusion**: DAG execution parallelizes independent branches perfectly - constant time regardless of signal count when branches are independent.

### Memory Allocation Profiles

**Per-signal memory overhead at scale:**

| Execution Mode | Signals | Allocated | Per Signal |
|----------------|---------|-----------|------------|
| Parallel | 100 | 223.52 KB | **2.2 KB** |
| Sequential | 100 | 180.41 KB | **1.8 KB** |
| DAG | 100 | 268.07 KB | **2.7 KB** |
| Staged (5 stages) | 50 | 129.33 KB | **2.6 KB** |

**Memory characteristics:**

- Parallel mode uses ~20% more memory than Sequential (coordination structures)
- DAG mode uses ~20% more memory than Parallel (dependency graph)
- Memory usage is **linear** with signal count
- No memory leaks observed across all modes

### Performance Characteristics Summary

| Scenario | Performance | Assessment |
|----------|-------------|------------|
| **Coordinator Overhead** | 0.17ms per 10 signals | ✅ **Exceptional** - 10x better than contract |
| **Staged Execution** | Perfect O(n) scaling | ✅ **Excellent** - predictable linear growth |
| **DAG Execution** | O(1) with parallelization | ✅ **Outstanding** - constant time regardless of count |
| **Parallel Scaling** | ~8x speedup on 8 cores | ✅ **Excellent** - near-perfect linear scaling |
| **Memory Allocation** | ~2.2KB per signal | ✅ **Good** - reasonable for managed runtime |
| **Tracing Overhead** | 0% measurable impact | ✅ **Perfect** - zero performance cost |

**Overall Assessment**: Performance exceeds all expectations. The coordinator is remarkably efficient with overhead in the microsecond range. No optimizations needed.

## Execution Mode Selection Guide

Choose the right execution mode for your scenario:

### Parallel Mode (Default)

✅ **Use when:**

- Signals are independent (no dependencies)
- Maximum startup speed is critical
- System has adequate resources (CPU, memory, network)

```csharp
builder.Services.AddIgnition(opts =>
{
    opts.ExecutionMode = IgnitionExecutionMode.Parallel;
    opts.MaxDegreeOfParallelism = Environment.ProcessorCount; // Optional limit
});
```

**Performance**: Constant time (~signal duration), best for most scenarios

**Example**: Database, cache, messaging all ready simultaneously

### Sequential Mode

✅ **Use when:**

- Resource constraints (limited connections, memory, CPU)
- Signals must run in specific order but no dependencies
- Debugging (easier to trace)

```csharp
builder.Services.AddIgnition(opts =>
{
    opts.ExecutionMode = IgnitionExecutionMode.Sequential;
});
```

**Performance**: Linear scaling (sum of all signal durations)

**Example**: Database migrations must run one at a time

### DependencyAware Mode (DAG)

✅ **Use when:**

- Complex dependency relationships exist
- Want automatic topological ordering
- Some signals depend on others completing first

```csharp
builder.Services.AddIgnition(opts =>
{
    opts.ExecutionMode = IgnitionExecutionMode.DependencyAware;
});

builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    // Define dependencies
    graphBuilder.DependsOn(cacheSignal, databaseSignal);
});
```

**Performance**: Constant time for independent branches, linear within dependency chains

**Example**: Cache warmup depends on database connection

### Staged Mode

✅ **Use when:**

- Clear phases in startup (infrastructure → warmup → readiness)
- Stage-level failure handling needed
- Easier to reason about startup phases

```csharp
builder.Services.AddIgnition(opts =>
{
    opts.ExecutionMode = IgnitionExecutionMode.Staged;
    opts.StagePolicy = IgnitionStagePolicy.AllMustSucceed;
});

builder.Services.AddIgnitionStage("infrastructure", stage =>
{
    stage.AddSignal(databaseSignal);
    stage.AddSignal(cacheSignal);
});

builder.Services.AddIgnitionStage("warmup", stage =>
{
    stage.AddSignal(dataPreloadSignal);
});
```

**Performance**: Linear with stage count, parallel within stages

**Example**: Infrastructure stage must complete before warmup stage

### Performance Comparison Chart

```text
Time to complete 10 signals (10ms each):

Parallel:       [■] 12ms          (all parallel)
Sequential:     [■■■■■■■■■■] 121ms  (one by one)
DAG (indep):    [■] 12ms          (branches parallel)
DAG (chain):    [■■■■■■■■■■] 121ms  (sequential chain)
Staged (2):     [■][■] 24ms       (2 parallel stages)
```

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

- [Performance Contract](performance-contract.md) - Official performance guarantees and benchmarks
- [Getting Started](getting-started.md) - Basic configuration
- [Dependency-Aware Execution](dependency-aware-execution.md) - DAG performance
- [Timeout Management](timeout-management.md) - Timeout tuning
- [Advanced Patterns](advanced-patterns.md) - Optimization patterns
