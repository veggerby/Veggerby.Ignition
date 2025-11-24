# API Reference

Complete reference for Veggerby.Ignition public API surface.

## Core Interfaces

### IIgnitionSignal

Represents an asynchronous readiness operation.

```csharp
public interface IIgnitionSignal
{
    /// <summary>
    /// Gets the signal name for identification and diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the optional signal-specific timeout override.
    /// If null, uses global timeout settings.
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// Executes the readiness operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Task representing the async operation.</returns>
    Task WaitAsync(CancellationToken cancellationToken = default);
}
```

**Usage**:

```csharp
public class MySignal : IIgnitionSignal
{
    public string Name => "my-signal";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public async Task WaitAsync(CancellationToken ct)
    {
        // Implementation
    }
}
```

### IIgnitionCoordinator

Coordinates execution of registered signals.

```csharp
public interface IIgnitionCoordinator
{
    /// <summary>
    /// Executes all registered signals and awaits completion.
    /// Idempotent: subsequent calls return cached result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing completion.</returns>
    /// <exception cref="AggregateException">Thrown when Policy is FailFast and signals fail.</exception>
    Task WaitAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves cached aggregated result.
    /// </summary>
    /// <returns>Ignition result, or null if not yet executed.</returns>
    Task<IgnitionResult?> GetResultAsync();
}
```

**Usage**:

```csharp
var coordinator = serviceProvider.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();
var result = await coordinator.GetResultAsync();
```

### IIgnitionBundle

Represents a reusable package of related signals.

```csharp
public interface IIgnitionBundle
{
    /// <summary>
    /// Gets the bundle name for identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Configures the bundle's signals and dependencies.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Optional bundle configuration.</param>
    void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null);
}
```

**Usage**:

```csharp
public class MyBundle : IIgnitionBundle
{
    public string Name => "my-bundle";

    public void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        // Register signals
    }
}
```

### IIgnitionGraphBuilder

Fluent builder for dependency graphs.

```csharp
public interface IIgnitionGraphBuilder
{
    /// <summary>
    /// Adds signals to the graph.
    /// </summary>
    IIgnitionGraphBuilder AddSignals(IEnumerable<IIgnitionSignal> signals);

    /// <summary>
    /// Declares that <paramref name="dependent"/> depends on <paramref name="prerequisites"/>.
    /// </summary>
    IIgnitionGraphBuilder DependsOn(
        IIgnitionSignal dependent,
        params IIgnitionSignal[] prerequisites);

    /// <summary>
    /// Automatically applies dependencies declared via [SignalDependency] attributes.
    /// </summary>
    IIgnitionGraphBuilder ApplyAttributeDependencies();

    /// <summary>
    /// Gets signals that <paramref name="signal"/> depends on.
    /// </summary>
    IEnumerable<IIgnitionSignal> GetDependencies(IIgnitionSignal signal);

    /// <summary>
    /// Gets signals that depend on <paramref name="signal"/>.
    /// </summary>
    IEnumerable<IIgnitionSignal> GetDependents(IIgnitionSignal signal);

    /// <summary>
    /// Gets signals with no dependencies (root signals).
    /// </summary>
    IEnumerable<IIgnitionSignal> GetRootSignals();

    /// <summary>
    /// Gets signals with no dependents (leaf signals).
    /// </summary>
    IEnumerable<IIgnitionSignal> GetLeafSignals();
}
```

**Usage**:

```csharp
builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    graphBuilder.AddSignals(signals);
    graphBuilder.DependsOn(cacheSignal, databaseSignal);
});
```

## Configuration Classes

### IgnitionOptions

Configuration for ignition coordinator.

