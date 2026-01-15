# Metrics Integration Guide

This guide covers integrating Veggerby.Ignition with observability systems for production monitoring and alerting.

## Table of Contents

- [Overview](#overview)
- [IIgnitionMetrics Interface](#iignitionmetrics-interface)
- [OpenTelemetry Integration](#opentelemetry-integration)
- [Prometheus.NET Integration](#prometheusnet-integration)
- [Custom Metrics Backend](#custom-metrics-backend)
- [Metrics Emitted by Coordinator](#metrics-emitted-by-coordinator)
- [Best Practices](#best-practices)

## Overview

Veggerby.Ignition provides an abstraction for metrics collection via the `IIgnitionMetrics` interface, enabling integration with any observability system without adding dependencies. The default implementation is a no-op (`NullIgnitionMetrics`) with zero overhead.

### Why Metrics Matter

Startup metrics enable:

- **Alerting**: Trigger alerts when startup exceeds SLA thresholds
- **Trending**: Track startup performance degradation over time
- **Debugging**: Identify slow signals causing startup delays
- **Capacity planning**: Correlate startup time with load/resources
- **SLO tracking**: Monitor startup reliability percentages

### Metrics Collection Points

The coordinator records metrics at these points:

1. **Per-signal duration**: When each signal completes (success, failure, or timeout)
2. **Per-signal status**: Outcome status (Succeeded, Failed, TimedOut, Skipped)
3. **Total duration**: Overall coordinator execution time

## IIgnitionMetrics Interface

```csharp
namespace Veggerby.Ignition.Metrics;

/// <summary>
/// Abstraction for recording ignition metrics, enabling integration with observability systems
/// (OpenTelemetry, Prometheus, App Metrics, etc.) without adding any external dependencies.
/// </summary>
public interface IIgnitionMetrics
{
    /// <summary>
    /// Records the duration of a signal execution.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="duration">The elapsed time for the signal execution.</param>
    void RecordSignalDuration(string name, TimeSpan duration);

    /// <summary>
    /// Records the completion status of a signal.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="status">The outcome status of the signal.</param>
    void RecordSignalStatus(string name, IgnitionSignalStatus status);

    /// <summary>
    /// Records the total duration of the ignition process.
    /// </summary>
    /// <param name="duration">The total elapsed time for all signals.</param>
    void RecordTotalDuration(TimeSpan duration);
}
```

### Implementation Requirements

- **Thread-safe**: Methods may be called from multiple concurrent signals
- **Non-blocking**: Avoid blocking operations (use fire-and-forget or buffering)
- **Minimal allocations**: Optimize for performance (hot path)
- **Exception handling**: Catch exceptions internally; don't propagate to coordinator

## OpenTelemetry Integration

OpenTelemetry is the recommended observability standard for cloud-native applications.

### Installation

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting
```

### Implementation

```csharp
using System.Diagnostics.Metrics;
using Veggerby.Ignition.Metrics;

/// <summary>
/// OpenTelemetry metrics adapter for Veggerby.Ignition.
/// </summary>
public sealed class OpenTelemetryIgnitionMetrics : IIgnitionMetrics
{
    private readonly Meter _meter;
    private readonly Histogram<double> _signalDuration;
    private readonly Counter<long> _signalStatus;
    private readonly Histogram<double> _totalDuration;

    public OpenTelemetryIgnitionMetrics(string meterName = "Veggerby.Ignition")
    {
        _meter = new Meter(meterName, "1.0.0");

        // Histogram for signal durations (in milliseconds)
        _signalDuration = _meter.CreateHistogram<double>(
            name: "ignition.signal.duration",
            unit: "ms",
            description: "Duration of individual ignition signal execution");

        // Counter for signal status outcomes
        _signalStatus = _meter.CreateCounter<long>(
            name: "ignition.signal.status",
            unit: "{signal}",
            description: "Count of ignition signal outcomes by status");

        // Histogram for total ignition duration (in milliseconds)
        _totalDuration = _meter.CreateHistogram<double>(
            name: "ignition.total.duration",
            unit: "ms",
            description: "Total duration of ignition process");
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("signal.name", name));
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatus.Add(
            1,
            new KeyValuePair<string, object?>("signal.name", name),
            new KeyValuePair<string, object?>("signal.status", status.ToString()));
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Record(duration.TotalMilliseconds);
    }
}
```

### Configuration

```csharp
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// Register OpenTelemetry metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Veggerby.Ignition")
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri("http://otel-collector:4317");
            });
    });

// Register ignition metrics adapter
builder.Services.AddSingleton<IIgnitionMetrics, OpenTelemetryIgnitionMetrics>();

// Configure ignition with metrics
builder.Services.AddIgnition(opts =>
{
    opts.GlobalTimeout = TimeSpan.FromSeconds(30);
    opts.Metrics = builder.Services.BuildServiceProvider().GetRequiredService<IIgnitionMetrics>();
});
```

### Querying Metrics

**PromQL (via Prometheus scrape endpoint):**

```promql
# Average signal duration by signal name
avg by (signal_name) (ignition_signal_duration_milliseconds)

# P95 signal duration
histogram_quantile(0.95, ignition_signal_duration_milliseconds_bucket)

# Signal failure rate
rate(ignition_signal_status_total{signal_status="Failed"}[5m])

# Total ignition duration P99
histogram_quantile(0.99, ignition_total_duration_milliseconds_bucket)
```

**Full Example with ASP.NET Core:**

```csharp
using OpenTelemetry.Metrics;
using Veggerby.Ignition.Metrics;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Veggerby.Ignition")
            .AddPrometheusExporter(); // Expose /metrics endpoint
    });

