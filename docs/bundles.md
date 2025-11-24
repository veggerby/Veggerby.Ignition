# Ignition Bundles Guide

This guide covers Veggerby.Ignition's bundle system, which enables reusable, composable signal packages
that can be registered as a unit.

## What are Bundles?

Bundles are packaged sets of related ignition signals with pre-configured dependencies and options.
They eliminate the need to manually register multiple signals individually and enable ecosystem modules
for common initialization patterns.

### Benefits of Bundles

- **Reusability**: Package common patterns once, use everywhere
- **Composability**: Combine multiple bundles in an application
- **Ecosystem**: Share bundles across teams and projects
- **Consistency**: Ensure correct initialization order and configuration
- **Maintainability**: Update bundle logic without changing consuming code

### When to Create a Bundle

Create a bundle when you have:

- ✅ Multiple related signals that always work together
- ✅ Complex dependency relationships to encapsulate
- ✅ Initialization patterns used across multiple projects
- ✅ Third-party integrations (Redis, Kafka, messaging, etc.)
- ✅ Reusable infrastructure warmup sequences

Don't create a bundle for:

- ❌ Single signals (use `AddIgnitionSignal` instead)
- ❌ Application-specific logic with no reuse
- ❌ Signals with no relationship

## Built-in Bundles

Veggerby.Ignition includes two built-in bundles for common scenarios.

### HttpDependencyBundle

Verifies HTTP endpoint readiness by performing GET requests.

#### Single Endpoint

```csharp
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle(
        "https://api.example.com/health",
        TimeSpan.FromSeconds(10)));
```

Creates a signal named `http-dependency:api.example.com` that:

- Performs a GET request to the specified URL
- Succeeds if status code is 2xx
- Fails if status code is not 2xx or request times out
- Uses the specified timeout (10 seconds)

#### Multiple Endpoints

```csharp
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle(
        new[]
        {
            "https://api1.example.com/ready",
            "https://api2.example.com/ready",
            "https://api3.example.com/ready"
        },
        TimeSpan.FromSeconds(5)));
```

Creates three signals:

- `http-dependency:api1.example.com`
- `http-dependency:api2.example.com`
- `http-dependency:api3.example.com`

All execute in parallel (if using Parallel execution mode).

#### Customize Timeout via Bundle Options

```csharp
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle("https://slow-api.example.com"),
    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(30));
```

The bundle's `DefaultTimeout` applies to signals that don't specify their own timeout.

#### Use Cases

- **Microservice dependencies**: Verify dependent services are available
- **Health check warmup**: Ensure endpoints respond before routing traffic
- **Circuit breaker coordination**: Don't start if upstream services are down

### DatabaseTrioBundle

Represents a typical database initialization sequence: connect → validate schema → warm up data.

#### Full Trio

```csharp
builder.Services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        databaseName: "primary-db",
        connectFactory: ct => dbConnection.OpenAsync(ct),
        validateSchemaFactory: ct => schemaValidator.ValidateAsync(ct),
        warmupFactory: ct => dataCache.WarmAsync(ct),
        defaultTimeout: TimeSpan.FromSeconds(15)));
```

Creates three signals with dependencies:

```text
db:primary-db:connect
        |
db:primary-db:validate-schema
        |
db:primary-db:warmup
```

- **Connect**: Opens database connection
- **Validate Schema**: Checks schema validity (depends on Connect)
- **Warmup**: Warms cache/loads data (depends on Validate Schema)

#### Connection and Warmup Only

```csharp
builder.Services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        databaseName: "replica-db",
        connectFactory: ct => replicaConnection.OpenAsync(ct),
        warmupFactory: ct => replicaCache.WarmAsync(ct)));
```

Creates two signals:

```text
db:replica-db:connect
        |
db:replica-db:warmup
```

Schema validation is omitted when `validateSchemaFactory` is null.

#### Use Cases

- **Primary database initialization**: Connect → migrate → seed
- **Replica database warmup**: Connect → warm cache
- **Multi-database applications**: Multiple bundles for different databases

#### Dependency Graph

The bundle automatically configures dependencies:

- If all three factories provided: warmup depends on validate, validate depends on connect
- If only connect and warmup: warmup depends on connect

## Creating Custom Bundles

Implement `IIgnitionBundle` to create reusable signal modules.

### Basic Bundle Structure

