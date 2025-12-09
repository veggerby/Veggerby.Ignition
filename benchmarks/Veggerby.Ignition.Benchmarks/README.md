# Veggerby.Ignition Benchmarks

This project contains performance benchmarks for Veggerby.Ignition using BenchmarkDotNet.

## Running Benchmarks

### Run All Benchmarks

```bash
cd benchmarks/Veggerby.Ignition.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark

```bash
dotnet run -c Release --filter "*ExecutionModeBenchmarks*"
```

### Run with Specific Parameters

```bash
dotnet run -c Release -- --filter *ExecutionModeBenchmarks* --job short
```

## Benchmark Categories

### ExecutionModeBenchmarks
Compares different execution modes (Parallel vs Sequential) with varying signal counts (10, 100, 1000).

**Key Metrics:**
- Mean execution time
- Memory allocations
- Throughput

**Parameters:**
- `SignalCount`: 10, 100, 1000
- `ExecutionMode`: Parallel, Sequential
- `SignalDelayMs`: 10ms (simulated work)

### ObservabilityOverheadBenchmarks
Measures overhead introduced by tracing and metrics collection.

**Key Metrics:**
- Overhead with tracing enabled vs disabled
- Memory allocation differences

**Parameters:**
- `SignalCount`: 100
- `EnableTracing`: true, false

### ConcurrencyLimitingBenchmarks
Evaluates performance impact of MaxDegreeOfParallelism settings.

**Key Metrics:**
- Execution time vs parallelism limit
- Memory usage patterns

**Parameters:**
- `SignalCount`: 100
- `MaxDegreeOfParallelism`: 1, 4, 8, -1 (unlimited)

### CoordinatorOverheadBenchmarks
Isolates pure coordinator overhead with minimal-work signals.

**Key Metrics:**
- Fixed overhead per signal
- Scaling characteristics

**Parameters:**
- `SignalCount`: 1, 10, 100, 1000

## Interpreting Results

BenchmarkDotNet produces detailed results including:

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation
- **Gen0/Gen1/Gen2**: GC collection counts per 1000 operations
- **Allocated**: Total memory allocated

### Example Output

```
|              Method | SignalCount | ExecutionMode |     Mean |   Error |  StdDev | Allocated |
|-------------------- |------------ |-------------- |---------:|--------:|--------:|----------:|
| WaitAllAsync        |          10 |      Parallel |  12.5 ms | 0.15 ms | 0.14 ms |   15.2 KB |
| WaitAllAsync        |         100 |      Parallel |  25.3 ms | 0.32 ms | 0.30 ms |  125.8 KB |
| WaitAllAsync        |        1000 |      Parallel | 180.5 ms | 2.10 ms | 1.95 ms | 1250.4 KB |
```

## Performance Contract Validation

These benchmarks validate the performance contract documented in `docs/performance-contract.md`:

- **Coordinator Overhead**: < 1ms for 1 signal, < 100ms for 1000 signals
- **Parallel Scaling**: Near-linear up to CPU core count
- **Sequential Scaling**: Linear with signal count
- **Tracing Overhead**: < 10% with tracing enabled
- **Memory Allocation**: < 1 KB per signal

## CI Integration

Benchmarks can be run in CI to detect performance regressions:

```bash
dotnet run -c Release -- --filter *CoordinatorOverheadBenchmarks* --exporters json
```

Compare results against baseline using BenchmarkDotNet's statistical comparison features.

## Related Documentation

- [Performance Guide](../../docs/performance.md)
- [Performance Contract](../../docs/performance-contract.md)
- [Getting Started](../../docs/getting-started.md)
