# Veggerby.Ignition.Orleans

Orleans readiness signals for Veggerby.Ignition - verify Orleans cluster client availability during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Orleans
```

## Usage

### Basic Orleans Cluster Client Verification

```csharp
builder.Services.AddIgnition();

// Requires IClusterClient to be registered
builder.Services.AddOrleansClient(clientBuilder =>
{
    clientBuilder.UseLocalhostClustering();
});

builder.Services.AddOrleansReadiness();

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Timeout Configuration

```csharp
builder.Services.AddOrleansReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(10);
});
```

## Features

- **Cluster Client Verification**: Checks if cluster client is available and accessible
- **Activity Tracing**: Tags for cluster verification status
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Efficient Client Reuse**: Uses existing registered IClusterClient
- **Idempotent Execution**: Cached results prevent redundant checks
- **Thread-Safe**: Concurrent readiness checks execute once

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | `null` (uses global timeout) |

## Logging

The signal emits structured logs at different levels:

- **Information**: Check start and successful completions
- **Debug**: Client verification status
- **Error**: Client unavailability, connection errors

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `orleans.cluster_check`: Type of check performed

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddOrleansClient(clientBuilder => { /* ... */ });
builder.Services.AddOrleansReadiness();

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Integration with Orleans Hosting

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Orleans client
builder.Services.AddOrleansClient(clientBuilder =>
{
    clientBuilder.UseConnectionString("Provider=Clustering;...");
});

// Add ignition with Orleans readiness
builder.Services.AddIgnition();
builder.Services.AddOrleansReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// Wait for Orleans cluster client to be ready before serving requests
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

app.Run();
```

## Error Handling

Connection failures and initialization errors are logged and propagated:

```csharp
try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        if (inner is InvalidOperationException invalidEx)
        {
            Console.WriteLine($"Orleans client error: {invalidEx.Message}");
        }
    }
}
```

## Cluster Client Requirements

The signal requires an `IClusterClient` to be registered in the DI container. Typical registration:

```csharp
// For development
builder.Services.AddOrleansClient(clientBuilder =>
{
    clientBuilder.UseLocalhostClustering();
});

// For production with Azure Table Storage clustering
builder.Services.AddOrleansClient(clientBuilder =>
{
    clientBuilder.UseAzureStorageClustering(options =>
    {
        options.ConfigureTableServiceClient(connectionString);
    });
});
```

## Performance

- Uses existing IClusterClient (no additional client creation)
- Minimal allocations per signal invocation
- Async throughout (no blocking I/O)
- Idempotent execution (check performed once)

## Best Practices

1. **Timeout**: Set appropriate timeout based on cluster size and network latency
2. **Production**: Use longer timeouts in production for larger clusters
3. **Error Handling**: Always handle Orleans-specific exceptions in startup code
4. **Client Registration**: Ensure IClusterClient is registered before adding Orleans readiness

## Advanced Verification

For more advanced cluster verification (such as testing grain activation or checking active silos), you can implement custom signals that use grain calls or the management grain interface. The basic Orleans readiness signal focuses on verifying the cluster client is available and can be accessed from the dependency injection container.

## License

MIT License. See [LICENSE](../../LICENSE).