// Ignition metrics adapter
builder.Services.AddSingleton<IIgnitionMetrics, OpenTelemetryIgnitionMetrics>();

// Ignition configuration
builder.Services.AddIgnition(opts =>
{
    opts.GlobalTimeout = TimeSpan.FromSeconds(30);
});

// Configure metrics from DI
builder.Services.Configure<IgnitionOptions>(opts =>
{
    opts.Metrics = builder.Services.BuildServiceProvider().GetRequiredService<IIgnitionMetrics>();
});

var app = builder.Build();

// Map Prometheus scrape endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();
```

## Prometheus.NET Integration

Direct Prometheus.NET integration without OpenTelemetry.

### Installation

```bash
dotnet add package prometheus-net
dotnet add package prometheus-net.AspNetCore
```

### Implementation

```csharp
using Prometheus;
using Veggerby.Ignition.Metrics;

/// <summary>
/// Prometheus.NET metrics adapter for Veggerby.Ignition.
/// </summary>
public sealed class PrometheusIgnitionMetrics : IIgnitionMetrics
{
    private readonly Histogram _signalDuration;
    private readonly Counter _signalStatus;
    private readonly Histogram _totalDuration;

    public PrometheusIgnitionMetrics()
    {
        // Histogram for signal durations (in seconds)
        _signalDuration = Metrics.CreateHistogram(
            name: "ignition_signal_duration_seconds",
            help: "Duration of individual ignition signal execution",
            labelNames: new[] { "signal_name" },
            buckets: new[] { 0.01, 0.05, 0.1, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0 });

        // Counter for signal status outcomes
        _signalStatus = Metrics.CreateCounter(
            name: "ignition_signal_status_total",
            help: "Count of ignition signal outcomes by status",
            labelNames: new[] { "signal_name", "signal_status" });

        // Histogram for total duration (in seconds)
        _totalDuration = Metrics.CreateHistogram(
            name: "ignition_total_duration_seconds",
            help: "Total duration of ignition process",
            buckets: new[] { 0.1, 0.5, 1.0, 5.0, 10.0, 30.0, 60.0, 120.0, 300.0 });
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.WithLabels(name).Observe(duration.TotalSeconds);
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatus.WithLabels(name, status.ToString()).Inc();
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Observe(duration.TotalSeconds);
    }
}
```

### Configuration

```csharp
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Register Prometheus metrics adapter
builder.Services.AddSingleton<IIgnitionMetrics, PrometheusIgnitionMetrics>();