```csharp
public class IgnitionOptions
{
    /// <summary>
    /// Global timeout for all signals. Default: 30 seconds.
    /// Soft deadline unless CancelOnGlobalTimeout is true.
    /// </summary>
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Execution strategy. Default: Parallel.
    /// </summary>
    public IgnitionExecutionMode ExecutionMode { get; set; } = IgnitionExecutionMode.Parallel;

    /// <summary>
    /// Failure handling policy. Default: BestEffort.
    /// </summary>
    public IgnitionPolicy Policy { get; set; } = IgnitionPolicy.BestEffort;

    /// <summary>
    /// Maximum concurrent signals (Parallel/DependencyAware modes).
    /// Null = unlimited. Default: null.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// When true, global timeout triggers cancellation of outstanding signals.
    /// Default: false (soft timeout).
    /// </summary>
    public bool CancelOnGlobalTimeout { get; set; } = false;

    /// <summary>
    /// When true, per-signal timeout triggers cancellation of that signal.
    /// Default: false.
    /// </summary>
    public bool CancelIndividualOnTimeout { get; set; } = false;

    /// <summary>
    /// When true, emits Activity for distributed tracing.
    /// Default: false.
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// Number of slowest signals to log. 0 = disabled. Default: 0.
    /// </summary>
    public int SlowHandleLogCount { get; set; } = 0;
}
```

### IgnitionBundleOptions

Configuration for bundles.

```csharp
public class IgnitionBundleOptions
{
    /// <summary>
    /// Default timeout applied to signals in bundle that don't specify their own.
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Reserved for future use.
    /// </summary>
    public IgnitionPolicy? Policy { get; set; }
}
```

## Result Classes

### IgnitionResult

Aggregated result of all signals.

```csharp
public class IgnitionResult
{
    /// <summary>
    /// Whether global or per-signal timeout occurred.
    /// </summary>
    public bool TimedOut { get; }

    /// <summary>
    /// Total execution duration.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Per-signal results.
    /// </summary>
    public IReadOnlyList<IgnitionSignalResult> Results { get; }
}
```

### IgnitionSignalResult

Result for individual signal.

```csharp
public class IgnitionSignalResult
{
    /// <summary>
    /// Signal name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Execution status.
    /// </summary>
    public IgnitionSignalStatus Status { get; }

    /// <summary>
    /// Signal execution duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Signal timeout (if specified).
    /// </summary>
    public TimeSpan? Timeout { get; }

    /// <summary>
    /// Exception (if Status is Failed).
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Names of failed prerequisite signals (DAG mode, Status is Skipped).
    /// </summary>
    public IReadOnlyList<string> FailedDependencies { get; }
}
```

## Enumerations

### IgnitionExecutionMode

Execution strategy for signals.

```csharp
public enum IgnitionExecutionMode
{
    /// <summary>
    /// All signals execute concurrently.
    /// </summary>
    Parallel = 0,

    /// <summary>
    /// Signals execute one at a time in registration order.
    /// </summary>
    Sequential = 1,

    /// <summary>
    /// Signals execute based on dependency graph.
    /// </summary>
    DependencyAware = 2
}
```

### IgnitionPolicy

Failure handling policy.

```csharp
public enum IgnitionPolicy
{
    /// <summary>
    /// Throw AggregateException on any failure.
    /// </summary>
    FailFast = 0,

    /// <summary>
    /// Log failures but continue startup.
    /// </summary>
    BestEffort = 1,

    /// <summary>
    /// Proceed when global timeout elapses.
    /// </summary>
    ContinueOnTimeout = 2
}
```

### IgnitionSignalStatus

Signal execution status.

```csharp
public enum IgnitionSignalStatus
{
    /// <summary>
    /// Signal completed successfully.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// Signal threw an exception.
    /// </summary>
    Failed = 1,

    /// <summary>
    /// Signal exceeded timeout.
    /// </summary>
    TimedOut = 2,

    /// <summary>
    /// Signal skipped due to failed dependencies (DAG mode only).
    /// </summary>
    Skipped = 3
}
```

## Extension Methods

### Service Collection Extensions

