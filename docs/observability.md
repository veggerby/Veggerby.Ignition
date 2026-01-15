# Observability and Diagnostics

This guide covers monitoring, logging, tracing, and diagnostics for Veggerby.Ignition startup signals.

## Overview

Ignition provides multiple observability features:

- **Result inspection**: Detailed per-signal outcomes
- **Structured logging**: Automatic and custom log messages
- **Activity tracing**: OpenTelemetry-compatible distributed tracing
- **Health checks**: ASP.NET Core health check integration
- **Slow signal logging**: Automatic performance bottleneck detection
- **Metrics**: Extensible metric collection points

## Result Inspection

The `IgnitionResult` contains complete diagnostic information about signal execution.

### Result Properties

```csharp
var coordinator = serviceProvider.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();
var result = await coordinator.GetResultAsync();

// Overall metrics
bool timedOut = result.TimedOut;           // Did global timeout occur?
TimeSpan totalDuration = result.TotalDuration; // Total execution time

// Per-signal results
foreach (var signalResult in result.Results)
{
    string name = signalResult.Name;
    IgnitionSignalStatus status = signalResult.Status;
    TimeSpan duration = signalResult.Duration;
    TimeSpan? timeout = signalResult.Timeout;
    Exception? exception = signalResult.Exception;
    string[] failedDeps = signalResult.FailedDependencies; // DAG mode only
}
```

### Signal Status Values

```csharp
public enum IgnitionSignalStatus
{
    Succeeded,  // Signal completed successfully
    Failed,     // Signal threw an exception
    TimedOut,   // Signal exceeded its timeout
    Skipped     // Signal skipped due to failed dependencies (DAG mode)
}
```

### Inspecting Successful Signals

```csharp
var succeeded = result.Results.Where(r => r.Status == IgnitionSignalStatus.Succeeded);
foreach (var r in succeeded)
{
    logger.LogInformation(
        "✓ {Name} completed in {Duration:F0}ms",
        r.Name,
        r.Duration.TotalMilliseconds);
}
```

### Inspecting Failed Signals

```csharp
var failed = result.Results.Where(r => r.Status == IgnitionSignalStatus.Failed);
foreach (var r in failed)
{
    logger.LogError(
        r.Exception,
        "✗ {Name} failed after {Duration:F0}ms",
        r.Name,
        r.Duration.TotalMilliseconds);
}
```

### Inspecting Timed Out Signals

```csharp
var timedOut = result.Results.Where(r => r.Status == IgnitionSignalStatus.TimedOut);
foreach (var r in timedOut)
{
    logger.LogWarning(
        "⏱ {Name} timed out (timeout: {Timeout:F0}ms, elapsed: {Elapsed:F0}ms)",
        r.Name,
        r.Timeout?.TotalMilliseconds ?? 0,
        r.Duration.TotalMilliseconds);
}
```

### Inspecting Skipped Signals (DAG Mode)

```csharp
var skipped = result.Results.Where(r => r.Status == IgnitionSignalStatus.Skipped);
foreach (var r in skipped)
{
    logger.LogWarning(
        "⊘ {Name} skipped due to failed dependencies: {Dependencies}",
        r.Name,
        string.Join(", ", r.FailedDependencies));
}
```

### Computing Summary Statistics

```csharp
var summary = new
{
    Total = result.Results.Count,
    Succeeded = result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded),
    Failed = result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed),
    TimedOut = result.Results.Count(r => r.Status == IgnitionSignalStatus.TimedOut),
    Skipped = result.Results.Count(r => r.Status == IgnitionSignalStatus.Skipped),
    TotalDuration = result.TotalDuration.TotalSeconds,
    AvgDuration = result.Results.Average(r => r.Duration.TotalMilliseconds),
    MaxDuration = result.Results.Max(r => r.Duration.TotalMilliseconds),
    MinDuration = result.Results.Min(r => r.Duration.TotalMilliseconds)
};

logger.LogInformation(
    "Ignition summary: {Succeeded}/{Total} succeeded, {Failed} failed, {TimedOut} timed out, {Skipped} skipped. " +
    "Total: {TotalDuration:F2}s, Avg: {AvgDuration:F0}ms, Max: {MaxDuration:F0}ms",
    summary.Succeeded, summary.Total, summary.Failed, summary.TimedOut, summary.Skipped,
    summary.TotalDuration, summary.AvgDuration, summary.MaxDuration);
```

