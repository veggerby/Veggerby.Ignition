# Metrics Integration Sample

**Complexity**: Intermediate  
**Type**: Console Application  
**Focus**: Observability integration with OpenTelemetry and Prometheus

## Overview

This sample demonstrates how to integrate Veggerby.Ignition with observability platforms using the `IIgnitionMetrics` interface.

## What It Demonstrates

### Three Metrics Backends

1. **OpenTelemetry Metrics** - Industry-standard observability with console exporter
2. **Prometheus.NET** - Popular metrics library with scrape endpoint support
3. **Custom Metrics** - In-memory collection for custom reporting

### Key Concepts

- Implementing `IIgnitionMetrics` interface
- Recording signal durations and statuses
- Exporting metrics to external systems
- Custom metrics aggregation

## Prerequisites

- .NET 10.0 SDK
- No external services required

## How to Run

```bash
cd samples/Metrics
dotnet run
```

## Implementation Patterns

### 1. OpenTelemetry Implementation

```csharp
public class OpenTelemetryIgnitionMetrics : IIgnitionMetrics
{
    private readonly Meter _meter;
    private readonly Histogram<double> _signalDuration;
    
    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("signal.name", name));
    }
}
```

### 2. Prometheus.NET Implementation

```csharp
public class PrometheusIgnitionMetrics : IIgnitionMetrics
{
    private readonly Histogram _signalDuration;
    
    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.WithLabels(name).Observe(duration.TotalMilliseconds);
    }
}
```

### 3. Registering Metrics

```csharp
services.AddIgnition(options =>
{
    options.Metrics = new OpenTelemetryIgnitionMetrics();
});
```

## Use Cases

- Production monitoring and alerting
- Performance analysis and optimization
- SLO/SLA tracking for startup time
- Detecting regressions in CI/CD
- Custom dashboard integration

## Related Samples

- [TimelineExport](../TimelineExport/) - Startup analysis and visualization
- [WebApi](../WebApi/) - Production web application patterns

## Further Reading

- [IIgnitionMetrics API](../../src/Veggerby.Ignition/Metrics/IIgnitionMetrics.cs)
- [OpenTelemetry Documentation](https://opentelemetry.io/)
- [Prometheus Documentation](https://prometheus.io/)
