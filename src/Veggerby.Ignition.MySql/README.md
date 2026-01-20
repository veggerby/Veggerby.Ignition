# Veggerby.Ignition.MySql

MySQL readiness signals for [Veggerby.Ignition](../Veggerby.Ignition) - verify database connections and schema during application startup.

## Features

- **Multiple Verification Strategies**: Ping, simple query, table existence, connection pool validation
- **Retry/Backoff**: Built-in exponential backoff for transient failures
- **Staged Execution**: Coordinate MySQL readiness with other infrastructure dependencies
- **Factory Pattern**: Support for Testcontainers and dynamic connection strings
- **OpenTelemetry**: Activity tracing integration with proper tags

## Installation

```bash
dotnet add package Veggerby.Ignition.MySql
```

## Quick Start

### Basic Usage

```csharp
using Veggerby.Ignition.MySql;

var builder = WebApplication.CreateBuilder(args);

// Add MySQL readiness check
builder.Services.AddMySqlReadiness(
    "Server=localhost;Database=myapp;User=root;Password=secret",
    options =>
    {
        options.VerificationStrategy = MySqlVerificationStrategy.Ping;
        options.Timeout = TimeSpan.FromSeconds(30);
    });

// Add Ignition coordinator
builder.Services.AddIgnition();

var app = builder.Build();

// Wait for MySQL before starting
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

app.Run();
```

## Verification Strategies

### Ping (Default)

Lightweight connection ping using MySQL PING command:

```csharp
services.AddMySqlReadiness(connectionString, options =>
{
    options.VerificationStrategy = MySqlVerificationStrategy.Ping;
});
```

### Simple Query

Execute `SELECT 1` to verify connection and query execution:

```csharp
services.AddMySqlReadiness(connectionString, options =>
{
    options.VerificationStrategy = MySqlVerificationStrategy.SimpleQuery;
});
```

### Table Existence

Verify specific tables exist in the schema:

```csharp
services.AddMySqlReadiness(connectionString, options =>
{
    options.VerificationStrategy = MySqlVerificationStrategy.TableExists;
    options.VerifyTables.Add("users");
    options.VerifyTables.Add("orders");
    options.FailOnMissingTables = true; // Default
});
```

### Connection Pool

Validate connection pool readiness:

```csharp
services.AddMySqlReadiness(connectionString, options =>
{
    options.VerificationStrategy = MySqlVerificationStrategy.ConnectionPool;
});
```

### Custom Query

Execute a custom query with optional row count validation:

```csharp
services.AddMySqlReadiness(connectionString, options =>
{
    options.TestQuery = "SELECT COUNT(*) FROM system_status WHERE ready = 1";
    options.ExpectedMinimumRows = 1;
});
```

## Advanced Scenarios

### Staged Execution with Testcontainers

```csharp
// Stage 0: Start MySQL container
var infrastructure = new InfrastructureManager();
services.AddSingleton(infrastructure);
services.AddIgnitionFromTaskWithStage(
    "mysql-container",
    async ct => await infrastructure.StartMySqlAsync(),
    stage: 0);

// Stage 1: Verify MySQL readiness
services.AddMySqlReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().MySqlConnectionString,
    options =>
    {
        options.Stage = 1;
        options.VerificationStrategy = MySqlVerificationStrategy.TableExists;
        options.VerifyTables.Add("migrations");
    });
```

### Retry Configuration

```csharp
services.AddMySqlReadiness(connectionString, options =>
{
    options.MaxRetries = 10;
    options.RetryDelay = TimeSpan.FromMilliseconds(500);
    options.Timeout = TimeSpan.FromSeconds(60);
});
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | 30 seconds | Per-signal timeout |
| `Stage` | `int?` | `null` | Staged execution phase |
| `MaxRetries` | `int` | 8 | Maximum retry attempts |
| `RetryDelay` | `TimeSpan` | 500ms | Initial retry delay (exponential backoff) |
| `VerificationStrategy` | `MySqlVerificationStrategy` | `Ping` | Verification strategy |
| `VerifyTables` | `List<string>` | Empty | Tables to verify (TableExists strategy) |
| `FailOnMissingTables` | `bool` | `true` | Fail if tables don't exist |
| `Schema` | `string?` | `null` | Schema for table verification |
| `TestQuery` | `string?` | `null` | Custom query to execute |
| `ExpectedMinimumRows` | `int?` | `null` | Minimum rows for custom query |

## Health Checks

MySQL readiness integrates with ASP.NET Core health checks:

```csharp
builder.Services.AddIgnition();
builder.Services.AddHealthChecks()
    .AddIgnition(); // Includes all registered signals

app.MapHealthChecks("/health");
```

## OpenTelemetry Integration

Activity tags are automatically set when tracing is enabled:

- `mysql.server`: MySQL server hostname
- `mysql.database`: Database name
- `mysql.verification_strategy`: Strategy used

## Dependencies

- [MySqlConnector](https://mysqlconnector.net/) - Modern async MySQL driver
- Veggerby.Ignition (core library)

## License

MIT License - see [LICENSE](../../LICENSE) for details.