## Timeline Export (Gantt-like Output)

Export a structured timeline of signal execution for analysis, visualization, and debugging.

### Basic Export

```csharp
var result = await coordinator.GetResultAsync();

// Export to timeline object
var timeline = result.ExportTimeline(
    executionMode: "Parallel",
    globalTimeout: TimeSpan.FromSeconds(30));

// Export to JSON
var json = timeline.ToJson(indented: true);
```

### Console Visualization

Display a Gantt-like ASCII visualization:

```csharp
// Export and display
var timeline = result.ExportTimeline();
timeline.WriteToConsole();

// Or get as string
string output = timeline.ToConsoleString();
Console.WriteLine(output);
```

Example output:

```text
╔══════════════════════════════════════════════════════════════════════════════╗
║                    IGNITION TIMELINE                                         ║
╠══════════════════════════════════════════════════════════════════════════════╣
║ Total Duration:     1215.5ms                                               ║
║ Timed Out:      NO                                                         ║
║ Execution Mode: Parallel                                                   ║
╠══════════════════════════════════════════════════════════════════════════════╣
║ SIGNAL TIMELINE (Gantt View)                                                 ║
║    0         243       486       729       972           1215ms             ║
║    |---------|---------|---------|---------|---------|                      ║
║ ✅ external-service     [████████████████████████                ] 602ms    ║
║ ✅ configuration-load   [█████████████████                       ] 430ms    ║
║ ✅ cache-warmup         [█████████████████████████████████████████] 1201ms  ║
║ ✅ database-connection  [█████████████████████████████████       ] 801ms    ║
╠══════════════════════════════════════════════════════════════════════════════╣
║ SUMMARY                                                                      ║
║   Total Signals:    4                                                        ║
║   ✅ Succeeded:     4                                                        ║
║   Max Concurrency:  4                                                        ║
║   🐢 Slowest:       cache-warmup (1201ms)                                   ║
║   🚀 Fastest:       configuration-load (430ms)                              ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

### Timeline Properties

```csharp
var timeline = result.ExportTimeline();

// Metadata
double totalMs = timeline.TotalDurationMs;
bool timedOut = timeline.TimedOut;
string? mode = timeline.ExecutionMode;

// Events (per-signal timing)
foreach (var e in timeline.Events)
{
    string name = e.SignalName;
    string status = e.Status;
    double startMs = e.StartMs;   // Relative to ignition start
    double endMs = e.EndMs;       // Relative to ignition start
    double durationMs = e.DurationMs;
    int? concurrentGroup = e.ConcurrentGroup;
}

