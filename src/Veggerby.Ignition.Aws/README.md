# Veggerby.Ignition.Aws

AWS S3 readiness signals for [Veggerby.Ignition](../Veggerby.Ignition/README.md) - verify S3 bucket access during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Aws
```

## Quick Start

```csharp
using Veggerby.Ignition.Aws;

// AWS S3 bucket readiness
builder.Services.AddS3Readiness("my-bucket", options =>
{
    options.Region = "us-east-1";
    options.VerifyBucketAccess = true;
    options.Timeout = TimeSpan.FromSeconds(10);
});

// Register the coordinator
builder.Services.AddIgnition();

var app = builder.Build();

// Wait for all signals before accepting traffic
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
app.Run();
```

## Features

### AWS S3 Bucket Verification

- **Connection verification**: Validates access to AWS S3 service
- **Bucket access checks**: Verifies that a specific bucket exists and is accessible
- **Lightweight verification**: Uses GetBucketLocation API for minimal overhead
- **Activity tracing**: Tags include bucket name, region, verification settings
- **IAM role support**: Works with default AWS credential providers (environment variables, IAM roles, profiles)

## Configuration Options

### S3ReadinessOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `null` | Per-signal timeout (falls back to global timeout if null) |
| `BucketName` | `string?` | `null` | Bucket to verify (null = service-level check only) |
| `Region` | `string?` | `null` | AWS region (null = use client default) |
| `VerifyBucketAccess` | `bool` | `true` | Whether to verify bucket existence and access |

## Advanced Usage

### Using Existing S3 Client

If you already have an S3 client registered in your DI container:

```csharp
using Amazon;
using Amazon.S3;

// Register client first
services.AddSingleton<IAmazonS3>(sp =>
    new AmazonS3Client(RegionEndpoint.USEast1));

// Then register readiness signal
services.AddS3Readiness(options =>
{
    options.BucketName = "my-bucket";
    options.VerifyBucketAccess = true;
});
```

### IAM Role Authentication

The package uses AWS SDK's default credential provider chain, which automatically supports:

- Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
- IAM roles for EC2 instances or ECS tasks
- AWS credentials file (`~/.aws/credentials`)
- IAM roles for service accounts (EKS)

No additional configuration is needed for IAM role authentication:

```csharp
// Will automatically use IAM role if running on EC2/ECS/EKS
services.AddS3Readiness("my-bucket", options =>
{
    options.Region = "us-west-2";
});
```

### Explicit Credentials

For explicit credentials, register a custom S3 client:

```csharp
using Amazon.Runtime;
using Amazon.S3;

services.AddSingleton<IAmazonS3>(sp =>
{
    var credentials = new BasicAWSCredentials(accessKey, secretKey);
    return new AmazonS3Client(credentials, RegionEndpoint.USEast1);
});

services.AddS3Readiness(options => options.BucketName = "my-bucket");
```

### Connection-Only Verification

For lightweight checks that only verify S3 service connectivity:

```csharp
services.AddS3Readiness(options =>
{
    options.BucketName = null; // or omit BucketName
    options.VerifyBucketAccess = false;
});
```

### Cross-Region Buckets

```csharp
services.AddS3Readiness("my-us-bucket", options =>
{
    options.Region = "us-east-1";
});

services.AddS3Readiness("my-eu-bucket", options =>
{
    options.Region = "eu-west-1";
});
```

## Error Handling

- **Connection failures**: Throws exceptions when AWS S3 is unreachable
- **Missing buckets**: Throws `InvalidOperationException` when `VerifyBucketAccess` is `true` and bucket doesn't exist or isn't accessible
- **Permission errors**: Propagates AWS SDK exceptions for authentication/authorization failures
- **Timeout handling**: Respects per-signal or global timeout configuration

## Dependencies

- `AWSSDK.S3` (v3.7.410.11+)
- `Veggerby.Ignition` (core library)

## See Also

- [Veggerby.Ignition](../Veggerby.Ignition/README.md) - Core library
- [Veggerby.Ignition.Azure](../Veggerby.Ignition.Azure/README.md) - Azure Storage readiness signals
- [AWS S3 Documentation](https://docs.aws.amazon.com/s3/)