// Configure ignition
builder.Services.AddIgnition(opts =>
{
    opts.GlobalTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.Configure<IgnitionOptions>(opts =>
{
    opts.Metrics = builder.Services.BuildServiceProvider().GetRequiredService<IIgnitionMetrics>();
});

var app = builder.Build();

// Enable Prometheus HTTP metrics endpoint
app.UseHttpMetrics();
app.MapMetrics(); // Expose /metrics endpoint

app.Run();
```

### PromQL Queries

```promql
# Average signal duration by name
avg by (signal_name) (rate(ignition_signal_duration_seconds_sum[5m]) / rate(ignition_signal_duration_seconds_count[5m]))

# P95 signal duration
histogram_quantile(0.95, rate(ignition_signal_duration_seconds_bucket[5m]))

# Signal failure rate (failures per second)
rate(ignition_signal_status_total{signal_status="Failed"}[5m])

# Total ignition duration P99
histogram_quantile(0.99, rate(ignition_total_duration_seconds_bucket[5m]))

# Signals timing out (rate)
rate(ignition_signal_status_total{signal_status="TimedOut"}[5m])
```

### Grafana Dashboard Example

Create a Grafana dashboard with these panels:

**Panel 1: Average Startup Duration (Gauge)**

```promql
avg(ignition_total_duration_seconds)
```

**Panel 2: Signal Duration Breakdown (Bar Chart)**

```promql
avg by (signal_name) (rate(ignition_signal_duration_seconds_sum[5m]) / rate(ignition_signal_duration_seconds_count[5m]))
```

**Panel 3: Signal Success Rate (Graph)**

```promql
sum by (signal_status) (rate(ignition_signal_status_total[5m]))
```

**Panel 4: P95 Startup Duration Over Time (Graph)**

```promql
histogram_quantile(0.95, rate(ignition_total_duration_seconds_bucket[5m]))
```

## Custom Metrics Backend

Implement `IIgnitionMetrics` for any observability system.

### Example: Application Insights

```csharp
using Microsoft.ApplicationInsights;
using Veggerby.Ignition.Metrics;

/// <summary>
/// Application Insights metrics adapter for Veggerby.Ignition.
/// </summary>
public sealed class ApplicationInsightsMetrics : IIgnitionMetrics
{
    private readonly TelemetryClient _telemetryClient;

    public ApplicationInsightsMetrics(TelemetryClient telemetryClient)
    {
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _telemetryClient.TrackMetric(
            name: "Ignition.Signal.Duration",
            value: duration.TotalMilliseconds,
            properties: new Dictionary<string, string>
            {
                ["SignalName"] = name
            });
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _telemetryClient.TrackMetric(
            name: "Ignition.Signal.Status",
            value: 1,
            properties: new Dictionary<string, string>
            {
                ["SignalName"] = name,
                ["Status"] = status.ToString()
            });
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _telemetryClient.TrackMetric(
            name: "Ignition.Total.Duration",
            value: duration.TotalMilliseconds);
    }
}
```

### Example: Datadog

```csharp
using StatsdClient;
using Veggerby.Ignition.Metrics;

/// <summary>
/// Datadog (StatsD) metrics adapter for Veggerby.Ignition.
/// </summary>
public sealed class DatadogIgnitionMetrics : IIgnitionMetrics
{
    private readonly IDogStatsd _dogStatsd;

    public DatadogIgnitionMetrics(IDogStatsd dogStatsd)
    {
        _dogStatsd = dogStatsd ?? throw new ArgumentNullException(nameof(dogStatsd));
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _dogStatsd.Histogram(
            "ignition.signal.duration",
            duration.TotalMilliseconds,
            tags: new[] { $"signal:{name}" });
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _dogStatsd.Increment(
            "ignition.signal.status",
            tags: new[] { $"signal:{name}", $"status:{status}" });
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _dogStatsd.Histogram("ignition.total.duration", duration.TotalMilliseconds);
    }
}
```

### Example: In-Memory (Testing/Development)

```csharp
using System.Collections.Concurrent;
using Veggerby.Ignition.Metrics;

/// <summary>
/// In-memory metrics collector for testing and development.
/// </summary>
public sealed class InMemoryIgnitionMetrics : IIgnitionMetrics
{
    private readonly ConcurrentBag<SignalDurationRecord> _signalDurations = new();
    private readonly ConcurrentBag<SignalStatusRecord> _signalStatuses = new();
    private readonly ConcurrentBag<TimeSpan> _totalDurations = new();

    public IReadOnlyList<SignalDurationRecord> SignalDurations => _signalDurations.ToList();
    public IReadOnlyList<SignalStatusRecord> SignalStatuses => _signalStatuses.ToList();
    public IReadOnlyList<TimeSpan> TotalDurations => _totalDurations.ToList();

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDurations.Add(new SignalDurationRecord(name, duration));
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatuses.Add(new SignalStatusRecord(name, status));
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDurations.Add(duration);
    }

    public void Reset()
    {
        _signalDurations.Clear();
        _signalStatuses.Clear();
        _totalDurations.Clear();
    }

    public record SignalDurationRecord(string Name, TimeSpan Duration);
    public record SignalStatusRecord(string Name, IgnitionSignalStatus Status);
}
```

## Metrics Emitted by Coordinator

### Signal Duration

**Metric:** `ignition.signal.duration`

**Type:** Histogram

**Labels:**

- `signal.name` / `signal_name`: Name of the signal

**Unit:** Milliseconds (ms) or Seconds (s)

**Description:** Duration of individual signal execution from start to completion.

**When recorded:** After each signal completes (success, failure, or timeout).

**Example values:**

- `database-readiness`: 145ms
- `redis-readiness`: 23ms
- `rabbitmq-readiness`: 312ms

### Signal Status

**Metric:** `ignition.signal.status`

**Type:** Counter

**Labels:**

- `signal.name` / `signal_name`: Name of the signal
- `signal.status` / `signal_status`: Outcome status (`Succeeded`, `Failed`, `TimedOut`, `Skipped`)

**Unit:** Count (`{signal}`)

**Description:** Count of signal outcomes by status.

**When recorded:** After each signal completes.

**Example values:**

- `database-readiness`, `Succeeded`: 1
- `cache-warmup`, `TimedOut`: 1
- `optional-service`, `Skipped`: 1 (DAG mode)

### Total Duration

**Metric:** `ignition.total.duration`

**Type:** Histogram

**Labels:** None

**Unit:** Milliseconds (ms) or Seconds (s)

**Description:** Total elapsed time for the entire ignition process (all signals).

**When recorded:** Once, when `WaitAllAsync()` completes.

**Example values:**

- Total duration: 523ms (Parallel mode, 10 signals)
- Total duration: 1245ms (Sequential mode, 10 signals)

### Metric Recording Sequence

```text
1. Coordinator starts WaitAllAsync()
2. For each signal:
   a. Signal executes
   b. RecordSignalDuration(name, duration) called
   c. RecordSignalStatus(name, status) called
3. All signals complete
4. RecordTotalDuration(totalDuration) called
5. Coordinator returns result
```

## Best Practices

### 1. Use Histogram Buckets Appropriate for Startup

Choose buckets that reflect realistic startup times:

```csharp
// Good: Covers 10ms to 5min
buckets: new[] { 0.01, 0.05, 0.1, 0.5, 1.0, 5.0, 10.0, 30.0, 60.0, 300.0 }

// Bad: Too granular for startup metrics
buckets: new[] { 0.001, 0.002, 0.005, 0.01, 0.02 }
```

### 2. Add Custom Labels for Dimensionality

Extend metrics with environment/deployment context:

```csharp
public void RecordSignalDuration(string name, TimeSpan duration)
{
    _signalDuration.Record(
        duration.TotalMilliseconds,
        new KeyValuePair<string, object?>("signal.name", name),
        new KeyValuePair<string, object?>("environment", _environment.EnvironmentName),
        new KeyValuePair<string, object?>("region", _region));
}
```

### 3. Implement Exception Handling

Metrics should never crash the coordinator:

```csharp
public void RecordSignalDuration(string name, TimeSpan duration)
{
    try
    {
        _signalDuration.Record(duration.TotalMilliseconds, ...);
    }
    catch (Exception ex)
    {
        // Log but don't propagate
        _logger.LogWarning(ex, "Failed to record signal duration metric");
    }
}
```

### 4. Use Fire-and-Forget for Remote Backends

Avoid blocking coordinator on network I/O:

```csharp
public void RecordSignalDuration(string name, TimeSpan duration)
{
    // Fire-and-forget to avoid blocking
    _ = Task.Run(() =>
    {
        try
        {
            _httpClient.PostAsync("/metrics", ...);
        }
        catch
        {
            // Swallow exceptions
        }
    });
}
```

### 5. Configure Metrics via Options

Decouple metrics from coordinator registration:

```csharp
builder.Services.AddSingleton<IIgnitionMetrics, OpenTelemetryIgnitionMetrics>();

builder.Services.Configure<IgnitionOptions>(opts =>
{
    opts.GlobalTimeout = TimeSpan.FromSeconds(30);
    opts.Metrics = opts.ServiceProvider.GetRequiredService<IIgnitionMetrics>();
});
```

### 6. Alert on Critical Thresholds

Set up alerts for startup issues:

**Prometheus Alerting Rules:**

```yaml
groups:
  - name: ignition_alerts
    rules:
      # Alert if startup exceeds 60 seconds
      - alert: IgnitionSlowStartup
        expr: ignition_total_duration_seconds > 60
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Application startup is slow"
          description: "Startup took {{ $value }}s (threshold: 60s)"

      # Alert if any signal fails
      - alert: IgnitionSignalFailure
        expr: rate(ignition_signal_status_total{signal_status="Failed"}[5m]) > 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Ignition signal failure detected"
          description: "Signal {{ $labels.signal_name }} failed"

      # Alert if signal timeout rate exceeds 10%
      - alert: IgnitionHighTimeoutRate
        expr: (rate(ignition_signal_status_total{signal_status="TimedOut"}[5m]) / rate(ignition_signal_status_total[5m])) > 0.1
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High ignition signal timeout rate"
```

### 7. Dashboard Layout

Create dashboards with these sections:

**Overview:**

- Total startup duration (gauge)
- Signal count (counter)
- Success rate (graph)

**Signal Breakdown:**

- Duration by signal (bar chart)
- Status by signal (table)
- P95 duration by signal (graph)

**Trends:**

- Startup duration over time (graph)
- Failure rate over time (graph)
- Slow signal trends (heatmap)

## Related Documentation

- [Observability Guide](observability.md) - Logging, tracing, and health checks
- [Performance Guide](performance.md) - Performance tuning and optimization
- [Timeout Management](timeout-management.md) - Timeout configuration
- [Getting Started](getting-started.md) - Basic ignition setup
- [API Reference](api-reference.md) - IIgnitionMetrics interface reference
