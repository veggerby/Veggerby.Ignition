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

- **Cluster Client Registration Verification**: Verifies IClusterClient is registered in DI container
- **Activity Tracing**: Tags for cluster verification status
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Efficient Client Reuse**: Uses existing registered IClusterClient
- **Idempotent Execution**: Cached results prevent redundant checks
- **Thread-Safe**: Concurrent readiness checks execute once

> **Note**: This signal performs basic verification that the Orleans cluster client is available in the DI container.
> For more comprehensive cluster connectivity checks (e.g., testing grain activation, verifying cluster membership,
> or checking silo availability), implement a custom `IIgnitionSignal` that makes actual grain calls or uses
> management grain methods specific to your Orleans configuration.

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

For more comprehensive cluster verification (such as testing grain activation or checking active silos), 
implement a custom signal:

```csharp
public class OrleansClusterHealthSignal : IIgnitionSignal
{
    private readonly IClusterClient _clusterClient;
    
    public string Name => "orleans-cluster-health";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(30);
    
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        // Example: Verify you can get a grain reference and make a call
        var healthGrain = _clusterClient.GetGrain<IHealthCheckGrain>(0);
        await healthGrain.CheckHealthAsync();
        
        // Or use management grain for more thorough checks if available
        // var mgmt = _clusterClient.GetGrain<IManagementGrain>(0);
        // var hosts = await mgmt.GetHosts(onlyActive: true);
        // if (hosts.Count == 0) throw new InvalidOperationException("No active silos");
    }
}

services.AddSingleton<IIgnitionSignal, OrleansClusterHealthSignal>();
```

The basic Orleans readiness signal provided by this package focuses on verifying DI registration,
which is sufficient for ensuring Orleans client configuration is complete during application startup.

## License

MIT License. See [LICENSE](../../LICENSE).
