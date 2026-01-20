# Veggerby.Ignition.MariaDb

MariaDB readiness signals for Veggerby.Ignition - verify database connections and schema during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.MariaDb
dotnet add package MySqlConnector
```

## Usage

### Basic Connection Verification

```csharp
builder.Services.AddIgnition();

builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=mydb;User=root;Password=pass");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### Verification Strategies

MariaDB readiness supports multiple verification strategies:

```csharp
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=mydb;User=root;Password=pass",
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.Ping; // Default
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

**Available Strategies:**

- **Ping** (default): Basic connection ping using MySQL PING command
- **SimpleQuery**: Execute `SELECT 1` to verify query execution
- **TableExists**: Verify specific tables exist in the database
- **ConnectionPool**: Validate connection pool readiness

### Table Existence Verification

```csharp
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=mydb;User=root;Password=pass",
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.TableExists;
        options.VerifyTables.AddRange(new[] { "users", "products", "orders" });
        options.FailOnMissingTables = true; // Default - fail if any table missing
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

### Custom Query Verification

```csharp
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=mydb;User=root;Password=pass",
    options =>
    {
        options.TestQuery = "SELECT COUNT(*) FROM users WHERE active = 1";
        options.ExpectedMinimumRows = 1; // Expect at least 1 row returned
        options.Timeout = TimeSpan.FromSeconds(5);
    });
```

### Staged Execution with Testcontainers

```csharp
// Stage 0: Start MariaDB container
var infrastructure = new InfrastructureManager();
services.AddSingleton(infrastructure);
services.AddIgnitionFromTaskWithStage("mariadb-container",
    async ct => await infrastructure.StartMariaDbAsync(), stage: 0);

// Stage 1: Verify MariaDB readiness after container is started
services.AddMariaDbReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().MariaDbConnectionString,
    options =>
    {
        options.Stage = 1;
        options.VerificationStrategy = MariaDbVerificationStrategy.TableExists;
        options.VerifyTables.AddRange(new[] { "migrations" });
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

## Features

- **Multiple Verification Strategies**: Ping, query execution, table existence, connection pool validation
- **Retry with Exponential Backoff**: Configurable retry policy for transient failures (default: 8 retries, 500ms initial delay)
- **Activity Tracing**: Tags for server, database, and verification strategy
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Idempotent Execution**: Cached results prevent redundant connection attempts
- **Thread-Safe**: Concurrent readiness checks execute once
- **MariaDB/MySQL Compatible**: Uses MySqlConnector for wire-protocol compatibility

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | 30 seconds |
| `Stage` | `int?` | Staged execution phase number | `null` (unstaged) |
| `MaxRetries` | `int` | Maximum retry attempts for transient failures | 8 |
| `RetryDelay` | `TimeSpan` | Initial delay between retries (exponential backoff) | 500ms |
| `VerificationStrategy` | `MariaDbVerificationStrategy` | Verification approach | `Ping` |
| `VerifyTables` | `List<string>` | Tables to verify when using `TableExists` | Empty |
| `FailOnMissingTables` | `bool` | Fail if any table is missing | `true` |
| `Schema` | `string?` | Optional schema/database name override | `null` (uses connection string DB) |
| `TestQuery` | `string?` | Custom SQL query to execute | `null` |
| `ExpectedMinimumRows` | `int?` | Minimum rows expected from `TestQuery` | `null` |

## Logging

The signal emits structured logs at different levels:

- **Information**: Connection attempts and successful completions
- **Debug**: Ping execution, query execution, table verification
- **Warning**: Missing tables when `FailOnMissingTables = false`
- **Error**: Connection failures, query execution failures

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `mariadb.server`: Server hostname from connection string
- `mariadb.database`: Database name from connection string
- `mariadb.verification_strategy`: Verification strategy used

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddMariaDbReadiness(connectionString);

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Multiple Databases

```csharp
// Primary database
builder.Services.AddMariaDbReadiness(
    primaryConnectionString,
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.SimpleQuery;
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// Read replica
builder.Services.AddMariaDbReadiness(
    replicaConnectionString,
    options =>
    {
        options.VerificationStrategy = MariaDbVerificationStrategy.Ping;
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

Note: Multiple MariaDB signals will share the same name ("mariadb-readiness"). For distinct signal names, implement custom `IIgnitionSignal` instances.

### Retry Configuration

```csharp
builder.Services.AddMariaDbReadiness(
    "Server=localhost;Database=mydb;User=root;Password=pass",
    options =>
    {
        options.MaxRetries = 12; // More retries for slow startup
        options.RetryDelay = TimeSpan.FromSeconds(1); // Longer initial delay
        options.Timeout = TimeSpan.FromMinutes(2); // Overall timeout
    });
```

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
        if (inner is MySqlException mysqlEx)
        {
            // Handle MariaDB/MySQL-specific errors
            Console.WriteLine($"MariaDB Error: {mysqlEx.Message}");
        }
    }
}
```

## Performance

- Minimal allocations per signal invocation
- Connection pooling handled by MySqlConnector
- Async throughout (no blocking I/O)
- Idempotent execution (connection attempted once)
- Exponential backoff prevents retry storms

## MariaDB vs MySQL

This package uses **MySqlConnector** for compatibility with both MariaDB and MySQL databases. MariaDB is wire-compatible with MySQL, so the same client library works for both:

- **MariaDB 10.x+**: Fully supported
- **MySQL 5.7+**: Fully supported
- **MySQL 8.0+**: Fully supported

The package is named `Veggerby.Ignition.MariaDb` to reflect its primary target, but works seamlessly with MySQL databases as well.

## License

MIT License. See [LICENSE](../../LICENSE).
