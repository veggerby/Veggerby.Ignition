# Performance Contract

This document establishes the **performance contract** for Veggerby.Ignition: measurable, documented guarantees about performance characteristics, scaling behavior, overhead, and limits.

## Overview

Veggerby.Ignition provides startup coordination with **predictable, bounded overhead** that scales linearly with signal count. The coordinator itself introduces minimal latency; signal implementation and I/O dominate total startup time.

## Performance Guarantees

### Coordinator Overhead

**Definition**: Time spent in coordinator logic (scheduling, timeout management, result aggregation) excluding signal execution.

| Signal Count | Max Overhead | Typical Overhead |
|--------------|--------------|------------------|
| 1            | < 1 ms       | < 0.5 ms         |
| 10           | < 5 ms       | < 2 ms           |
| 100          | < 20 ms      | < 10 ms          |
| 1000         | < 100 ms     | < 50 ms          |
| 10000        | < 500 ms     | < 250 ms         |

**Validation**: Run `CoordinatorOverheadBenchmarks` with `Task.CompletedTask` signals to isolate overhead.

### Memory Allocation

**Per-Signal Allocation** (cached after first execution):

- Signal task wrapper: ~200 bytes
- Result metadata: ~300 bytes
- Event/diagnostic overhead (tracing enabled): ~500 bytes

**Total**: < 1 KB per signal (typical), < 2 KB per signal (with tracing)

**Validation**: Run `MemoryDiagnoser` benchmarks to confirm allocation patterns.

### Execution Mode Scaling

#### Parallel Mode

**Characteristics**:
- All signals start simultaneously
- Completion time ≈ longest signal duration + coordinator overhead
- CPU usage: scales with parallelism (up to MaxDegreeOfParallelism or CPU cores)

**Scaling**:
- **Linear** throughput up to CPU core count
- **Sublinear** beyond CPU saturation (if signals are CPU-bound)
- **Near-constant** time for I/O-bound signals (network, disk)

**Example**:

```text
10 signals × 100ms each → ~110ms total (parallel)
100 signals × 100ms each → ~120ms total (parallel)
1000 signals × 100ms each → ~150ms total (parallel, I/O-bound)
```

**Best For**:
- Independent signals
- I/O-bound initialization (database connections, HTTP health checks)
- Fastest startup time
- Systems with multiple CPU cores

**Avoid When**:
- Signals share limited resources (connection pools)
- Memory-constrained environments
- Signals have ordering dependencies

#### Sequential Mode

**Characteristics**:
- Signals execute one at a time in registration order
- Completion time = sum of all signal durations + coordinator overhead

**Scaling**:
- **Linear** with signal count and duration
- **Predictable** resource usage (only 1 signal active at a time)

**Example**:

```text
10 signals × 100ms each → ~1010ms total
100 signals × 100ms each → ~10,020ms total
1000 signals × 100ms each → ~100,050ms total
```

**Best For**:
- Resource-constrained environments (limited memory, single CPU core)
- Signals with strict ordering requirements
- Debugging/troubleshooting (easier to trace execution order)
- Environments where predictable, low-concurrency behavior is required

**Avoid When**:
- Startup speed is critical
- Signals are independent and I/O-bound

#### DependencyAware Mode (DAG)

**Characteristics**:
- Independent branches run in parallel
- Dependent chains run sequentially
- Automatic topological sorting and cycle detection

**Scaling**:
- **Depends on graph structure**:
  - Fully independent signals → same as Parallel mode
  - Fully sequential chain → same as Sequential mode
  - Mixed (typical) → parallelism within independent branches

**Complexity**:
- Dependency graph construction: **O(V + E)** where V = vertices (signals), E = edges (dependencies)
- Topological sort: **O(V + E)**
- Execution overhead: **~2-5ms for 100 signals** (graph building, scheduling)

**Example**:

```text
Graph: A(100ms) → B(100ms), C(100ms) → D(100ms), E(100ms)
Result: A and C and E start parallel → max(100,100,100) = 100ms
        B and D start after A and C → 100ms
        Total: ~220ms (2 waves + overhead)
```

