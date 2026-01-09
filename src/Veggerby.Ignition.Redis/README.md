# Veggerby.Ignition.Redis

Redis readiness signals for Veggerby.Ignition - verify cache connections and operations during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Redis
```

## Features

- **Multiple Verification Strategies**:
  - `ConnectionOnly` - Fast connection establishment check (default)
  - `Ping` - Connection + PING command verification
  - `PingAndTestKey` - Full read/write verification with test key round-trip

- **Flexible Configuration**:
  - Use existing `IConnectionMultiplexer` from DI or provide connection string
  - Configurable per-signal timeout
  - Customizable test key prefix
  - Automatic cleanup of test keys (60s TTL + explicit delete)

- **Production Ready**:
  - Thread-safe and idempotent execution
  - Structured logging with diagnostic details
  - Activity tracing support
  - Works with all coordinator policies and execution modes

## Quick Start

### Basic Usage (Connection String)

```csharp
using Veggerby.Ignition.Redis;

builder.Services.AddIgnition();
builder.Services.AddRedisReadiness("localhost:6379");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Verification Strategy

```csharp
builder.Services.AddRedisReadiness("localhost:6379", options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
    options.TestKeyPrefix = "myapp:readiness:";
    options.Timeout = TimeSpan.FromSeconds(5);
});
```

### Using Existing Connection Multiplexer

```csharp
// Register connection multiplexer with custom configuration
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse("localhost:6379");
    config.ConnectTimeout = 5000;
    config.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(config);
});

// Use the registered multiplexer
builder.Services.AddRedisReadiness(options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.Ping;
});
```

## Verification Strategies

### ConnectionOnly (Default)

Fastest option - only verifies that the connection multiplexer is connected.

```csharp
builder.Services.AddRedisReadiness("localhost:6379", options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.ConnectionOnly;
});
```

**Use when**: You trust the connection health and want minimal startup overhead.

### Ping

Executes a PING command to verify server responsiveness.

```csharp
builder.Services.AddRedisReadiness("localhost:6379", options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.Ping;
});
```

**Use when**: You want to verify server responsiveness with minimal overhead.

### PingAndTestKey (Most Thorough)

Executes PING and performs a full set/get/delete round-trip with a test key.

```csharp
builder.Services.AddRedisReadiness("localhost:6379", options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
    options.TestKeyPrefix = "ignition:readiness:";
});
```

**Use when**: You need to verify full read/write capability before starting.

**Test Key Behavior**:
- Keys use format: `{TestKeyPrefix}{Guid}`
- Set with 60-second TTL
- Explicitly deleted after verification
- No cache pollution

## Configuration Options

```csharp
public sealed class RedisReadinessOptions
{
    /// <summary>
    /// Per-signal timeout. Null uses global timeout.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Verification strategy (default: ConnectionOnly).
    /// </summary>
    public RedisVerificationStrategy VerificationStrategy { get; set; }

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

builder.Services.AddRedisReadiness("localhost:6379", options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.Ping;
    options.Timeout = TimeSpan.FromSeconds(5); // Per-signal override
});

var result = await coordinator.WaitAllAsync();
// Check result.SignalResults for Redis status
```

## Cluster and Sentinel Support

Works with Redis cluster and Sentinel configurations through StackExchange.Redis:

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = new ConfigurationOptions
    {
        EndPoints = { "node1:6379", "node2:6379", "node3:6379" },
        AbortOnConnectFail = false,
        ConnectTimeout = 5000
    };
    return ConnectionMultiplexer.Connect(config);
});

builder.Services.AddRedisReadiness(options =>
{
    options.VerificationStrategy = RedisVerificationStrategy.Ping;
});
```

## Logging and Tracing

Structured logging output:

```
[Information] Redis readiness check starting for endpoints localhost:6379 using strategy Ping
[Debug] Redis connection established
[Debug] Executing Redis PING command
[Debug] Redis PING completed in 1.23ms
[Information] Redis readiness check completed successfully
```

Activity tags when tracing is enabled:

- `redis.endpoints` - Connected endpoints
- `redis.verification_strategy` - Strategy used

## Error Handling

Exceptions are properly categorized:

- **Connection failures**: `RedisConnectionException`, `InvalidOperationException`
- **Timeout**: `OperationCanceledException` (when timeout configured)
- **Verification failures**: `InvalidOperationException` with descriptive message

All exceptions are logged and surfaced through the coordinator's aggregated result.

## See Also

- [Veggerby.Ignition Core Documentation](../Veggerby.Ignition/README.md)
- [Sample Project](../../samples/Caching/README.md)
- [StackExchange.Redis Documentation](https://stackexchange.github.io/StackExchange.Redis/)