```csharp
public static class ServiceCollectionExtensions
{
    // Core registration
    IServiceCollection AddIgnition(Action<IgnitionOptions>? configure = null);

    // Signal registration
    IServiceCollection AddIgnitionSignal<TSignal>() where TSignal : class, IIgnitionSignal;
    IServiceCollection AddIgnitionSignal(IIgnitionSignal signal);
    IServiceCollection AddIgnitionSignal(Type signalType);

    // Task-based registration
    IServiceCollection AddIgnitionFromTask(string name, Task task, TimeSpan? timeout = null);
    IServiceCollection AddIgnitionFromTask(string name, Func<CancellationToken, Task> readyTaskFactory, TimeSpan? timeout = null);

    // Service adapter registration
    IServiceCollection AddIgnitionFor<TService>(Func<TService, Task> taskSelector, string? name = null, TimeSpan? timeout = null);
    IServiceCollection AddIgnitionForAll<TService>(Func<TService, Task> taskSelector, string groupName);
    IServiceCollection AddIgnitionFromFactory(Func<IServiceProvider, Task> taskFactory, string name, TimeSpan? timeout = null);

    // Bundle registration
    IServiceCollection AddIgnitionBundle(IIgnitionBundle bundle, Action<IgnitionBundleOptions>? configure = null);
    IServiceCollection AddIgnitionBundle<TBundle>() where TBundle : class, IIgnitionBundle, new();
    IServiceCollection AddIgnitionBundles(params IIgnitionBundle[] bundles);

    // Graph registration
    IServiceCollection AddIgnitionGraph(Action<IIgnitionGraphBuilder, IServiceProvider> configure);
}
```

### TaskCompletionSource Extensions

```csharp
public static class TaskCompletionSourceExtensions
{
    /// <summary>
    /// Sets the TaskCompletionSource to completed (success).
    /// </summary>
    void Ignited(this TaskCompletionSource tcs);

    /// <summary>
    /// Sets the TaskCompletionSource to faulted (failure).
    /// </summary>
    void IgnitionFailed(this TaskCompletionSource tcs, Exception exception);
}
```

**Usage**:

```csharp
private readonly TaskCompletionSource _readyTcs = new();

// Success
_readyTcs.Ignited();

// Failure
_readyTcs.IgnitionFailed(new InvalidOperationException("Failed"));
```

## Attributes

### SignalDependencyAttribute

Declares signal dependencies for DAG mode.

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class SignalDependencyAttribute : Attribute
{
    /// <summary>
    /// Declares dependency on signal by name.
    /// </summary>
    public SignalDependencyAttribute(string dependencyName);

    /// <summary>
    /// Declares dependency on signal by type.
    /// </summary>
    public SignalDependencyAttribute(Type dependencyType);

    public string? DependencyName { get; }
    public Type? DependencyType { get; }
}
```

**Usage**:

```csharp
[SignalDependency("database")]
public class CacheSignal : IIgnitionSignal { /* ... */ }

[SignalDependency(typeof(DatabaseSignal))]
public class CacheSignal : IIgnitionSignal { /* ... */ }
```

## Built-in Bundles

### HttpDependencyBundle

```csharp
public class HttpDependencyBundle : IIgnitionBundle
{
    public HttpDependencyBundle(string url, TimeSpan? timeout = null);
    public HttpDependencyBundle(IEnumerable<string> urls, TimeSpan? timeout = null);

    public string Name { get; }
    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null);
}
```

### DatabaseTrioBundle

```csharp
public class DatabaseTrioBundle : IIgnitionBundle
{
    public DatabaseTrioBundle(
        string databaseName,
        Func<CancellationToken, Task> connectFactory,
        Func<CancellationToken, Task>? validateSchemaFactory = null,
        Func<CancellationToken, Task>? warmupFactory = null,
        TimeSpan? defaultTimeout = null);

    public string Name { get; }
    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null);
}
```

## Health Check

Ignition automatically registers a health check named `ignition-readiness`.

**Registration**: Automatic when calling `AddIgnition()`

**Status Mapping**:

- `Healthy`: All signals succeeded
- `Degraded`: Soft global timeout (no failures)
- `Unhealthy`: Signal failures or hard timeout

**Data**:

```csharp
{
    "signalCount": 5,
    "succeededCount": 5,
    "failedCount": 0,
    "timedOutCount": 0,
    "totalDuration": "00:00:03.5420000",
    "timedOut": false
}
```

## Activity Tracing

When `EnableTracing = true`:

- **ActivitySource**: `Veggerby.Ignition.IgnitionCoordinator`
- **Activity name**: `Ignition.WaitAll`
- **Tags**: Policy, timeout, execution mode

Register with OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Veggerby.Ignition.IgnitionCoordinator");
    });
```

## Related Topics

- [Getting Started](getting-started.md) - Basic usage examples
- [Features](features.md) - Feature overview
- [Advanced Patterns](advanced-patterns.md) - Advanced usage
