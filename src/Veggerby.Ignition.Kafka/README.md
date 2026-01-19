# Veggerby.Ignition.Kafka

Kafka readiness verification package for Veggerby.Ignition. Validates Apache Kafka cluster connectivity, topic existence, producer functionality, and consumer group registration during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Kafka
```

## Quick Start

### Basic Cluster Connectivity

```csharp
using Veggerby.Ignition.Kafka;

builder.Services.AddKafkaReadiness("localhost:9092");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### Topic Verification

```csharp
builder.Services.AddKafkaReadiness("localhost:9092", options =>
{
    options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
    options.WithTopic("orders");
    options.WithTopic("payments");
    options.WithTopic("notifications");
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

### Producer Test

```csharp
builder.Services.AddKafkaReadiness("localhost:9092", options =>
{
    options.VerificationStrategy = KafkaVerificationStrategy.ProducerTest;
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

### Consumer Group Verification

```csharp
builder.Services.AddKafkaReadiness("localhost:9092", options =>
{
    options.VerificationStrategy = KafkaVerificationStrategy.ConsumerGroupCheck;
    options.VerifyConsumerGroup = "order-processing";
    options.Timeout = TimeSpan.FromSeconds(20);
});
```

## Verification Strategies

### ClusterMetadata (Default)

Fast and lightweight—validates broker connectivity by retrieving cluster metadata.

```csharp
options.VerificationStrategy = KafkaVerificationStrategy.ClusterMetadata;
```

**Use when:**
- You need basic Kafka availability checks
- Performance is critical
- Topics are created externally

### TopicMetadata

Verifies that specified topics exist by retrieving topic metadata.

```csharp
options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
options.WithTopic("events");
options.WithTopic("commands");
options.FailOnMissingTopics = true; // Default
```

**Use when:**
- Your application requires specific topics to exist
- You want to fail fast if topics are missing
- Topics are pre-created by infrastructure tools

### ProducerTest

Produces a test message to a temporary topic to verify end-to-end producer connectivity.

```csharp
options.VerificationStrategy = KafkaVerificationStrategy.ProducerTest;
```

**Use when:**
- You need comprehensive producer verification
- You want to test message persistence
- Network/permission issues should fail startup

**Note:** Creates and deletes a temporary topic (`__ignition_test_*`). Requires administrative permissions.

### ConsumerGroupCheck

Verifies that the specified consumer group exists by listing consumer groups.

```csharp
options.VerificationStrategy = KafkaVerificationStrategy.ConsumerGroupCheck;
options.VerifyConsumerGroup = "my-consumer-group";
```

**Use when:**
- Your application depends on a specific consumer group
- You want to verify consumer registration
- Consumer offset management is critical

## Advanced Configuration

### Custom Producer Configuration

```csharp
var producerConfig = new ProducerConfig
{
    BootstrapServers = "kafka.example.com:9093",
    SecurityProtocol = SecurityProtocol.SaslSsl,
    SaslMechanism = SaslMechanism.Plain,
    SaslUsername = "user",
    SaslPassword = "password"
};

builder.Services.AddKafkaReadiness(producerConfig, options =>
{
    options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
    options.WithTopic("secure-topic");
});
```

### Retry Configuration

```csharp
builder.Services.AddKafkaReadiness("localhost:9092", options =>
{
    options.MaxRetries = 5;
    options.RetryDelay = TimeSpan.FromMilliseconds(500);
    options.Timeout = TimeSpan.FromSeconds(60);
});
```

### Schema Registry Verification (Confluent)

```csharp
builder.Services.AddKafkaReadiness("localhost:9092", options =>
{
    options.SchemaRegistryUrl = "http://localhost:8081";
    options.VerifySchemaRegistry = true;
});
```

**Note:** Schema Registry verification is optional and Confluent-specific. Uses HTTP health check.

## Testcontainers Integration

### Staged Execution with Testcontainers

```csharp
// Stage 0: Start Kafka container
var infrastructure = new InfrastructureManager();
builder.Services.AddSingleton(infrastructure);
builder.Services.AddIgnitionFromTaskWithStage("kafka-container",
    async ct => await infrastructure.StartKafkaAsync(), stage: 0);

// Stage 3: Verify Kafka readiness
builder.Services.AddKafkaReadiness(
    sp => sp.GetRequiredService<InfrastructureManager>().KafkaBootstrapServers,
    options =>
    {
        options.Stage = 3;
        options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
        options.WithTopic("test-topic");
        options.Timeout = TimeSpan.FromSeconds(30);
    });
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | `15s` | Per-signal timeout override |
| `Stage` | `int?` | `null` | Staged execution phase |
| `MaxRetries` | `int` | `3` | Maximum retry attempts for transient failures |
| `RetryDelay` | `TimeSpan` | `200ms` | Initial delay between retries (exponential backoff) |
| `VerificationStrategy` | `KafkaVerificationStrategy` | `ClusterMetadata` | Verification approach |
| `VerifyTopics` | `List<string>` | Empty | Topics to verify (TopicMetadata strategy) |
| `FailOnMissingTopics` | `bool` | `true` | Fail if topics don't exist |
| `VerifyConsumerGroup` | `string?` | `null` | Consumer group to verify (ConsumerGroupCheck strategy) |
| `SchemaRegistryUrl` | `string?` | `null` | Schema Registry URL (Confluent-specific) |
| `VerifySchemaRegistry` | `bool` | `false` | Enable Schema Registry verification |

## Activity Tracing

The Kafka readiness signal integrates with OpenTelemetry Activity tracing:

```csharp
builder.Services.AddIgnition(options =>
{
    options.EnableTracing = true;
});
```

**Activity Tags:**
- `kafka.bootstrap.servers`: Bootstrap servers
- `kafka.verification.strategy`: Verification strategy used

## Error Handling

### Missing Topics

```csharp
options.FailOnMissingTopics = false; // Log warning instead of failing
```

### Connection Failures

The signal automatically retries transient connection failures with exponential backoff:

```csharp
options.MaxRetries = 5;
options.RetryDelay = TimeSpan.FromMilliseconds(500);
```

### Timeout Behavior

- Per-signal timeout (`options.Timeout`) overrides global coordinator timeout
- Producer test creates temporary topics—requires sufficient timeout
- Consumer group checks are fast—10-15 seconds typically sufficient

## Best Practices

1. **Use ClusterMetadata for basic checks**: Fastest and most reliable for connection-only verification
2. **Verify topics in production**: Use `TopicMetadata` to fail fast if required topics don't exist
3. **ProducerTest for critical pipelines**: Validates end-to-end message persistence but adds overhead
4. **Set appropriate timeouts**: Kafka clusters can take 10-30 seconds to respond under load
5. **Use staged execution with Testcontainers**: Ensures containers start before verification
6. **Configure retries for reliability**: Transient network issues are common in distributed systems

## Dependencies

- **Confluent.Kafka**: Official .NET client library (~1.9MB)
- **Veggerby.Ignition**: Core readiness coordination library

## License

Same license as Veggerby.Ignition (see root repository LICENSE file).