// Summary statistics
var summary = timeline.Summary;
int total = summary.TotalSignals;
int succeeded = summary.SucceededCount;
int maxConcurrency = summary.MaxConcurrency;
string? slowest = summary.SlowestSignal;
double? slowestMs = summary.SlowestDurationMs;
```

### JSON Schema (v1.0)

```json
{
  "schemaVersion": "1.0",
  "totalDurationMs": 1215.5,
  "timedOut": false,
  "executionMode": "Parallel",
  "globalTimeoutMs": 30000,
  "startedAt": "2025-01-15T10:30:00.000Z",
  "completedAt": "2025-01-15T10:30:01.215Z",
  "events": [
    {
      "signalName": "database-connection",
      "status": "Succeeded",
      "startMs": 0,
      "endMs": 801,
      "durationMs": 801,
      "concurrentGroup": 1
    }
  ],
  "boundaries": [
    { "type": "GlobalTimeoutConfigured", "timeMs": 30000 },
    { "type": "IgnitionComplete", "timeMs": 1215.5 }
  ],
  "summary": {
    "totalSignals": 4,
    "succeededCount": 4,
    "failedCount": 0,
    "timedOutCount": 0,
    "maxConcurrency": 4,
    "slowestSignal": "cache-warmup",
    "slowestDurationMs": 1201,
    "fastestSignal": "configuration-load",
    "fastestDurationMs": 430,
    "averageDurationMs": 758.5
  }
}
```

### Use Cases

1. **Startup Debugging**: Identify slow signals or bottlenecks
2. **Container Warmup Analysis**: Profile Kubernetes/Docker startup
3. **CI Timing Regression Detection**: Compare JSON exports between builds
4. **Concurrent Execution Visualization**: See parallel execution patterns
5. **External Tool Integration**: Export to Chrome DevTools, Perfetto, etc.

### Saving Timelines for Analysis

```csharp
var result = await coordinator.GetResultAsync();
var timeline = result.ExportTimeline(
    executionMode: "Parallel",
    globalTimeout: options.GlobalTimeout);

// Save to file
var json = timeline.ToJson(indented: true);
await File.WriteAllTextAsync($"timeline-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json", json);

// Or log summary
logger.LogInformation(
    "Startup: {Duration:F0}ms, Concurrency: {Concurrency}, Slowest: {Slowest} ({SlowestMs:F0}ms)",
    timeline.TotalDurationMs,
    timeline.Summary?.MaxConcurrency,
    timeline.Summary?.SlowestSignal,
    timeline.Summary?.SlowestDurationMs);
```

## Slow Signal Logging

Automatically identify performance bottlenecks by logging the slowest signals.

### Configuration

```csharp
builder.Services.AddIgnition(options =>
{
    options.SlowHandleLogCount = 5; // Log top 5 slowest signals
});
```

### Log Output

Ignition automatically logs the N slowest signals:

```text
[Information] Top 5 slowest ignition signals:
[Information]   1. cache-warmup: 4850ms (Succeeded) - 48.5% of total time
[Information]   2. search-index: 3200ms (Succeeded) - 32.0% of total time
[Information]   3. database: 1500ms (Succeeded) - 15.0% of total time
[Information]   4. authentication: 350ms (Succeeded) - 3.5% of total time
[Information]   5. configuration: 100ms (Succeeded) - 1.0% of total time
```

### Use Cases

- **Development**: Identify optimization opportunities
- **Production**: Detect regressions or environment-specific slowness
- **Troubleshooting**: Pinpoint which signal exceeded global timeout

## Structured Logging

### Logging in Signals

Use dependency injection to get a logger:

```csharp
public class DatabaseSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseSignal> _logger;
    private readonly IDbConnection _connection;

    public DatabaseSignal(ILogger<DatabaseSignal> logger, IDbConnection connection)
    {
        _logger = logger;
        _connection = connection;
    }

    public string Name => "database";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public async Task WaitAsync(CancellationToken ct)
    {
        _logger.LogInformation("Connecting to database...");

        try
        {
            await _connection.OpenAsync(ct);
            _logger.LogInformation("Database connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database");
            throw;
        }
    }
}
```

### Logging Best Practices

#### Use Structured Data

```csharp
_logger.LogInformation(
    "Cache warmed with {ItemCount} items in {Duration:F2}s",
    itemCount,
    duration.TotalSeconds);
