# Veggerby.Ignition.Postgres

PostgreSQL readiness signals for Veggerby.Ignition - verify database connections and schema during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Postgres
```

## Usage

### Basic Connection Verification

```csharp
builder.Services.AddIgnition();

builder.Services.AddPostgresReadiness(
    "Host=localhost;Database=mydb;Username=user;Password=pass");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Validation Query

```csharp
builder.Services.AddPostgresReadiness(
    "Host=localhost;Database=mydb;Username=user;Password=pass",
    options =>
    {
        options.ValidationQuery = "SELECT 1";
        options.Timeout = TimeSpan.FromSeconds(5);
    });
```

### Advanced Schema Validation

```csharp
builder.Services.AddPostgresReadiness(
    connectionString,
    options =>
    {
        // Verify specific table exists
        options.ValidationQuery = "SELECT 1 FROM users LIMIT 1";
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

## Features

- **Connection Verification**: Validates database connectivity
- **Optional Query Validation**: Execute custom queries to verify schema readiness
- **Activity Tracing**: Tags for server, database, and validation queries
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Idempotent Execution**: Cached results prevent redundant connection attempts
- **Thread-Safe**: Concurrent readiness checks execute once

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | `null` (uses global timeout) |
| `ValidationQuery` | `string?` | SQL query to execute after connection | `null` (connection only) |

## Logging

The signal emits structured logs at different levels:

- **Information**: Connection attempts and successful completions
- **Debug**: Connection established, validation query execution
- **Error**: Connection failures, query execution failures

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `postgres.server`: Host from connection string
- `postgres.database`: Database name from connection string
- `postgres.validation_query`: Validation query if configured

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddPostgresReadiness(connectionString);

builder.Services.AddHealthChecks()
    .AddCheck("ignition-readiness", () =>
    {
        var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
        var result = coordinator.GetResultAsync().Result;
        return result.Succeeded
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
    });
```

### Multiple Databases

```csharp
// Primary database
builder.Services.AddPostgresReadiness(
    primaryConnectionString,
    options =>
    {
        options.ValidationQuery = "SELECT 1";
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// Read replica
builder.Services.AddPostgresReadiness(
    replicaConnectionString,
    options =>
    {
        options.ValidationQuery = "SELECT 1";
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

Note: Currently, multiple PostgreSQL signals will share the same name ("postgres-readiness"). For distinct signals, implement custom `IIgnitionSignal` with unique names.

## Error Handling

Connection failures and query execution errors are logged and propagated:

```csharp
try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        if (inner is NpgsqlException pgEx)
        {
            // Handle PostgreSQL-specific errors
            Console.WriteLine($"PostgreSQL Error: {pgEx.Message}");
        }
    }
}
```

## Performance

- Minimal allocations per signal invocation
- Connection pooling handled by Npgsql
- Async throughout (no blocking I/O)
- Idempotent execution (connection attempted once)

## License

MIT License. See [LICENSE](../../LICENSE).