**Best For**:
- Complex initialization with dependencies (e.g., database → cache → worker threads)
- Mixed independent/dependent signals
- Automatic parallelization of independent branches
- Enforcing correct initialization order

**Avoid When**:
- Signal count > 1000 (graph overhead becomes noticeable)
- All signals are independent (use Parallel instead)
- All signals are sequential (use Sequential instead)
- Dynamic dependencies (graph is static at coordinator creation)

**Recommendations**:
- Keep dependency graphs **shallow** (< 10 levels deep) for best performance
- Prefer **wide** graphs (many independent branches) over **narrow** chains
- Limit total signal count to **< 1000** for sub-100ms graph overhead
- For 10,000+ signals with dependencies, use **Staged** mode instead

#### Staged Mode (Multi-Phase)

**Characteristics**:
- Signals grouped into sequential stages (0, 1, 2, ...)
- Within each stage: parallel execution
- Between stages: sequential execution

**Scaling**:
- Completion time = sum of stage durations + coordinator overhead
- Stage duration ≈ longest signal in that stage (parallel within stage)

**Example**:

```text
Stage 0: 10 signals × 100ms each → ~110ms
Stage 1: 5 signals × 200ms each → ~210ms
Stage 2: 20 signals × 50ms each → ~60ms
Total: ~380ms + overhead
```

**Best For**:
- Logical startup phases (infrastructure → services → application logic)
- Large signal counts (1000+) with implicit ordering
- Coarse-grained dependency expression (stage-level, not signal-level)
- Simple mental model (stages are explicit)

**Avoid When**:
- Fine-grained dependencies needed (use DAG instead)
- Signal count < 50 (overhead not justified)

**Recommendations**:
- Keep **2-5 stages** for clarity (too many stages = sequential overhead)
- Balance signals across stages (avoid 1 slow signal holding up entire stage)
- Use `IgnitionStagePolicy` to control cross-stage failure behavior

### Tracing Overhead

**Impact**:
- **Enabled**: ~5-10% overhead (Activity creation, propagation)
- **Disabled**: near-zero overhead (no diagnostic allocation)

**Recommendation**:
- Enable in **development** for observability
- Enable in **production** if tracing infrastructure is present (OpenTelemetry, Application Insights)
- Disable in **performance-critical** environments or when tracing is not consumed

### Timeout Management Overhead

**Per-Signal Timeout**:
- Adds `CancellationTokenSource` per signal: ~100 bytes allocation
- Timeout evaluation: ~0.1ms per signal

**Global Timeout**:
- Single `CancellationTokenSource` for all signals: ~100 bytes
- Negligible overhead (shared across signals)

**Recommendation**:
- Use **global timeout** for simplicity and minimal overhead
- Use **per-signal timeout** only when signals have significantly different expected durations

## Concurrency Limits (MaxDegreeOfParallelism)

**Effect**:
- Limits concurrent signal execution in Parallel, DependencyAware, and Staged modes
- Signals queue until a slot is available

**Tuning**:

| Signal Type | Recommended MaxDegreeOfParallelism |
|-------------|-------------------------------------|
| CPU-bound   | `Environment.ProcessorCount`        |
| I/O-bound (network) | `Environment.ProcessorCount × 2` |
| I/O-bound (disk)    | `Environment.ProcessorCount × 4` |
| Mixed       | `Environment.ProcessorCount + 2`    |
| Constrained (e.g., DB pool = 10) | Match pool size (10) |

**Example**:

```csharp
// I/O-bound signals (HTTP health checks)
options.MaxDegreeOfParallelism = Environment.ProcessorCount * 2;

// Database signals with connection pool limit of 10
options.MaxDegreeOfParallelism = 10;
```

## Determinism Guarantees

### Classification Determinism

**Guarantee**: Given identical signal tasks and options, coordinator produces **identical** `IgnitionResult` classification.

