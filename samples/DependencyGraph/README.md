# Dependency Graph Sample

**Complexity**: Advanced
**Type**: Console Application
**Focus**: Dependency-aware (DAG) execution mode

## Overview

This sample demonstrates how to use Veggerby.Ignition's dependency-aware execution mode to coordinate startup signals with complex dependency relationships. Instead of running all signals in parallel or sequentially, the DAG (Directed Acyclic Graph) mode automatically determines the correct execution order based on declared dependencies.

## What This Sample Demonstrates

### Core Features

- **Dependency Declaration**: Two ways to declare signal dependencies:
  - Attribute-based using `[SignalDependency]`
  - Fluent API using `builder.DependsOn()`
- **Automatic Topological Sorting**: Signals execute in dependency order automatically
- **Parallel Independent Branches**: Signals with no dependency relationship run concurrently
- **Failure Propagation**: When a signal fails, all dependents are automatically skipped
- **Cycle Detection**: Invalid dependency graphs are caught at startup with clear error messages

### Dependency Structure

The sample implements a realistic startup scenario with the following dependency graph:

```txt
       Database          Configuration
          |               /     \
        Cache            /       \
          |             /         \
          +--------- Worker       API
```

**Execution Flow**:

1. Database and Configuration start in parallel (no dependencies)
2. Cache starts after Database completes
3. API starts after Configuration completes (runs parallel with Cache)
4. Worker starts after BOTH Cache AND Configuration complete

## Running the Sample

```bash
cd samples/DependencyGraph
dotnet run
```

## Expected Output

You'll see two examples running:

### Example 1: Attribute-Based Dependencies

Uses `[SignalDependency]` attributes on signal classes for declarative dependency declaration.

### Example 2: Fluent API Dependencies

Uses `builder.DependsOn()` method calls to define dependencies programmatically.

Both examples produce the same execution order but demonstrate different API styles.

## Key Concepts

### 1. Dependency Declaration

**Attribute-based** (declarative):

```csharp
[SignalDependency("database")]
public class CacheSignal : IIgnitionSignal
{
    public string Name => "cache";
    // ...
}
```

**Fluent API** (programmatic):

```csharp
services.AddIgnitionGraph((builder, sp) =>
{
    var db = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "database");
    var cache = sp.GetServices<IIgnitionSignal>().First(s => s.Name == "cache");
    
    builder.AddSignals(new[] { db, cache });
    builder.DependsOn(cache, db); // cache depends on db
});
```

### 2. Execution Mode

To enable dependency-aware execution:

```csharp
services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
});
```

### 3. Graph Building

The graph must be explicitly built and registered:

```csharp
services.AddIgnitionGraph((builder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    builder.AddSignals(signals);
    builder.ApplyAttributeDependencies(); // For attribute-based approach
});
```

### 4. Result Inspection

Check if signals were skipped due to failed dependencies:

```csharp
var result = await coordinator.GetResultAsync();
foreach (var r in result.Results)
{
    if (r.SkippedDueToDependencies)
    {
        Console.WriteLine($"{r.Name} skipped due to: {string.Join(", ", r.FailedDependencies)}");
    }
}
```

## Benefits of DAG Mode

1. **Automatic Ordering**: No need to manually sequence signals
2. **Optimal Parallelism**: Independent branches run concurrently automatically
3. **Clear Dependencies**: Dependencies are explicit and self-documenting
4. **Failure Isolation**: Failed signals only affect their dependents
5. **Deterministic Execution**: Same graph structure produces same execution order

## Common Use Cases

- **Database → Cache → Worker**: Sequential data layer initialization
- **Configuration → Multiple Services**: Config must load before service-specific setup
- **Primary → Replica Connections**: Replica connects only if primary succeeds
- **Migration → Seeding → Validation**: Database setup pipeline
- **External APIs → Internal Services**: Dependency health checks before service startup

## Comparison with Other Modes

| Mode | Use When | Execution Order | Independent Signals |
|------|----------|----------------|---------------------|
| Parallel | No dependencies | All at once | All concurrent |
| Sequential | Simple linear order | Registration order | One at a time |
| DependencyAware | Complex dependencies | Topological sort | Parallel if independent |

## Troubleshooting

### Cycle Detection Error

If you see an error like:

```txt
Ignition graph contains a cycle: s1 -> s2 -> s3 -> s1
```

This means you've created a circular dependency. Review your dependency declarations and break the cycle.

### Signal Never Starts

If a signal never executes, check:

1. Are all its dependencies registered?
2. Did any dependency fail or timeout?
3. Check the `FailedDependencies` property in the result

### Unexpected Execution Order

Remember that signals with no dependency relationship can execute in any order (they run in parallel). Only dependency relationships guarantee ordering.

## Related Documentation

- [Main README](../../README.md#dependency-aware-execution-dag)
- [API Documentation](../../src/Veggerby.Ignition/README.md)
- [Epic Issue](https://github.com/veggerby/Veggerby.Ignition/issues/2)
