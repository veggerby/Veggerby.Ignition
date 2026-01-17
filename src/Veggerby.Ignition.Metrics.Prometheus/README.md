# Veggerby.Ignition.Metrics.Prometheus

Official Prometheus metrics implementation for Veggerby.Ignition.

## Installation

```bash
dotnet add package Veggerby.Ignition.Metrics.Prometheus
```

## Quick Start

```csharp
using Veggerby.Ignition;
using Veggerby.Ignition.Metrics.Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Register ignition
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
});

// Register Prometheus metrics
builder.Services.AddPrometheusIgnitionMetrics();

var app = builder.Build();

// Expose metrics endpoint
app.MapMetrics(); // Available at /metrics

// Wait for ignition
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

await app.RunAsync();
```

## Metrics Exposed

This package exposes the following Prometheus metrics:

### `ignition_signal_duration_seconds`

**Type:** Histogram

**Description:** Duration of individual signal execution in seconds

**Labels:**
- `signal_name` - Name of the signal
- `status` - Always "completed"

**Buckets:** Exponential from 1ms to ~32s (1ms, 2ms, 4ms, 8ms, 16ms, 32ms, 64ms, 128ms, 256ms, 512ms, 1s, 2s, 4s, 8s, 16s, 32s)

### `ignition_signal_total`

**Type:** Counter

**Description:** Total number of signal executions by status

**Labels:**
- `signal_name` - Name of the signal
- `status` - Status: `Succeeded`, `Failed`, `TimedOut`, `Skipped`, or `Cancelled`

### `ignition_total_duration_seconds`

**Type:** Histogram

**Description:** Total duration of the entire ignition process

**Buckets:** Exponential from 100ms to ~3276s

## Integration with Grafana

### Example PromQL Queries

**Signal success rate:**
```promql
sum(rate(ignition_signal_total{status="Succeeded"}[5m])) by (signal_name)
/ 
sum(rate(ignition_signal_total[5m])) by (signal_name)
```

**95th percentile signal duration:**
```promql
histogram_quantile(0.95, sum(rate(ignition_signal_duration_seconds_bucket[5m])) by (signal_name, le))
```

**Total ignition duration:**
```promql
histogram_quantile(0.95, sum(rate(ignition_total_duration_seconds_bucket[5m])) by (le))
```

## Requirements

- .NET 8.0 or later
- Veggerby.Ignition
- prometheus-net

## License

MIT