```csharp
using Veggerby.Ignition;
using Veggerby.Ignition.Bundles;

public sealed class RedisStarterBundle : IIgnitionBundle
{
    private readonly string _connectionString;

    public RedisStarterBundle(string connectionString)
    {
        _connectionString = connectionString;
    }

    public string Name => "RedisStarter";

    public void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        // Register signals
        services.AddIgnitionFromTask(
            "redis:connect",
            ct => ConnectAsync(_connectionString, ct),
            options.DefaultTimeout ?? TimeSpan.FromSeconds(10));

        services.AddIgnitionFromTask(
            "redis:health",
            ct => HealthCheckAsync(ct),
            options.DefaultTimeout ?? TimeSpan.FromSeconds(5));

        services.AddIgnitionFromTask(
            "redis:warmup",
            ct => WarmupCacheAsync(ct),
            options.DefaultTimeout ?? TimeSpan.FromSeconds(20));
    }

    private async Task ConnectAsync(string connStr, CancellationToken ct)
    {
        // Connection logic
        await Task.Delay(1000, ct); // Simulated
    }

    private async Task HealthCheckAsync(CancellationToken ct)
    {
        // Ping Redis
        await Task.Delay(500, ct); // Simulated
    }

    private async Task WarmupCacheAsync(CancellationToken ct)
    {
        // Pre-populate cache
        await Task.Delay(2000, ct); // Simulated
    }
}
```

### Register the Bundle

```csharp
builder.Services.AddIgnitionBundle(
    new RedisStarterBundle("localhost:6379"),
    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(15));
```

### Bundle with Dependencies

Add dependency relationships between bundle signals:

```csharp
public void ConfigureBundle(
    IServiceCollection services,
    Action<IgnitionBundleOptions>? configure = null)
{
    var options = new IgnitionBundleOptions();
    configure?.Invoke(options);

    // Register signals
    services.AddIgnitionFromTask(
        "redis:connect",
        ct => ConnectAsync(ct),
        options.DefaultTimeout);

    services.AddIgnitionFromTask(
        "redis:health",
        ct => HealthCheckAsync(ct),
        options.DefaultTimeout);

    services.AddIgnitionFromTask(
        "redis:warmup",
        ct => WarmupCacheAsync(ct),
        options.DefaultTimeout);

    // Define dependency graph
    services.AddIgnitionGraph((builder, sp) =>
    {
        var signals = sp.GetServices<IIgnitionSignal>();
        var connectSig = signals.First(s => s.Name == "redis:connect");
        var healthSig = signals.First(s => s.Name == "redis:health");
        var warmupSig = signals.First(s => s.Name == "redis:warmup");

        builder.AddSignals(new[] { connectSig, healthSig, warmupSig });
        builder.DependsOn(healthSig, connectSig);   // health after connect
        builder.DependsOn(warmupSig, healthSig);    // warmup after health
    });
}
```

Dependency graph:

```text
redis:connect → redis:health → redis:warmup
```

### Bundle with DI-Resolved Services

Access services from the DI container within your bundle:

```csharp
public void ConfigureBundle(
    IServiceCollection services,
    Action<IgnitionBundleOptions>? configure = null)
{
    var options = new IgnitionBundleOptions();
    configure?.Invoke(options);

    // Register bundle's own services
    services.AddSingleton<IRedisConnection>(sp =>
        new RedisConnection(_connectionString));

    // Create signals using DI
    services.AddIgnitionFromTask(
        "redis:connect",
        async ct =>
        {
            var sp = services.BuildServiceProvider();
            var connection = sp.GetRequiredService<IRedisConnection>();
            await connection.OpenAsync(ct);
        },
        options.DefaultTimeout);
}
```

**Warning**: Avoid `services.BuildServiceProvider()` in bundles when possible. Prefer signal factories that resolve services:

```csharp
// Better: Use factory that receives IServiceProvider
services.AddIgnitionFromFactory(
    taskFactory: sp =>
    {
        var connection = sp.GetRequiredService<IRedisConnection>();
        return connection.OpenAsync(default);
    },
    name: "redis:connect",
    timeout: options.DefaultTimeout);
```

## Advanced Bundle Patterns

### Parameterized Bundle

Accept configuration parameters:

