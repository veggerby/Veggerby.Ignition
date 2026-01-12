# Veggerby.Ignition.Azure

Azure Storage readiness signals for [Veggerby.Ignition](../Veggerby.Ignition/README.md) - verify Azure Blob Storage, Queue Storage, and Table Storage connections during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Azure
```

## Quick Start

```csharp
using Veggerby.Ignition.Azure;

// Azure Blob Storage readiness
builder.Services.AddAzureBlobReadiness(connectionString, options =>
{
    options.ContainerName = "config";
    options.VerifyContainerExists = true;
    options.CreateIfNotExists = false;
    options.Timeout = TimeSpan.FromSeconds(10);
});

// Azure Queue Storage readiness
builder.Services.AddAzureQueueReadiness(connectionString, options =>
{
    options.QueueName = "messages";
    options.VerifyQueueExists = true;
});

// Azure Table Storage readiness
builder.Services.AddAzureTableReadiness(connectionString, options =>
{
    options.TableName = "entities";
    options.VerifyTableExists = true;
});

// Register the coordinator
builder.Services.AddIgnition();

var app = builder.Build();

// Wait for all signals before accepting traffic
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
app.Run();
```

## Features

### Azure Blob Storage

- **Connection verification**: Validates access to Azure Blob Storage account
- **Container existence checks**: Optionally verifies that a specific container exists
- **Auto-provisioning**: Optionally creates missing containers if configured
- **Activity tracing**: Tags include account name, container, verification settings

### Azure Queue Storage

- **Connection verification**: Validates access to Azure Queue Storage account
- **Queue existence checks**: Optionally verifies that a specific queue exists
- **Auto-provisioning**: Optionally creates missing queues if configured
- **Activity tracing**: Tags include account name, queue, verification settings

### Azure Table Storage

- **Connection verification**: Validates access to Azure Table Storage account
- **Table existence checks**: Optionally verifies that a specific table exists
- **Auto-provisioning**: Optionally creates missing tables if configured
- **Activity tracing**: Tags include account name, table, verification settings

## Configuration Options

### AzureBlobReadinessOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `null` | Per-signal timeout (falls back to global timeout if null) |
| `ContainerName` | `string?` | `null` | Container to verify (null = connection-only check) |
| `VerifyContainerExists` | `bool` | `true` | Whether to verify container existence |
| `CreateIfNotExists` | `bool` | `false` | Auto-create container if missing |

### AzureQueueReadinessOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `null` | Per-signal timeout (falls back to global timeout if null) |
| `QueueName` | `string?` | `null` | Queue to verify (null = connection-only check) |
| `VerifyQueueExists` | `bool` | `true` | Whether to verify queue existence |
| `CreateIfNotExists` | `bool` | `false` | Auto-create queue if missing |

### AzureTableReadinessOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `null` | Per-signal timeout (falls back to global timeout if null) |
| `TableName` | `string?` | `null` | Table to verify (null = connection-only check) |
| `VerifyTableExists` | `bool` | `true` | Whether to verify table existence |
| `CreateIfNotExists` | `bool` | `false` | Auto-create table if missing |

## Advanced Usage

### Using Existing Clients

If you already have Azure Storage clients registered in your DI container, use the overload without connection string:

```csharp
// Register clients first
services.AddSingleton(new BlobServiceClient(connectionString));
services.AddSingleton(new QueueServiceClient(connectionString));
services.AddSingleton(new TableServiceClient(connectionString));

// Then register readiness signals
services.AddAzureBlobReadiness(options => options.ContainerName = "config");
services.AddAzureQueueReadiness(options => options.QueueName = "messages");
services.AddAzureTableReadiness(options => options.TableName = "entities");
```

### Managed Identity Authentication

```csharp
using Azure.Identity;

// Use DefaultAzureCredential for managed identity support
var blobServiceClient = new BlobServiceClient(
    new Uri("https://<account>.blob.core.windows.net"),
    new DefaultAzureCredential());

services.AddSingleton(blobServiceClient);
services.AddAzureBlobReadiness(options =>
{
    options.ContainerName = "config";
    options.VerifyContainerExists = true;
});
```

### Connection-Only Verification

For lightweight checks that only verify connectivity without checking specific containers/queues/tables:

```csharp
services.AddAzureBlobReadiness(connectionString);
services.AddAzureQueueReadiness(connectionString);
services.AddAzureTableReadiness(connectionString);
```

## Error Handling

- **Connection failures**: Throws exceptions when Azure Storage is unreachable
- **Missing containers/queues/tables**: Throws `InvalidOperationException` when `VerifyContainerExists`/`VerifyQueueExists`/`VerifyTableExists` is `true` and resource doesn't exist (unless `CreateIfNotExists` is `true`)
- **Permission errors**: Propagates Azure SDK exceptions for authentication/authorization failures
- **Timeout handling**: Respects per-signal or global timeout configuration

## Dependencies

- `Azure.Storage.Blobs` (v12.23.0+)
- `Azure.Storage.Queues` (v12.21.0+)
- `Azure.Data.Tables` (v12.9.1+)
- `Veggerby.Ignition` (core library)

## See Also

- [Veggerby.Ignition](../Veggerby.Ignition/README.md) - Core library
- [Veggerby.Ignition.Aws](../Veggerby.Ignition.Aws/README.md) - AWS S3 readiness signals
- [Azure Storage Documentation](https://learn.microsoft.com/azure/storage/)
