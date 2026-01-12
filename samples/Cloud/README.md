# Cloud Storage Readiness Sample

This sample demonstrates how to use the `Veggerby.Ignition.Azure` and `Veggerby.Ignition.Aws` packages to verify cloud storage readiness during application startup.

## What This Sample Shows

- **Azure Blob Storage** readiness verification
- **Azure Queue Storage** readiness verification
- **Azure Table Storage** readiness verification
- **AWS S3** bucket access verification
- Configuring connection-only checks vs. resource existence verification
- Handling multiple cloud providers in a single application

## Running the Sample

### Prerequisites

Choose one of these options:

#### Option 1: Use Azure Azurite Emulator (Recommended for Testing)

```bash
# Install and run Azurite (Azure Storage emulator)
npm install -g azurite
azurite --silent --location ./azurite-data --debug ./azurite-debug.log
```

The sample uses `UseDevelopmentStorage=true` by default, which connects to Azurite on localhost.

#### Option 2: Use Real Azure Storage Account

```bash
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
```

#### Option 3: Use LocalStack for AWS (Optional)

```bash
# Install and run LocalStack
docker run --rm -p 4566:4566 localstack/localstack

export AWS_ACCESS_KEY_ID=test
export AWS_SECRET_ACCESS_KEY=test
export AWS_REGION=us-east-1
export AWS_S3_BUCKET=ignition-test-bucket
```

### Run the Sample

```bash
dotnet run --project samples/Cloud
```

## Configuration

By default, the sample performs **connection-only** checks without verifying that specific containers, queues, tables, or buckets exist. This is the fastest option and doesn't require pre-provisioning resources.

To enable resource existence verification, edit `Program.cs` and set:

```csharp
options.VerifyContainerExists = true; // For blob containers
options.VerifyQueueExists = true;     // For queues
options.VerifyTableExists = true;     // For tables
options.VerifyBucketAccess = true;    // For S3 buckets
```

**Note:** When verification is enabled, you must either:
- Create the resources manually beforehand, OR
- Set `options.CreateIfNotExists = true` to auto-create missing resources

## Expected Output

### Success (All Services Ready)

```
Cloud Storage Readiness Sample
===============================

Starting cloud storage readiness checks...

Readiness Check Results:
========================
Overall Status: ✓ SUCCESS
Total Duration: 245.32ms
Signals Evaluated: 4

✓ azure-blob-readiness
  Status: Succeeded
  Duration: 87.12ms

✓ azure-queue-readiness
  Status: Succeeded
  Duration: 62.45ms

✓ azure-table-readiness
  Status: Succeeded
  Duration: 54.23ms

✓ s3-readiness
  Status: Succeeded
  Duration: 41.52ms

✓ All cloud storage services are ready!
```

### Failure (Service Unavailable)

```
Cloud Storage Readiness Sample
===============================

Starting cloud storage readiness checks...

Readiness Check Results:
========================
Overall Status: ✗ FAILED
Total Duration: 312.45ms
Signals Evaluated: 4

✓ azure-blob-readiness
  Status: Succeeded
  Duration: 89.23ms

✗ azure-queue-readiness
  Status: Failed
  Duration: 103.45ms
  Error: No connection could be made because the target machine actively refused it.

✓ azure-table-readiness
  Status: Succeeded
  Duration: 67.89ms

✗ s3-readiness
  Status: Failed
  Duration: 51.88ms
  Error: Unable to connect to the remote server

⚠ Some cloud storage services are not ready. Check the errors above.
```

## Key Concepts

### Connection-Only vs. Existence Verification

**Connection-Only** (default):
- Verifies that the storage service is reachable
- Doesn't check specific containers/queues/tables/buckets
- Fast and suitable for most scenarios
- No pre-provisioning required

**Existence Verification**:
- Verifies that specific resources exist
- Requires `ContainerName`, `QueueName`, `TableName`, or `BucketName` to be set
- Can auto-create missing resources with `CreateIfNotExists = true`
- Slightly slower but ensures end-to-end readiness

### Multiple Cloud Providers

This sample shows how to combine Azure and AWS storage checks in the same application. The coordinator waits for all signals to complete before declaring the application ready.

### Timeouts

Each signal has an optional timeout. If not specified, the global coordinator timeout applies. Adjust timeouts based on your environment:

```csharp
options.Timeout = TimeSpan.FromSeconds(5);  // Quick local check
options.Timeout = TimeSpan.FromSeconds(30); // Slower cloud connection
```

## See Also

- [Veggerby.Ignition.Azure README](../../src/Veggerby.Ignition.Azure/README.md)
- [Veggerby.Ignition.Aws README](../../src/Veggerby.Ignition.Aws/README.md)
- [Core Ignition Documentation](../../README.md)