```csharp
public sealed class KafkaConsumerBundle : IIgnitionBundle
{
    private readonly string _topic;
    private readonly string _groupId;
    private readonly string[] _brokers;

    public KafkaConsumerBundle(string topic, string groupId, params string[] brokers)
    {
        _topic = topic;
        _groupId = groupId;
        _brokers = brokers;
    }

    public string Name => $"KafkaConsumer[{_topic}]";

    public void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        // Use parameters in signal configuration
        services.AddIgnitionFromTask(
            $"kafka:{_topic}:connect",
            ct => ConnectToBrokersAsync(_brokers, ct),
            configure?.DefaultTimeout);

        services.AddIgnitionFromTask(
            $"kafka:{_topic}:subscribe",
            ct => SubscribeToTopicAsync(_topic, _groupId, ct),
            configure?.DefaultTimeout);
    }
}

// Usage
builder.Services.AddIgnitionBundle(
    new KafkaConsumerBundle(
        topic: "orders",
        groupId: "order-processor",
        brokers: new[] { "kafka1:9092", "kafka2:9092" }));
```

### Conditional Signal Registration

Register signals based on configuration:

```csharp
public void ConfigureBundle(
    IServiceCollection services,
    Action<IgnitionBundleOptions>? configure = null)
{
    var options = new IgnitionBundleOptions();
    configure?.Invoke(options);

    // Always register connection
    services.AddIgnitionFromTask(
        "db:connect",
        ct => ConnectAsync(ct),
        options.DefaultTimeout);

    // Conditionally register migration
    if (_runMigrations)
    {
        services.AddIgnitionFromTask(
            "db:migrate",
            ct => MigrateAsync(ct),
            options.DefaultTimeout);
    }

    // Conditionally register seeding
    if (_seedData)
    {
        services.AddIgnitionFromTask(
            "db:seed",
            ct => SeedAsync(ct),
            options.DefaultTimeout);
    }
}
```

### Multi-Instance Bundle

Create multiple instances of the same bundle:

```csharp
// Primary database
builder.Services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        "primary",
        ct => primary.ConnectAsync(ct),
        ct => validator.ValidateAsync(ct),
        ct => cache.WarmAsync(ct)));

// Replica database
builder.Services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        "replica",
        ct => replica.ConnectAsync(ct),
        warmupFactory: ct => replicaCache.WarmAsync(ct))); // No schema validation

// Analytics database
builder.Services.AddIgnitionBundle(
    new DatabaseTrioBundle(
        "analytics",
        ct => analytics.ConnectAsync(ct),
        warmupFactory: ct => analyticsCache.WarmAsync(ct)));
```

### Nested Bundle Dependencies

Create dependencies between signals from different bundles:

```csharp
// Register bundles
builder.Services.AddIgnitionBundle(new DatabaseBundle("primary"));
builder.Services.AddIgnitionBundle(new CacheBundle("redis"));

// Wire cross-bundle dependencies
builder.Services.AddIgnitionGraph((graphBuilder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    var dbConnect = signals.First(s => s.Name == "db:primary:connect");
    var cacheWarmup = signals.First(s => s.Name == "redis:warmup");

    graphBuilder.AddSignals(new[] { dbConnect, cacheWarmup });
    graphBuilder.DependsOn(cacheWarmup, dbConnect); // Cache after database
});
```

## Testing Bundles in Isolation

### Unit Test Bundle Registration

```csharp
[Fact]
public void Bundle_Registers_Expected_Signals()
{
    // Arrange
    var services = new ServiceCollection();
    var bundle = new RedisStarterBundle("localhost:6379");

    // Act
    services.AddIgnitionBundle(bundle);
    var sp = services.BuildServiceProvider();
    var signals = sp.GetServices<IIgnitionSignal>().ToList();

    // Assert
    signals.Should().HaveCount(3);
    signals.Should().Contain(s => s.Name == "redis:connect");
    signals.Should().Contain(s => s.Name == "redis:health");
    signals.Should().Contain(s => s.Name == "redis:warmup");
}
```

### Integration Test Bundle

```csharp
[Fact]
public async Task Bundle_Signals_Execute_Successfully()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddIgnition(options =>
    {
        options.GlobalTimeout = TimeSpan.FromSeconds(30);
        options.Policy = IgnitionPolicy.FailFast;
    });
    services.AddIgnitionBundle(new RedisStarterBundle("localhost:6379"));

    var sp = services.BuildServiceProvider();
    var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

    // Act
    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();

    // Assert
    result.TimedOut.Should().BeFalse();
    result.Results.Should().AllSatisfy(r =>
        r.Status.Should().Be(IgnitionSignalStatus.Succeeded));
}
```

### Test Bundle with Mocks

