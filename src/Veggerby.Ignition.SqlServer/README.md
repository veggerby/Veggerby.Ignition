# Veggerby.Ignition.SqlServer

SQL Server readiness signals for Veggerby.Ignition - verify database connections and schema during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.SqlServer
```

## Usage

### Basic Connection Verification

```csharp
builder.Services.AddIgnition();

builder.Services.AddSqlServerReadiness(
    "Server=localhost;Database=MyDb;Trusted_Connection=True;");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Validation Query

```csharp
builder.Services.AddSqlServerReadiness(
    "Server=localhost;Database=MyDb;Trusted_Connection=True;",
    options =>
    {
        options.ValidationQuery = "SELECT 1";
        options.Timeout = TimeSpan.FromSeconds(5);
    });
```

### Advanced Schema Validation

```csharp
builder.Services.AddSqlServerReadiness(
    connectionString,
    options =>
    {
        // Verify specific table exists
        options.ValidationQuery = "SELECT TOP 1 1 FROM Users";
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

- `sqlserver.server`: Data source from connection string
- `sqlserver.database`: Initial catalog from connection string
- `sqlserver.validation_query`: Validation query if configured

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddSqlServerReadiness(connectionString);

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Multiple Databases

```csharp
// Primary database
builder.Services.AddSqlServerReadiness(
    primaryConnectionString,
    options =>
    {
        options.ValidationQuery = "SELECT 1";
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// Reporting database
builder.Services.AddSqlServerReadiness(
    reportingConnectionString,
    options =>
    {
        options.ValidationQuery = "SELECT 1";
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

Note: Currently, multiple SQL Server signals will share the same name ("sqlserver-readiness"). For distinct signals, implement custom `IIgnitionSignal` with unique names.

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
        if (inner is SqlException sqlEx)
        {
            // Handle SQL Server-specific errors
            Console.WriteLine($"SQL Error {sqlEx.Number}: {sqlEx.Message}");
        }
    }
}
```

## Performance

- Minimal allocations per signal invocation
- Connection pooling handled by `Microsoft.Data.SqlClient`
- Async throughout (no blocking I/O)
- Idempotent execution (connection attempted once)

## License

MIT License. See [LICENSE](../../LICENSE).