```

#### Log Progress for Long Operations

```csharp
public async Task WaitAsync(CancellationToken ct)
{
    _logger.LogInformation("Starting cache warmup");

    _logger.LogDebug("Loading product catalog...");
    await LoadProductsAsync(ct);

    _logger.LogDebug("Loading user preferences...");
    await LoadPreferencesAsync(ct);

    _logger.LogDebug("Building recommendation cache...");
    await BuildRecommendationsAsync(ct);

    _logger.LogInformation("Cache warmup complete");
}
```

#### Use Log Levels Appropriately

```csharp
_logger.LogDebug("Starting signal {Name}", Name);        // Development detail
_logger.LogInformation("Signal {Name} completed", Name); // Normal operation
_logger.LogWarning("Signal {Name} slow", Name);          // Degraded performance
_logger.LogError(ex, "Signal {Name} failed", Name);      // Recoverable error
_logger.LogCritical(ex, "Critical failure in {Name}", Name); // Unrecoverable
```

### Logging Configuration

#### Minimum Level

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Information); // Production
// builder.Logging.SetMinimumLevel(LogLevel.Debug);   // Development
```

#### Filtering

```csharp
builder.Logging.AddFilter("Veggerby.Ignition", LogLevel.Information);
builder.Logging.AddFilter("MyApp.Signals", LogLevel.Debug);
```

#### Sinks

```csharp
// Console
builder.Logging.AddConsole();

// Application Insights
builder.Logging.AddApplicationInsights(config["ApplicationInsights:InstrumentationKey"]);

// Serilog
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/ignition-.txt", rollingInterval: RollingInterval.Day);
});
```

## Activity Tracing

Ignition integrates with .NET's `Activity` API for distributed tracing via OpenTelemetry.

### Enable Tracing

```csharp
builder.Services.AddIgnition(options =>
{
    options.EnableTracing = true; // Emit Activity events
});
```

### ActivitySource Details

- **Source name**: `Veggerby.Ignition.IgnitionCoordinator`
- **Activity name**: `Ignition.WaitAll`
- **Operation name**: `WaitAll`

### OpenTelemetry Integration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Veggerby.Ignition.IgnitionCoordinator")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            });
    });
```

### Trace Output

The `Ignition.WaitAll` activity includes:

- **Span duration**: Total ignition time
- **Tags**: Configuration values (policy, timeout, execution mode)
- **Events**: Signal start/completion events
- **Child spans**: Individual signal executions (if instrumented)

### Custom Signal Tracing

Add tracing within signals:

```csharp
public class DatabaseSignal : IIgnitionSignal
{
    private static readonly ActivitySource s_activitySource = new("MyApp.Signals");