```csharp
public sealed class TestableRedisBundle : IIgnitionBundle
{
    private readonly Func<CancellationToken, Task> _connectFactory;
    private readonly Func<CancellationToken, Task> _healthFactory;
    private readonly Func<CancellationToken, Task> _warmupFactory;

    public TestableRedisBundle(
        Func<CancellationToken, Task> connectFactory,
        Func<CancellationToken, Task> healthFactory,
        Func<CancellationToken, Task> warmupFactory)
    {
        _connectFactory = connectFactory;
        _healthFactory = healthFactory;
        _warmupFactory = warmupFactory;
    }

    public string Name => "TestableRedis";

    public void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        services.AddIgnitionFromTask("redis:connect", _connectFactory);
        services.AddIgnitionFromTask("redis:health", _healthFactory);
        services.AddIgnitionFromTask("redis:warmup", _warmupFactory);
    }
}

// Test
[Fact]
public async Task Bundle_Handles_Connection_Failure()
{
    // Arrange
    var connectMock = Substitute.For<Func<CancellationToken, Task>>();
    connectMock(Arg.Any<CancellationToken>())
        .Returns(Task.FromException(new InvalidOperationException("Connection failed")));

    var bundle = new TestableRedisBundle(
        connectMock,
        ct => Task.CompletedTask,
        ct => Task.CompletedTask);

    var services = new ServiceCollection();
    services.AddLogging();
    services.AddIgnition(options => options.Policy = IgnitionPolicy.BestEffort);
    services.AddIgnitionBundle(bundle);

    var sp = services.BuildServiceProvider();
    var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

    // Act
    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();

    // Assert
    var connectResult = result.Results.First(r => r.Name == "redis:connect");
    connectResult.Status.Should().Be(IgnitionSignalStatus.Failed);
    connectResult.Exception.Should().BeOfType<InvalidOperationException>();
}
```

## Publishing and Sharing Bundles

### Package as NuGet Library

Create a class library for your bundle:

```bash
dotnet new classlib -n MyCompany.Ignition.RedisBundles
cd MyCompany.Ignition.RedisBundles
dotnet add package Veggerby.Ignition
```

Include bundle implementation and pack:

```bash
dotnet pack -c Release
```

Publish to NuGet or internal feed:

```bash
dotnet nuget push bin/Release/MyCompany.Ignition.RedisBundles.1.0.0.nupkg
```

### Bundle Naming Conventions

- **Namespace**: `<Company>.Ignition.<Technology>Bundles`
- **Class name**: `<Technology><Purpose>Bundle`
- **Signal names**: `<technology>:<instance>:<operation>`

Examples:

- `MyCompany.Ignition.RedisBundles.RedisStarterBundle`
- `MyCompany.Ignition.KafkaBundles.KafkaConsumerBundle`
- `MyCompany.Ignition.DatabaseBundles.PostgresTrioBundle`

### Documentation Template

Provide clear documentation for consumers:

```markdown
# MyCompany.Ignition.RedisBundles

Ignition bundles for Redis initialization and warmup.

## Installation

```bash
dotnet add package MyCompany.Ignition.RedisBundles
```

## Usage

```csharp
builder.Services.AddIgnitionBundle(
    new RedisStarterBundle("localhost:6379"),
    opts => opts.DefaultTimeout = TimeSpan.FromSeconds(15));
```

## Signals Created

- `redis:connect`: Establishes Redis connection
- `redis:health`: Performs PING health check
- `redis:warmup`: Pre-populates cache with critical keys

## Dependencies

- `redis:health` depends on `redis:connect`
- `redis:warmup` depends on `redis:health`

## Configuration Options

- `connectionString`: Redis connection string (required)
- `DefaultTimeout`: Timeout for all signals (default: 10s)

```

## Real-World Bundle Examples

### Example 1: ElasticSearch Starter Bundle

