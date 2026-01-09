# Veggerby.Ignition.Marten

Marten document store readiness signals for Veggerby.Ignition - verify Marten readiness during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Marten
```

## Usage

### Basic Document Store Verification

```csharp
builder.Services.AddMarten(opts =>
{
    opts.Connection("Host=localhost;Database=mydb;Username=user;Password=pass");
});

builder.Services.AddIgnition();
builder.Services.AddMartenReadiness();

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Custom Options

```csharp
builder.Services.AddMartenReadiness(options =>
{
    options.VerifyDocumentStore = true;
    options.Timeout = TimeSpan.FromSeconds(5);
});
```

## Features

- **Document Store Verification**: Validates that Marten can connect to PostgreSQL
- **Activity Tracing**: Tags for verification status
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Idempotent Execution**: Cached results prevent redundant checks
- **Thread-Safe**: Concurrent readiness checks execute once
- **DI Integration**: Automatically resolves `IDocumentStore` from service provider

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | `null` (uses global timeout) |
| `VerifyDocumentStore` | `bool` | Verify document store connectivity | `true` |

## Logging

The signal emits structured logs at different levels:

- **Information**: Check start and successful completion
- **Debug**: Document store connection verification
- **Error**: Connection failures

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `marten.verify_store`: Whether document store verification is enabled

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddMartenReadiness();

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Advanced Marten Setup

```csharp
builder.Services.AddMarten(opts =>
{
    opts.Connection(configuration.GetConnectionString("Marten"));
    opts.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
});

builder.Services.AddIgnition(opts =>
{
    opts.GlobalTimeout = TimeSpan.FromSeconds(30);
    opts.Policy = IgnitionPolicy.BestEffort;
});

builder.Services.AddMartenReadiness(opts =>
{
    opts.Timeout = TimeSpan.FromSeconds(10);
});
```

### With Other Database Signals

```csharp
// PostgreSQL connection (raw Npgsql)
builder.Services.AddPostgresReadiness(connectionString, opts =>
{
    opts.ValidationQuery = "SELECT 1";
});

// Marten document store
builder.Services.AddMartenReadiness(opts =>
{
    opts.VerifyDocumentStore = true;
});

await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

## Error Handling

Document store connectivity failures are logged and propagated:

```csharp
try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        // Handle Marten/PostgreSQL-specific errors
        Console.WriteLine($"Marten Error: {inner.Message}");
    }
}
```

## Performance

- Minimal allocations per signal invocation
- Uses lightweight Marten session for verification
- Async throughout (no blocking I/O)
- Idempotent execution (verification attempted once)

## Prerequisites

- Marten must be registered in DI before calling `AddMartenReadiness()`
- PostgreSQL database must be accessible
- Marten schema should be created if using strict schema validation

## License

MIT License. See [LICENSE](../../LICENSE).
