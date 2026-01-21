# Veggerby.Ignition.Pulsar.DotPulsar

Apache Pulsar readiness verification package for Veggerby.Ignition using the official [DotPulsar](https://github.com/apache/pulsar-dotpulsar) client.

## Installation

```bash
dotnet add package Veggerby.Ignition.Pulsar.DotPulsar
```

## Quick Start

### Basic Cluster Connectivity

```csharp
using Veggerby.Ignition.Pulsar.DotPulsar;

var builder = WebApplication.CreateBuilder(args);

// Add Pulsar readiness check
builder.Services.AddPulsarReadiness("pulsar://localhost:6650");

// Add Ignition coordinator
builder.Services.AddIgnition();

var app = builder.Build();

// Wait for Pulsar to be ready before starting
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

app.Run();
```

### Topic Verification

```csharp
builder.Services.AddPulsarReadiness("pulsar://localhost:6650", options =>
{
    options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
    options.WithTopic("persistent://public/default/orders");
    options.WithTopic("persistent://public/default/payments");
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

### Producer Test

```csharp
builder.Services.AddPulsarReadiness("pulsar://localhost:6650", options =>
{
    options.VerificationStrategy = PulsarVerificationStrategy.ProducerTest;
    options.WithTopic("persistent://public/default/test-topic");
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

### Subscription Verification

```csharp
builder.Services.AddPulsarReadiness("pulsar://localhost:6650", options =>
{
    options.VerificationStrategy = PulsarVerificationStrategy.SubscriptionCheck;
    options.SubscriptionTopic = "persistent://public/default/orders";
    options.VerifySubscription = "order-processor-subscription";
});
```

### Admin API Health Check

```csharp
builder.Services.AddPulsarReadiness("pulsar://localhost:6650", options =>
{
    options.VerificationStrategy = PulsarVerificationStrategy.AdminApiCheck;
    options.AdminServiceUrl = "http://localhost:8080";
});
```

## Verification Strategies

| Strategy | Description | Configuration Required |
|----------|-------------|------------------------|
| `ClusterHealth` | Validates broker connectivity (default) | None |
| `TopicMetadata` | Verifies specified topics exist | `VerifyTopics` |
| `ProducerTest` | Produces test message to verify producer connectivity | `VerifyTopics` (at least one) |
| `SubscriptionCheck` | Verifies subscription exists for a topic | `VerifySubscription`, `SubscriptionTopic` |
| `AdminApiCheck` | Validates broker health via Admin API | `AdminServiceUrl` |

## Configuration Options

```csharp
public sealed class PulsarReadinessOptions
{
    // Per-signal timeout (default: 30 seconds)
    public TimeSpan? Timeout { get; set; }
    
    // Stage for staged execution (default: null/stage 0)
    public int? Stage { get; set; }
    
    // Max retry attempts (default: 8)
    public int MaxRetries { get; set; }
    
    // Initial retry delay with exponential backoff (default: 500ms)
    public TimeSpan RetryDelay { get; set; }
    
    // Verification strategy (default: ClusterHealth)
    public PulsarVerificationStrategy VerificationStrategy { get; set; }
    
    // Topics to verify
    public List<string> VerifyTopics { get; }
    
    // Fail on missing topics (default: true)
    public bool FailOnMissingTopics { get; set; }
    
    // Subscription name to verify
    public string? VerifySubscription { get; set; }
    
    // Topic for subscription verification
    public string? SubscriptionTopic { get; set; }
    
    // Admin API service URL
    public string? AdminServiceUrl { get; set; }
}
```

## Staged Execution

Use staged execution with Testcontainers to start Pulsar containers before verification:

```csharp
// Stage 0: Start Pulsar container
var infrastructure = new InfrastructureManager();
builder.Services.AddSingleton(infrastructure);
builder.Services.AddIgnitionFromTaskWithStage("pulsar-container",
    async ct => await infrastructure.StartPulsarAsync(), stage: 0);

// Stage 3: Verify Pulsar readiness
builder.Services.AddPulsarReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().PulsarServiceUrl,
    options =>
    {
        options.Stage = 3;
        options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
        options.WithTopic("persistent://public/default/orders");
    });
```

## Topic Name Format

Pulsar topics use a hierarchical naming structure:

```
{persistent|non-persistent}://{tenant}/{namespace}/{topic}
```

If you provide just a topic name (e.g., `"orders"`), it will be normalized to:

```
persistent://public/default/orders
```

You can also provide fully qualified topic names:

```csharp
options.WithTopic("persistent://my-tenant/my-namespace/my-topic");
```

## Retry Policy

Transient failures are automatically retried with exponential backoff:

- Default: 8 retries with 500ms initial delay
- Backoff sequence: 500ms → 1s → 2s → 4s → 8s
- Total retry window: ~15.5 seconds

Customize retry behavior:

```csharp
options.MaxRetries = 5;
options.RetryDelay = TimeSpan.FromMilliseconds(200);
```

## Activity Tracing

OpenTelemetry Activity tags are automatically added when tracing is enabled:

- `pulsar.service.url`: Pulsar service URL
- `pulsar.verification.strategy`: Verification strategy used

## License

MIT License - see LICENSE file for details.