**What is deterministic**:
- Signal status (Succeeded, Failed, TimedOut)
- Timeout classification (global vs per-signal)
- Policy application (FailFast, BestEffort, ContinueOnTimeout)
- Result aggregation

**What is NOT deterministic**:
- **Exact durations** (I/O, CPU scheduling variability)
- **Execution order** in Parallel mode (thread scheduling)
- **Concurrency patterns** (OS-level scheduling)

**Implication**: Tests can assert on **status** and **classification**, not on exact millisecond timings.

### Serialization Stability

**Guarantee**: Timeline and Recording JSON schemas are **versioned** and **backward-compatible**.

**Schema Version**: `1.0` (current)

**Stability Contract**:
- Adding optional fields: **allowed** (non-breaking)
- Removing fields: **not allowed** (breaking, requires version bump)
- Renaming fields: **not allowed** (breaking)
- Changing field types: **not allowed** (breaking)

**Validation**: Tests in `IgnitionRecordingTests` and `IgnitionTimelineExportTests` validate schema stability.

## Scalability Limits

### Recommended Limits

| Configuration | Max Signals | Notes |
|---------------|-------------|-------|
| Parallel mode | 10,000      | Beyond this, overhead becomes noticeable |
| Sequential mode | No limit   | Linear scaling; only time-limited |
| DependencyAware (DAG) | 1,000 | Graph overhead increases with signal count |
| Staged mode   | 100,000     | Most scalable for large counts |

### Known Bottlenecks

**When to avoid Ignition**:
- **> 100,000 signals**: Consider breaking into multiple coordinators or batching
- **Real-time constraints**: Ignition is for startup, not runtime orchestration
- **Dynamic dependencies**: DAG is static; runtime dependency changes not supported

## Performance Testing Recommendations

### Benchmark Validation

Run benchmarks to validate contract:

```bash
cd benchmarks/Veggerby.Ignition.Benchmarks
dotnet run -c Release --filter *CoordinatorOverheadBenchmarks*
dotnet run -c Release --filter *ExecutionModeBenchmarks*
dotnet run -c Release --filter *ObservabilityOverheadBenchmarks*
```

### Profiling Startup

Use `result.ExportTimeline()` to identify bottlenecks:

```csharp
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

var result = await coordinator.GetResultAsync();
var timeline = result.ExportTimeline();
var json = timeline.ToJson();
File.WriteAllText("startup-timeline.json", json);

// Analyze: identify slowest signals, concurrency issues, dependency chains
```

### CI Regression Detection

Capture baseline recordings and compare in CI:

```bash
# Capture baseline
dotnet run -- --exporters json --artifacts baseline/

# Compare against current
dotnet run -- --exporters json --baseline baseline/results.json
```

## Real-World Performance

### Typical Startup Profiles

**Web API** (20 signals):
- Database connection: 500ms
- Cache warmup: 300ms
- External API health check: 200ms
- Metrics initialization: 50ms
- Other signals: < 50ms each

**Expected Total**: ~600ms (Parallel mode), ~1500ms (Sequential mode)

**Worker Service** (10 signals):
- Message queue connection: 400ms
- Database migration check: 600ms
- Scheduler initialization: 100ms

**Expected Total**: ~650ms (Parallel mode), ~1100ms (Sequential mode)

## Contract Validation

This contract is validated by:

1. **Benchmarks**: `benchmarks/Veggerby.Ignition.Benchmarks`
2. **Unit Tests**: `test/Veggerby.Ignition.Tests` (determinism, idempotency)
3. **Integration Tests**: Sample projects under `samples/`
4. **CI**: Automated benchmark runs (planned)

## Related Documentation

- [Performance Guide](performance.md) - Optimization techniques
- [Getting Started](getting-started.md) - Basic configuration
- [Benchmarks](../benchmarks/Veggerby.Ignition.Benchmarks/README.md) - Running performance tests
- [Dependency-Aware Execution](dependency-aware-execution.md) - DAG performance characteristics
