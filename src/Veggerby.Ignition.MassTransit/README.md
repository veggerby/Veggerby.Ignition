# Veggerby.Ignition.MassTransit

MassTransit bus readiness signals for [Veggerby.Ignition](../Veggerby.Ignition) - verify message bus startup and connectivity during application initialization.

## Installation

```bash
dotnet add package Veggerby.Ignition.MassTransit
```

## Quick Start

### Basic Bus Readiness

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/");
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddMassTransitReadiness();
```

### With Custom Timeout

```csharp
builder.Services.AddMassTransitReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
    options.BusReadyTimeout = TimeSpan.FromSeconds(45);
});
```

### With Custom Timeout

```csharp
builder.Services.AddMassTransitReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
    options.BusReadyTimeout = TimeSpan.FromSeconds(45);
});
```

## Configuration Options

### `MassTransitReadinessOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `null` | Per-signal timeout (overrides global) |
| `BusReadyTimeout` | `TimeSpan` | 30s | Max time to wait for bus to become healthy |

## Features

- **Bus Health Verification**: Uses MassTransit's built-in health checks
- **Transport Agnostic**: Works with any MassTransit transport (RabbitMQ, Azure Service Bus, in-memory, etc.)
- **Activity Tracing**: Emits diagnostic tags for observability
- **Structured Logging**: Detailed logs at Info/Debug/Warning/Error levels
- **Graceful Timeout Handling**: Configurable wait periods for bus readiness

## Transport Support

This package works with **all MassTransit transports**:

### RabbitMQ

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq.example.com", "/production", h =>
        {
            h.Username("app");
            h.Password("secret");
        });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddMassTransitReadiness();
```

### Azure Service Bus

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host("Endpoint=sb://...");
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddMassTransitReadiness();
```

### In-Memory (Testing)

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddMassTransitReadiness();
```

## Integration with Ignition Coordinator

The MassTransit readiness signal integrates seamlessly with all Ignition coordinator features:

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(60);
    options.Policy = IgnitionPolicy.FailFast;
    options.EnableTracing = true;
});

builder.Services.AddMassTransit(/* ... */);
builder.Services.AddMassTransitReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// Wait for all signals including MassTransit
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

## Health Check Integration

When using Ignition with health checks enabled, the MassTransit signal status is included in the `ignition-readiness` health check:

```csharp
builder.Services.AddIgnition(addHealthCheck: true);
builder.Services.AddMassTransit(/* ... */);
builder.Services.AddMassTransitReadiness();

app.MapHealthChecks("/health");
```

## MassTransit vs Direct RabbitMQ

Choose the appropriate package based on your needs:

### Use `Veggerby.Ignition.MassTransit` when:
- You're already using MassTransit for message handling
- You need transport abstraction (might switch from RabbitMQ to Azure Service Bus)
- You want to leverage MassTransit's built-in health checks
- You care about receive endpoint readiness

### Use `Veggerby.Ignition.RabbitMq` when:
- You're using RabbitMQ directly without MassTransit
- You need fine-grained queue/exchange verification
- You want to perform round-trip messaging tests
- You have specific RabbitMQ client requirements

## Dependencies

- `Veggerby.Ignition` - Core ignition library
- `MassTransit` 8.3+ - Message bus abstraction
- `Microsoft.Extensions.*` - Logging, DI, Options

**Note**: Transport-specific packages (e.g., `MassTransit.RabbitMQ`) are peer dependencies - you must add them to your application project.

## License

MIT - See [LICENSE](../../LICENSE) in repository root.
