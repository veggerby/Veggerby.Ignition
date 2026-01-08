# Message Broker Readiness Sample

This sample demonstrates how to use `Veggerby.Ignition.RabbitMq` and `Veggerby.Ignition.MassTransit` packages to verify message broker readiness during application startup.

## Prerequisites

You need a running RabbitMQ instance on `localhost:5672`. The easiest way is using Docker:

```bash
docker run -d -p 5672:5672 --name rabbitmq rabbitmq:3
```

## What This Sample Demonstrates

1. **RabbitMQ Direct Connection Verification**
   - Basic connection and channel creation
   - Optional queue and exchange verification
   - Optional publish/consume round-trip test

2. **MassTransit Bus Readiness**
   - Leveraging MassTransit's built-in health checks
   - Transport-agnostic readiness verification
   - Integration with Ignition coordinator

3. **Coordinator Integration**
   - Parallel verification of multiple messaging concerns
   - BestEffort policy allows graceful degradation
   - Rich diagnostics and timing information

## Running the Sample

```bash
# Make sure RabbitMQ is running
docker ps | grep rabbitmq

# Run the sample
dotnet run

# You should see output like:
# === Starting Ignition Coordinator ===
# 
# === Ignition Complete ===
# Overall Status: Completed
# Duration: 245.12ms
# Signals: 2
#   - rabbitmq-readiness: Succeeded (120.45ms)
#   - masstransit-readiness: Succeeded (124.67ms)
# 
# ✓ All services are ready!
# ✓ MassTransit bus started
# 
# Press Ctrl+C to exit...
```

## Configuration Options

### RabbitMQ Readiness

```csharp
builder.Services.AddRabbitMqReadiness("amqp://localhost", options =>
{
    // Per-signal timeout
    options.Timeout = TimeSpan.FromSeconds(5);
    
    // Verify specific queues exist
    options.WithQueue("orders");
    options.WithQueue("notifications");
    
    // Verify specific exchanges exist
    options.WithExchange("events");
    
    // Perform end-to-end publish/consume test
    options.PerformRoundTripTest = true;
    options.RoundTripTestTimeout = TimeSpan.FromSeconds(3);
    
    // Warn instead of fail on missing topology
    options.FailOnMissingTopology = false;
});
```

### MassTransit Readiness

```csharp
builder.Services.AddMassTransitReadiness(options =>
{
    // Per-signal timeout
    options.Timeout = TimeSpan.FromSeconds(10);
    
    // Max wait for bus to become healthy
    options.BusReadyTimeout = TimeSpan.FromSeconds(30);
});
```

## Handling Failures

### Scenario 1: RabbitMQ Not Running

```
✗ Ignition failed with 1 error(s):
  - Connection failed
```

**Solution**: Start RabbitMQ or adjust connection string.

### Scenario 2: Missing Queue

```
✗ Ignition failed with 1 error(s):
  - Queue 'orders' verification failed
```

**Solutions**:
- Create the queue using RabbitMQ management UI
- Set `FailOnMissingTopology = false` to warn instead of fail
- Remove the queue verification if not needed

### Scenario 3: Timeout

```
✗ Ignition failed with 1 error(s):
  - Signal 'rabbitmq-readiness' timed out after 5 seconds
```

**Solutions**:
- Increase `options.Timeout`
- Increase `options.GlobalTimeout` on the coordinator
- Check network connectivity

## Integration with Health Checks

Both signals integrate with ASP.NET Core health checks:

```csharp
builder.Services.AddIgnition(addHealthCheck: true);
builder.Services.AddRabbitMqReadiness("amqp://localhost");
builder.Services.AddMassTransitReadiness();

app.MapHealthChecks("/health");
```

The `ignition-readiness` health check will include both messaging signals.

## Next Steps

- Explore queue and exchange verification
- Try the round-trip test feature
- Integrate with your existing MassTransit consumers
- Add health check monitoring

## Learn More

- [Veggerby.Ignition Documentation](../../README.md)
- [RabbitMQ Package README](../../src/Veggerby.Ignition.RabbitMq/README.md)
- [MassTransit Package README](../../src/Veggerby.Ignition.MassTransit/README.md)
- [RabbitMQ Documentation](https://www.rabbitmq.com/docs)
- [MassTransit Documentation](https://masstransit.io/)
