# Veggerby.Ignition.RabbitMq

RabbitMQ readiness signals for [Veggerby.Ignition](../Veggerby.Ignition) - verify broker connections, queues, and exchanges during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.RabbitMq
```

## Quick Start

### Basic Connection Verification

```csharp
builder.Services.AddRabbitMqReadiness("amqp://guest:guest@localhost:5672/");
```

### Queue and Exchange Verification

```csharp
builder.Services.AddRabbitMqReadiness("amqp://localhost", options =>
{
    options.WithQueue("orders");
    options.WithQueue("notifications");
    options.WithExchange("events");
    options.Timeout = TimeSpan.FromSeconds(5);
});
```

### Advanced Configuration

```csharp
var factory = new ConnectionFactory
{
    HostName = "rabbitmq.example.com",
    Port = 5671,
    VirtualHost = "/production",
    UserName = "app",
    Password = "secret",
    Ssl = new SslOption { Enabled = true }
};

builder.Services.AddRabbitMqReadiness(factory, options =>
{
    options.WithQueue("critical-events");
    options.PerformRoundTripTest = true;
    options.RoundTripTestTimeout = TimeSpan.FromSeconds(3);
    options.FailOnMissingTopology = false; // warn instead of fail
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

## Configuration Options

### `RabbitMqReadinessOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `null` | Per-signal timeout (overrides global) |
| `VerifyQueues` | `ICollection<string>` | empty | Queue names to verify |
| `VerifyExchanges` | `ICollection<string>` | empty | Exchange names to verify |
| `FailOnMissingTopology` | `bool` | `true` | Fail if queues/exchanges are missing |
| `PerformRoundTripTest` | `bool` | `false` | Perform publish/consume test |
| `RoundTripTestTimeout` | `TimeSpan` | 5s | Timeout for round-trip test |

## Features

- **Connection Verification**: Validates basic RabbitMQ connection and channel creation
- **Queue Verification**: Checks that specified queues exist and are accessible (passive declaration)
- **Exchange Verification**: Validates that exchanges exist (passive declaration)
- **Round-Trip Test**: Optional end-to-end publish/consume validation
- **Graceful Degradation**: Configurable behavior for missing topology (fail or warn)
- **Activity Tracing**: Emits diagnostic tags (host, port, virtualhost)
- **Structured Logging**: Detailed logs at Info/Debug/Warning/Error levels

## Integration with Ignition Coordinator

The RabbitMQ readiness signal integrates seamlessly with all Ignition coordinator features:

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
    options.EnableTracing = true;
});

builder.Services.AddRabbitMqReadiness("amqp://localhost", options =>
{
    options.WithQueue("orders");
    options.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// Wait for all signals including RabbitMQ
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

## Health Check Integration

When using Ignition with health checks enabled, the RabbitMQ signal status is included in the `ignition-readiness` health check:

```csharp
builder.Services.AddIgnition(addHealthCheck: true);
builder.Services.AddRabbitMqReadiness("amqp://localhost");

app.MapHealthChecks("/health");
```

## Topology Management

**Important**: This package verifies existing topology but does **not** create queues or exchanges. Use RabbitMQ management APIs, MassTransit configurators, or administrative tools to create topology before startup.

If `FailOnMissingTopology = false`, missing queues/exchanges are logged as warnings but do not fail the signal. This is useful for:
- Development environments where topology may not be fully provisioned
- Scenarios where queues are created dynamically
- Gradual rollout of new topology

## Dependencies

- `Veggerby.Ignition` - Core ignition library
- `RabbitMQ.Client` 7.0+ - Official RabbitMQ .NET client
- `Microsoft.Extensions.*` - Logging, DI, Options

## License

MIT - See [LICENSE](../../LICENSE) in repository root.
