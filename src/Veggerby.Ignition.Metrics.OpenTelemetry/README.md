# Veggerby.Ignition.Metrics.OpenTelemetry

Official OpenTelemetry metrics implementation for Veggerby.Ignition.

## Installation

```bash
dotnet add package Veggerby.Ignition.Metrics.OpenTelemetry
```

## Quick Start

```csharp
using OpenTelemetry.Metrics;
using Veggerby.Ignition;
using Veggerby.Ignition.Metrics.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// Register ignition
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
});

// Register OpenTelemetry metrics
builder.Services.AddOpenTelemetryIgnitionMetrics();

// Configure OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("Veggerby.Ignition")
        .AddPrometheusExporter());

var app = builder.Build();

// Wait for ignition
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

await app.RunAsync();
```

## Metrics Exposed

This package exposes the following OpenTelemetry metrics:

### `ignition.signal.duration`

**Type:** Histogram (double)

**Unit:** seconds (s)

**Description:** Duration of individual signal execution

**Tags:**
- `signal.name` - Name of the signal

### `ignition.signal.status`

**Type:** Counter (long)

**Description:** Total number of signal executions by status

**Tags:**
- `signal.name` - Name of the signal
- `signal.status` - Status: `Succeeded`, `Failed`, `TimedOut`, `Skipped`, or `Cancelled`

### `ignition.total.duration`

**Type:** Histogram (double)

**Unit:** seconds (s)

**Description:** Total duration of the entire ignition process

## Supported Exporters

This package is compatible with all standard OpenTelemetry exporters:

- **Prometheus** - `OpenTelemetry.Exporter.Prometheus.AspNetCore`
- **Console** - `OpenTelemetry.Exporter.Console`
- **OTLP** - `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- **Jaeger** - Via OTLP exporter
- **Zipkin** - Via OTLP exporter

### Example: Console Exporter

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("Veggerby.Ignition")
        .AddConsoleExporter());
```

### Example: OTLP Exporter

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter("Veggerby.Ignition")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
        }));
```

## Requirements

- .NET 8.0 or later
- Veggerby.Ignition
- OpenTelemetry

## License

MIT
