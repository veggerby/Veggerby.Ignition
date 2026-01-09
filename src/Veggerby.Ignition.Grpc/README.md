# Veggerby.Ignition.Grpc

gRPC readiness signals for Veggerby.Ignition - verify gRPC services using health check protocol during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Grpc
```

## Usage

### Basic gRPC Service Verification

```csharp
builder.Services.AddIgnition();

builder.Services.AddGrpcReadiness("https://grpc.example.com");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### Service-Specific Health Check

```csharp
builder.Services.AddGrpcReadiness(
    "https://grpc.example.com",
    options =>
    {
        options.ServiceName = "myservice";
        options.Timeout = TimeSpan.FromSeconds(5);
    });
```

### Overall Server Health

```csharp
builder.Services.AddGrpcReadiness(
    "https://grpc.example.com",
    options =>
    {
        // Null or empty service name checks overall server health
        options.ServiceName = null;
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

## Features

- **gRPC Health Check Protocol**: Uses standard `grpc.health.v1.Health` service
- **Service-Specific Checks**: Verify specific gRPC services by name
- **Overall Health**: Check server-wide health (no service name)
- **Channel State Tracking**: Monitors gRPC channel connection state
- **Activity Tracing**: Tags for service URL, service name, channel state, health status
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Efficient Channel Reuse**: Channel created once and reused
- **Idempotent Execution**: Cached results prevent redundant checks
- **Thread-Safe**: Concurrent readiness checks execute once

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | `null` (uses global timeout) |
| `ServiceName` | `string?` | Service name for health check | `null` (server health) |

## gRPC Health Check Protocol

This package uses the standard gRPC Health Checking Protocol defined in [grpc.health.v1](https://github.com/grpc/grpc/blob/master/doc/health-checking.md).

The health check service must be implemented on the gRPC server:

```csharp
// Server-side (for reference)
builder.Services.AddGrpcHealthChecks()
    .AddCheck("myservice", () => HealthCheckResult.Healthy());

app.MapGrpcHealthChecksService();
```

## Logging

The signal emits structured logs at different levels:

- **Information**: Check start and successful completions
- **Debug**: Channel state, request/response details
- **Error**: Health status failures, connection errors, unimplemented protocol

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `grpc.service_url`: Target gRPC service URL
- `grpc.service_name`: Service name or "(server)" for overall health
- `grpc.channel_state`: Current channel state (Ready, Idle, etc.)
- `grpc.health_status`: Health check response status

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddGrpcReadiness("https://grpc.example.com");

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Multiple gRPC Services

```csharp
// Main API service
builder.Services.AddGrpcReadiness(
    "https://api.grpc.example.com",
    options =>
    {
        options.ServiceName = "api";
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// Analytics service
builder.Services.AddGrpcReadiness(
    "https://analytics.grpc.example.com",
    options =>
    {
        options.ServiceName = "analytics";
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

Note: Currently, multiple gRPC signals will share the same name ("grpc-readiness"). For distinct signals, implement custom `IIgnitionSignal` with unique names.

### Channel Configuration

For advanced channel configuration (credentials, interceptors, etc.), create and configure the channel externally:

```csharp
// For more control, register the signal directly
services.AddSingleton<IIgnitionSignal>(sp =>
{
    var channelOptions = new GrpcChannelOptions
    {
        Credentials = ChannelCredentials.SecureSsl,
        // Other channel options...
    };
    
    var channel = GrpcChannel.ForAddress("https://grpc.example.com", channelOptions);
    var options = new GrpcReadinessOptions
    {
        ServiceName = "myservice",
        Timeout = TimeSpan.FromSeconds(5)
    };
    var logger = sp.GetRequiredService<ILogger<GrpcReadinessSignal>>();
    
    return new GrpcReadinessSignal(channel, "https://grpc.example.com", options, logger);
});
```

## Error Handling

Connection failures and health check errors are logged and propagated:

```csharp
try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        if (inner is RpcException rpcEx)
        {
            // Handle gRPC-specific errors
            Console.WriteLine($"gRPC Error: {rpcEx.Status}");
        }
        else if (inner is InvalidOperationException invalidEx)
        {
            // Handle health check failures
            Console.WriteLine($"Health Check Error: {invalidEx.Message}");
        }
    }
}
```

## Health Status Values

The gRPC health check protocol defines these serving status values:

- **SERVING**: Service is healthy and ready
- **NOT_SERVING**: Service is not healthy
- **UNKNOWN**: Health status unknown
- **SERVICE_UNKNOWN**: Requested service not found

Only **SERVING** is considered successful. All other statuses result in failure.

## Server Requirements

The gRPC server must implement the `grpc.health.v1.Health` service. If the server does not implement this protocol, the signal will fail with an "Unimplemented" error.

## Performance

- Efficient gRPC channel reuse (created once)
- Minimal allocations per signal invocation
- Async throughout (no blocking I/O)
- Idempotent execution (check performed once)

## License

MIT License. See [LICENSE](../../LICENSE).
