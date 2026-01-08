# Veggerby.Ignition.MongoDb

MongoDB readiness signals for Veggerby.Ignition - verify MongoDB cluster connections and collections during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.MongoDb
```

## Usage

### Basic Cluster Connectivity Verification

```csharp
builder.Services.AddIgnition();

builder.Services.AddMongoDbReadiness("mongodb://localhost:27017");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Collection Verification

```csharp
builder.Services.AddMongoDbReadiness(
    "mongodb://localhost:27017",
    options =>
    {
        options.DatabaseName = "mydb";
        options.VerifyCollection = "users";
        options.Timeout = TimeSpan.FromSeconds(5);
    });
```

### Using Existing MongoDB Client

```csharp
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient("mongodb://localhost:27017"));

builder.Services.AddMongoDbReadiness(options =>
{
    options.DatabaseName = "mydb";
    options.VerifyCollection = "orders";
});
```

### Fluent Configuration

```csharp
builder.Services.AddMongoDbReadiness(
    "mongodb://localhost:27017",
    options => options
        .WithDatabase("mydb")
        .WithCollection("users"));
```

## Features

- **Cluster Connectivity Verification**: Pings MongoDB cluster to validate accessibility
- **Optional Collection Verification**: Validates that specific collections exist
- **Activity Tracing**: Tags for database and collection names
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **Idempotent Execution**: Cached results prevent redundant checks
- **Thread-Safe**: Concurrent readiness checks execute once
- **Flexible Client Management**: Use connection string or existing `IMongoClient`

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | `null` (uses global timeout) |
| `DatabaseName` | `string?` | Database name for collection verification | `null` |
| `VerifyCollection` | `string?` | Collection name to verify | `null` (cluster verification only) |

## Logging

The signal emits structured logs at different levels:

- **Information**: Check start and successful completion
- **Debug**: Cluster ping, collection verification
- **Error**: Connection failures, collection not found

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `mongodb.database`: Database name (if specified)
- `mongodb.collection`: Collection name (if specified)

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddMongoDbReadiness("mongodb://localhost:27017");

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

### Sharded Cluster Setup

```csharp
builder.Services.AddMongoDbReadiness(
    "mongodb://mongos1:27017,mongos2:27017",
    options =>
    {
        options.DatabaseName = "sharded_db";
        options.VerifyCollection = "sharded_collection";
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

### Multiple Databases

```csharp
// Primary database
builder.Services.AddMongoDbReadiness(
    "mongodb://localhost:27017",
    options =>
    {
        options.DatabaseName = "primary";
        options.VerifyCollection = "users";
    });

// Analytics database
builder.Services.AddMongoDbReadiness(
    "mongodb://localhost:27017",
    options =>
    {
        options.DatabaseName = "analytics";
        options.VerifyCollection = "events";
    });
```

Note: Currently, multiple MongoDB signals will share the same name ("mongodb-readiness"). For distinct signals, implement custom `IIgnitionSignal` with unique names.

## Error Handling

Cluster connectivity and collection verification failures are logged and propagated:

```csharp
try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        if (inner is MongoException mongoEx)
        {
            // Handle MongoDB-specific errors
            Console.WriteLine($"MongoDB Error: {mongoEx.Message}");
        }
        else if (inner is InvalidOperationException opEx && opEx.Message.Contains("does not exist"))
        {
            // Collection not found
            Console.WriteLine($"Collection verification failed: {opEx.Message}");
        }
    }
}
```

## Performance

- Minimal allocations per signal invocation
- Uses MongoDB driver connection pooling
- Async throughout (no blocking I/O)
- Idempotent execution (verification attempted once)
- Efficient collection existence check using MongoDB driver APIs

## Advanced Scenarios

### Replica Set Verification

```csharp
builder.Services.AddMongoDbReadiness(
    "mongodb://mongo1:27017,mongo2:27017,mongo3:27017/?replicaSet=rs0",
    options =>
    {
        options.DatabaseName = "mydb";
        options.Timeout = TimeSpan.FromSeconds(15);
    });
```

### Atlas Cluster

```csharp
var atlasConnectionString = "mongodb+srv://<username>:<password>@cluster0.mongodb.net/";

builder.Services.AddMongoDbReadiness(
    atlasConnectionString,
    options =>
    {
        options.DatabaseName = "production";
        options.VerifyCollection = "users";
    });
```

## License

MIT License. See [LICENSE](../../LICENSE).