    public async Task WaitAsync(CancellationToken ct)
    {
        using var activity = s_activitySource.StartActivity("Database.Connect");
        activity?.SetTag("database.name", _databaseName);

        try
        {
            await _connection.OpenAsync(ct);
            activity?.SetTag("database.connected", true);
        }
        catch (Exception ex)
        {
            activity?.SetTag("database.connected", false);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}

// Register in OpenTelemetry
tracing.AddSource("MyApp.Signals");
```

### Trace Visualization

View traces in tools like:

- Jaeger
- Zipkin
- Application Insights
- Honeycomb
- Datadog

## Health Check Integration

Ignition automatically registers a health check named `ignition-readiness`.

### Registration

Health check is registered automatically when you call `AddIgnition`:

```csharp
builder.Services.AddIgnition(options => { /* ... */ });
// "ignition-readiness" health check now available
```

### Map Health Endpoint

```csharp
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");
```

### Health Status Mapping

| Condition | Status | Description |
|-----------|--------|-------------|
| All signals succeeded | `Healthy` | Complete successful startup |
| Soft global timeout (no failures) | `Degraded` | Startup slow but successful |
| Hard global timeout (cancellation) | `Unhealthy` | Deadline enforcement triggered |
| One or more signal failures | `Unhealthy` | Critical component failed |
| Exception during evaluation | `Unhealthy` | Unexpected error |

### Health Check Response

```json
{
  "status": "Healthy",
  "results": {
    "ignition-readiness": {
      "status": "Healthy",
      "description": "All ignition signals completed successfully",
      "data": {
        "signalCount": 5,
        "succeededCount": 5,
        "failedCount": 0,
        "timedOutCount": 0,
        "totalDuration": "00:00:03.5420000",
        "timedOut": false
      }
    }
  }
}
```

### Kubernetes Integration

Use health check for liveness/readiness probes:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: myapp
spec:
  containers:
  - name: myapp
    image: myapp:latest
    ports:
    - containerPort: 8080
    livenessProbe:
      httpGet:
        path: /health/live
        port: 8080
      initialDelaySeconds: 10
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 5
      periodSeconds: 5
```

### Custom Health Check Logic

Extend health check with custom logic:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("custom-check", () =>
    {
        var coordinator = serviceProvider.GetRequiredService<IIgnitionCoordinator>();
        var result = coordinator.GetResultAsync().GetAwaiter().GetResult();

        var criticalSignals = new[] { "database", "authentication" };
        var criticalFailed = result.Results
            .Where(r => criticalSignals.Contains(r.Name))
            .Any(r => r.Status != IgnitionSignalStatus.Succeeded);

        if (criticalFailed)
        {
            return HealthCheckResult.Unhealthy("Critical signals failed");
        }

        if (result.TimedOut)
        {
            return HealthCheckResult.Degraded("Startup degraded");
        }

        return HealthCheckResult.Healthy("All critical signals ready");
    });
```

## Monitoring Startup Performance

### Metrics to Track

1. **Startup duration**: `result.TotalDuration`
2. **Signal success rate**: `succeeded / total`
3. **Signal failure count**: Count of `Failed` status
4. **Timeout occurrences**: Count of `TimedOut` status
5. **Slowest signal**: Max `Duration` across signals

### Exporting Metrics

#### Prometheus

```csharp
// After ignition
var result = await coordinator.GetResultAsync();

// Export metrics
_metrics.Gauge("ignition_total_duration_seconds", result.TotalDuration.TotalSeconds);
_metrics.Counter("ignition_signals_total", result.Results.Count);
_metrics.Counter("ignition_signals_succeeded", result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded));
_metrics.Counter("ignition_signals_failed", result.Results.Count(r => r.Status == IgnitionSignalStatus.Failed));
_metrics.Counter("ignition_signals_timed_out", result.Results.Count(r => r.Status == IgnitionSignalStatus.TimedOut));

foreach (var r in result.Results)
{
    _metrics.Gauge(
        "ignition_signal_duration_seconds",
        r.Duration.TotalSeconds,
        new[] { new KeyValuePair<string, object?>("signal", r.Name) });
}
```

#### Application Insights

```csharp
var telemetry = serviceProvider.GetRequiredService<TelemetryClient>();
var result = await coordinator.GetResultAsync();

telemetry.TrackMetric("IgnitionDuration", result.TotalDuration.TotalSeconds);
telemetry.TrackEvent("IgnitionCompleted", new Dictionary<string, string>
{
    ["TimedOut"] = result.TimedOut.ToString(),
    ["SuccessRate"] = $"{result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded) / (double)result.Results.Count:P0}"
});

foreach (var r in result.Results)
{
    telemetry.TrackMetric($"Signal.{r.Name}.Duration", r.Duration.TotalMilliseconds);

    if (r.Status != IgnitionSignalStatus.Succeeded)
    {
        telemetry.TrackEvent($"Signal.{r.Name}.{r.Status}", new Dictionary<string, string>
        {
            ["Exception"] = r.Exception?.Message ?? "none"
        });
    }
}
```

### Alerting on Startup Failures

Set up alerts for:

1. **Critical signal failures**: Alert immediately
2. **Timeout increases**: Trend indicates degradation
3. **Success rate drop**: Below threshold (e.g., <95%)
4. **Slow signals**: Duration exceeds p95 baseline

#### Example: Prometheus Alert Rule

```yaml
groups:
- name: ignition
  rules:
  - alert: IgnitionCriticalSignalFailed
    expr: ignition_signals_failed > 0
    for: 1m
    labels:
      severity: critical
    annotations:
      summary: "Ignition signal failure detected"
      description: "{{ $value }} signals failed during startup"

