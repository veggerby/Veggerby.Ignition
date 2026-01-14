# Veggerby.Ignition.Postgres

PostgreSQL readiness signals for Veggerby.Ignition - verify database connections and schema during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Postgres
dotnet add package Npgsql
```

## Usage

### Modern Pattern: NpgsqlDataSource (Recommended)

The recommended approach uses `NpgsqlDataSource` for better connection pooling and DI integration:

```csharp
// Register NpgsqlDataSource in DI container
builder.Services.AddNpgsqlDataSource(
    "Host=localhost;Database=mydb;Username=user;Password=pass");

// Add PostgreSQL readiness signal (automatically resolves NpgsqlDataSource from DI)
builder.Services.AddIgnition();
builder.Services.AddPostgresReadiness();

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

**Why NpgsqlDataSource?**

- **Better connection pooling**: Modern Npgsql 7.0+ recommended pattern
- **Cleaner DI integration**: No connection string duplication
- **Multiplexing support**: Enables advanced pooling scenarios
- **Shared across services**: Same data source used by repositories/DbContext

### Legacy Pattern: Connection String

For simpler scenarios or when NpgsqlDataSource isn't registered in DI:

```csharp
builder.Services.AddIgnition();

builder.Services.AddPostgresReadiness(
    "Host=localhost;Database=mydb;Username=user;Password=pass");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Validation Query

```csharp
builder.Services.AddNpgsqlDataSource(connectionString);

builder.Services.AddPostgresReadiness(options =>
{
    options.ValidationQuery = "SELECT 1";
    options.Timeout = TimeSpan.FromSeconds(5);
});
```

### Advanced Schema Validation

```csharp
builder.Services.AddNpgsqlDataSource(connectionString);

builder.Services.AddPostgresReadiness(options =>
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

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Multiple Databases

With `NpgsqlDataSource` (when you have multiple databases):

```csharp
// Primary database
builder.Services.AddNpgsqlDataSource(
    primaryConnectionString,
    serviceKey: "primary");

// Read replica
builder.Services.AddNpgsqlDataSource(
    replicaConnectionString,
    serviceKey: "replica");

// Note: Currently AddPostgresReadiness only supports the default (non-keyed) data source.
// For multiple databases, register custom signals manually:
builder.Services.AddIgnitionSignal(sp =>
{
    var primaryDs = sp.GetRequiredKeyedService<NpgsqlDataSource>("primary");
    var logger = sp.GetRequiredService<ILogger<PostgresReadinessSignal>>();
    return new PostgresReadinessSignal(primaryDs, new PostgresReadinessOptions
    {
        ValidationQuery = "SELECT 1",
        Timeout = TimeSpan.FromSeconds(5)
    }, logger);
});

builder.Services.AddIgnitionSignal(sp =>
{
    var replicaDs = sp.GetRequiredKeyedService<NpgsqlDataSource>("replica");
    var logger = sp.GetRequiredService<ILogger<PostgresReadinessSignal>>();
    return new PostgresReadinessSignal(replicaDs, new PostgresReadinessOptions
    {
        ValidationQuery = "SELECT 1",
        Timeout = TimeSpan.FromSeconds(10)
    }, logger);
});
```

With connection strings (simpler for multiple databases):

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

Note: Multiple PostgreSQL signals will share the same name ("postgres-readiness"). For distinct signal names, implement custom `IIgnitionSignal` instances.

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
