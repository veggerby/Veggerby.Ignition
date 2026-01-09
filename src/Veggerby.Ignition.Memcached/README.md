# Veggerby.Ignition.Memcached

Memcached readiness signals for Veggerby.Ignition - verify cache connections and operations during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Memcached
```

## Features

- **Multiple Verification Strategies**:
  - `ConnectionOnly` - Fast connection establishment check (default)
  - `Stats` - Connection + stats command verification
  - `TestKey` - Full read/write verification with test key round-trip

- **Flexible Configuration**:
  - Use existing `IMemcachedClient` from DI or provide server endpoints
  - Configurable per-signal timeout
  - Customizable test key prefix
  - Automatic cleanup of test keys (60s expiration + explicit delete)

- **Production Ready**:
  - Thread-safe and idempotent execution
  - Structured logging with diagnostic details
  - Activity tracing support
  - Works with all coordinator policies and execution modes

## Quick Start

### Basic Usage (Server Endpoints)

```csharp
using Veggerby.Ignition.Memcached;

builder.Services.AddIgnition();
builder.Services.AddMemcachedReadiness(new[] { "localhost:11211" });

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Verification Strategy

```csharp
builder.Services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
{
    options.VerificationStrategy = MemcachedVerificationStrategy.TestKey;
    options.TestKeyPrefix = "myapp:readiness:";
    options.Timeout = TimeSpan.FromSeconds(5);
});
```

### Using Existing Memcached Client

```csharp
// Register Memcached client with custom configuration
builder.Services.AddEnyimMemcached(options =>
{
    options.AddServer("server1:11211");
    options.AddServer("server2:11211");
    options.Protocol = MemcachedProtocol.Binary;
});

// Use the registered client
builder.Services.AddMemcachedReadiness(options =>
{
    options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
});
```

## Verification Strategies

### ConnectionOnly (Default)

Fastest option - only verifies that the Memcached client is initialized.

```csharp
builder.Services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
{
    options.VerificationStrategy = MemcachedVerificationStrategy.ConnectionOnly;
});
```

**Use when**: You trust the client initialization and want minimal startup overhead.

### Stats

Executes a stats command to verify server responsiveness.

```csharp
builder.Services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
{
    options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
});
```

**Use when**: You want to verify server responsiveness with minimal overhead.

### TestKey (Most Thorough)

Performs a full set/get/delete round-trip with a test key.

```csharp
builder.Services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
{
    options.VerificationStrategy = MemcachedVerificationStrategy.TestKey;
    options.TestKeyPrefix = "ignition:readiness:";
});
```

**Use when**: You need to verify full read/write capability before starting.

**Test Key Behavior**:
- Keys use format: `{TestKeyPrefix}{Guid}`
- Set with 60-second expiration
- Explicitly deleted after verification
- No cache pollution

## Configuration Options

```csharp
public sealed class MemcachedReadinessOptions
{
    /// <summary>
    /// Per-signal timeout. Null uses global timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Verification strategy (default: ConnectionOnly).
    /// </summary>
    public MemcachedVerificationStrategy VerificationStrategy { get; set; }

    /// <summary>
    /// Prefix for test keys (default: "ignition:readiness:").
    /// </summary>
    public string TestKeyPrefix { get; set; }
}
```

## Integration with Coordinator

Works seamlessly with all ignition features:

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
});

builder.Services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
{
    options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
    options.Timeout = TimeSpan.FromSeconds(5); // Per-signal override
});

var result = await coordinator.WaitAllAsync();
// Check result.SignalResults for Memcached status
```

## Multiple Servers

Easily configure multiple Memcached servers:

```csharp
builder.Services.AddMemcachedReadiness(
    new[] { "cache1:11211", "cache2:11211", "cache3:11211" },
    options =>
    {
        options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
    });
```

## Logging and Tracing

Structured logging output:

```
[Information] Memcached readiness check starting using strategy Stats
[Debug] Memcached client initialized
[Debug] Executing Memcached stats command
[Debug] Memcached stats retrieved successfully
[Information] Memcached readiness check completed successfully
```

Activity tags when tracing is enabled:

- `memcached.verification_strategy` - Strategy used

## Error Handling

Exceptions are properly categorized:

- **Connection failures**: `InvalidOperationException`, client-specific exceptions
- **Timeout**: `OperationCanceledException` (when timeout configured)
- **Verification failures**: `InvalidOperationException` with descriptive message

All exceptions are logged and surfaced through the coordinator's aggregated result.

## See Also

- [Veggerby.Ignition Core Documentation](../Veggerby.Ignition/README.md)
- [Sample Project](../../samples/Caching/README.md)
- [EnyimMemcachedCore Documentation](https://github.com/cnblogs/EnyimMemcachedCore)