  - alert: IgnitionTimeoutIncreased
    expr: ignition_total_duration_seconds > 60
    for: 5m
    labels:
      severity: warning
    annotations:
      summary: "Ignition startup slow"
      description: "Startup took {{ $value }}s (threshold: 60s)"
```

## Dashboard Examples

### Grafana Dashboard Panels

#### Startup Duration Trend

```promql
ignition_total_duration_seconds
```

Panel type: Graph (time series)

#### Signal Success Rate

```promql
(ignition_signals_succeeded / ignition_signals_total) * 100
```

Panel type: Gauge (percentage)

#### Signal Duration Heatmap

```promql
ignition_signal_duration_seconds
```

Panel type: Heatmap

#### Recent Failures

```promql
increase(ignition_signals_failed[1h])
```

Panel type: Bar chart

### Application Insights Dashboard

Create custom workbook with:

1. **Startup time distribution** (histogram)
2. **Signal status breakdown** (pie chart)
3. **Top slowest signals** (table)
4. **Failure timeline** (line chart)
5. **Timeout occurrences** (count over time)

## Complete Observability Stack Example

### ASP.NET Core Application with Full Observability

```csharp
var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.ApplicationInsights(
            context.Configuration["ApplicationInsights:InstrumentationKey"],
            TelemetryConverter.Traces);
});

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Veggerby.Ignition.IgnitionCoordinator")
            .AddSource("MyApp.Signals")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(builder.Configuration["OpenTelemetry:Endpoint"]);
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    });

// Ignition
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
    options.EnableTracing = true;
    options.SlowHandleLogCount = 5;
});

// Signals
builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Prometheus metrics
app.MapPrometheusScrapingEndpoint();

// Health checks
app.MapHealthChecks("/health");

// Execute ignition
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

// Log and export results
var result = await coordinator.GetResultAsync();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var telemetry = app.Services.GetRequiredService<TelemetryClient>();

logger.LogInformation(
    "Ignition completed in {Duration:F2}s. {Succeeded}/{Total} succeeded",
    result.TotalDuration.TotalSeconds,
    result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded),
    result.Results.Count);

telemetry.TrackMetric("IgnitionDuration", result.TotalDuration.TotalSeconds);
telemetry.TrackEvent("IgnitionCompleted", new Dictionary<string, string>
{
    ["Policy"] = "BestEffort",
    ["TimedOut"] = result.TimedOut.ToString(),
    ["SignalCount"] = result.Results.Count.ToString()
});

await app.RunAsync();
```

## Troubleshooting with Observability

### Debug Slow Startup

1. Enable slow signal logging: `options.SlowHandleLogCount = 10;`
2. Check log output for top slow signals
3. Inspect per-signal durations in result
4. Add detailed logging within slow signals
5. Profile signal implementations

### Debug Failures

1. Check `result.Results` for `Failed` status
2. Inspect `Exception` property for error details
3. Review logs for signal-specific error messages
4. Enable tracing to see execution flow
5. Check health endpoint for failure details

### Debug Timeouts

1. Compare `Duration` vs `Timeout` for timed-out signals
2. Enable tracing to see when timeout occurred
3. Check if timeout is per-signal or global
4. Review `CancelOnGlobalTimeout` and `CancelIndividualOnTimeout` settings
5. Increase timeouts temporarily to observe actual duration

### Debug DAG Issues

1. Check for `Skipped` status in results
2. Inspect `FailedDependencies` to trace failure propagation
3. Enable debug logging for dependency resolution
4. Verify dependency graph configuration
5. Use graph query methods to inspect structure

## Related Topics

- [Getting Started](getting-started.md) - Basic logging setup
- [Timeout Management](timeout-management.md) - Timeout diagnostics
- [Policies](policies.md) - Policy outcome inspection
- [Metrics Integration](metrics-integration.md) - Production monitoring and alerting
- [Performance Guide](performance.md) - Performance profiling