```csharp
public sealed class ElasticSearchBundle : IIgnitionBundle
{
    private readonly string _clusterUrl;
    private readonly string _indexName;

    public ElasticSearchBundle(string clusterUrl, string indexName)
    {
        _clusterUrl = clusterUrl;
        _indexName = indexName;
    }

    public string Name => "ElasticSearch";

    public void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        services.AddIgnitionFromTask(
            "elasticsearch:connect",
            ct => ConnectToClusterAsync(_clusterUrl, ct),
            options.DefaultTimeout ?? TimeSpan.FromSeconds(10));

        services.AddIgnitionFromTask(
            "elasticsearch:index-check",
            ct => EnsureIndexExistsAsync(_indexName, ct),
            options.DefaultTimeout ?? TimeSpan.FromSeconds(5));

        services.AddIgnitionFromTask(
            "elasticsearch:warmup",
            ct => WarmupQueryCacheAsync(_indexName, ct),
            options.DefaultTimeout ?? TimeSpan.FromSeconds(20));

        services.AddIgnitionGraph((builder, sp) =>
        {
            var signals = sp.GetServices<IIgnitionSignal>();
            var connect = signals.First(s => s.Name == "elasticsearch:connect");
            var indexCheck = signals.First(s => s.Name == "elasticsearch:index-check");
            var warmup = signals.First(s => s.Name == "elasticsearch:warmup");

            builder.AddSignals(new[] { connect, indexCheck, warmup });
            builder.DependsOn(indexCheck, connect);
            builder.DependsOn(warmup, indexCheck);
        });
    }

    private async Task ConnectToClusterAsync(string url, CancellationToken ct) { /* ... */ }
    private async Task EnsureIndexExistsAsync(string index, CancellationToken ct) { /* ... */ }
    private async Task WarmupQueryCacheAsync(string index, CancellationToken ct) { /* ... */ }
}
```

### Example 2: Multi-Service Dependency Bundle

```csharp
public sealed class MicroserviceDependencyBundle : IIgnitionBundle
{
    private readonly Dictionary<string, string> _serviceEndpoints;

    public MicroserviceDependencyBundle(Dictionary<string, string> serviceEndpoints)
    {
        _serviceEndpoints = serviceEndpoints;
    }

    public string Name => "MicroserviceDependencies";

    public void ConfigureBundle(
        IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions();
        configure?.Invoke(options);

        foreach (var (serviceName, endpoint) in _serviceEndpoints)
        {
            services.AddIgnitionBundle(
                new HttpDependencyBundle(endpoint, options.DefaultTimeout ?? TimeSpan.FromSeconds(5)));
        }
    }
}

// Usage
builder.Services.AddIgnitionBundle(
    new MicroserviceDependencyBundle(new Dictionary<string, string>
    {
        ["auth-service"] = "https://auth.internal/health",
        ["user-service"] = "https://users.internal/health",
        ["payment-service"] = "https://payments.internal/health"
    }));
```

## Bundle Options Reference

`IgnitionBundleOptions` provides configuration for bundles:

```csharp
public class IgnitionBundleOptions
{
    /// <summary>
    /// Default timeout applied to bundle signals that don't specify their own timeout.
    /// </summary>
    public TimeSpan? DefaultTimeout { get; set; }

    /// <summary>
    /// Reserved for future use: policy override for bundle signals.
    /// </summary>
    public IgnitionPolicy? Policy { get; set; }
}
```

### Applying Options

```csharp
builder.Services.AddIgnitionBundle(
    new MyBundle(),
    opts =>
    {
        opts.DefaultTimeout = TimeSpan.FromSeconds(30);
        // opts.Policy = IgnitionPolicy.BestEffort; // Reserved for future
    });
```

Individual signal timeouts override bundle defaults:

```csharp
services.AddIgnitionFromTask(
    "my-signal",
    ct => DoWorkAsync(ct),
    TimeSpan.FromSeconds(5)); // Overrides bundle's DefaultTimeout
```

## Best Practices

### 1. Single Responsibility

Each bundle should encapsulate one initialization domain:

```csharp
// ✓ Good: Focused on Redis
public class RedisStarterBundle { }

// ✗ Bad: Mixed concerns
public class RedisAndDatabaseBundle { }
```

### 2. Clear Signal Naming

Use consistent, hierarchical naming:

```csharp
// Pattern: <technology>:<instance>:<operation>
"redis:primary:connect"
"redis:primary:warmup"
"kafka:orders:subscribe"
```

### 3. Document Dependencies

Clearly document signal dependency structure:

```csharp
/// <summary>
/// Dependency graph:
///   connect → health → warmup
/// </summary>
public void ConfigureBundle(...) { }
```

### 4. Provide Sensible Defaults

Set reasonable default timeouts:

```csharp
options.DefaultTimeout ?? TimeSpan.FromSeconds(10) // Fallback to 10s
```

### 5. Make Bundles Testable

Design for dependency injection and testing:

```csharp
// Accept factories instead of hard-coding
public MyBundle(Func<CancellationToken, Task> connectFactory) { }
```

## Related Topics

- [Getting Started](getting-started.md) - Basic signal registration
- [Dependency-Aware Execution](dependency-aware-execution.md) - Creating dependency graphs in bundles
- [Advanced Patterns](advanced-patterns.md) - Complex bundle scenarios
- [API Reference](api-reference.md) - IIgnitionBundle interface details
