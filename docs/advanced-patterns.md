# Advanced Patterns

This guide covers advanced usage patterns, testing strategies, and integration techniques for Veggerby.Ignition.

## Composite Signals with AddIgnitionForAll

Use `AddIgnitionForAll` to create a single signal that waits for multiple service instances.

### Pattern

```csharp
// Register multiple instances
builder.Services.AddSingleton<ShardIndexer>(new ShardIndexer("shard-1"));
builder.Services.AddSingleton<ShardIndexer>(new ShardIndexer("shard-2"));
builder.Services.AddSingleton<ShardIndexer>(new ShardIndexer("shard-3"));

// Create composite signal
builder.Services.AddIgnitionForAll<ShardIndexer>(
    indexer => indexer.ReadyTask,
    groupName: "shard-indexers");
```

### Behavior

- Resolves all registered instances of `ShardIndexer`
- Creates a single signal that waits for all instances to complete
- Signal succeeds only if all instances succeed

### Use Cases

- Multiple database shards
- Multiple Kafka consumers
- Multiple worker instances
- Parallel batch processors

### Example: Multiple Kafka Consumers

```csharp
public class KafkaConsumer
{
    private readonly string _topic;
    private readonly TaskCompletionSource _readyTcs = new();

    public KafkaConsumer(string topic)
    {
        _topic = topic;
    }

    public Task ReadyTask => _readyTcs.Task;

    public async Task StartAsync(CancellationToken ct)
    {
        // Connect and subscribe
        await ConnectAsync(ct);
        await SubscribeAsync(_topic, ct);

        _readyTcs.Ignited();

        // Continue consuming
        await ConsumeAsync(ct);
    }

    private async Task ConnectAsync(CancellationToken ct) { /* ... */ }
    private async Task SubscribeAsync(string topic, CancellationToken ct) { /* ... */ }
    private async Task ConsumeAsync(CancellationToken ct) { /* ... */ }
}

// Registration
builder.Services.AddSingleton(new KafkaConsumer("orders"));
builder.Services.AddSingleton(new KafkaConsumer("payments"));
builder.Services.AddSingleton(new KafkaConsumer("notifications"));

// All consumers must be ready
builder.Services.AddIgnitionForAll<KafkaConsumer>(
    c => c.ReadyTask,
    groupName: "kafka-consumers");
```

## Custom Signal Factories

Create signals from arbitrary task factories for maximum flexibility.

### AddIgnitionFromFactory

```csharp
builder.Services.AddIgnitionFromFactory(
    taskFactory: sp =>
    {
        var db = sp.GetRequiredService<DatabaseContext>();
        var cache = sp.GetRequiredService<IDistributedCache>();

        return Task.WhenAll(
            db.Database.CanConnectAsync(),
            cache.GetAsync("health-check"));
    },
    name: "combined-data-layer",
    timeout: TimeSpan.FromSeconds(15));
```

### Use Cases

- Combining multiple independent tasks
- Complex initialization logic
- Service provider-based resolution
- Dynamic task composition

### Example: Multi-Step Initialization

```csharp
builder.Services.AddIgnitionFromFactory(
    taskFactory: async sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        var config = sp.GetRequiredService<IConfiguration>();

        // Step 1: Load external configuration
        logger.LogInformation("Loading external config...");
        var externalConfig = await LoadExternalConfigAsync(config["ConfigUrl"]);

        // Step 2: Apply configuration
        logger.LogInformation("Applying configuration...");
        config["Runtime:Setting1"] = externalConfig.Setting1;

        // Step 3: Validate
        logger.LogInformation("Validating configuration...");
        ValidateConfig(config);

        logger.LogInformation("Multi-step initialization complete");
    },
    name: "multi-step-init",
    timeout: TimeSpan.FromSeconds(30));
```

## TaskCompletionSource Integration Patterns

Use `TaskCompletionSource` for signals that complete based on events or conditions.

### Extension Methods

Veggerby.Ignition provides convenience extensions:

```csharp
private readonly TaskCompletionSource _readyTcs = new();

// Signal success
_readyTcs.Ignited();

// Signal failure
_readyTcs.IgnitionFailed(new InvalidOperationException("Startup failed"));
```

### Pattern: Event-Based Readiness

```csharp
public class WebSocketService
{
    private readonly TaskCompletionSource _connectedTcs = new();

    public Task ConnectedTask => _connectedTcs.Task;

    public async Task StartAsync(CancellationToken ct)
    {
        var ws = new WebSocket();

        ws.OnConnected += (sender, args) =>
        {
            _connectedTcs.Ignited();
        };

        ws.OnError += (sender, args) =>
        {
            _connectedTcs.IgnitionFailed(new InvalidOperationException("Connection failed"));
        };

        await ws.ConnectAsync(ct);
    }
}

// Registration
builder.Services.AddSingleton<WebSocketService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebSocketService>());
builder.Services.AddIgnitionFor<WebSocketService>(
    s => s.ConnectedTask,
    name: "websocket-connected");
```

### Pattern: Polling for Readiness

```csharp
public class DatabaseMigrationService
{
    private readonly TaskCompletionSource _migratedTcs = new();

    public Task MigratedTask => _migratedTcs.Task;

    public async Task StartAsync(CancellationToken ct)
    {
        // Start background migration
        _ = Task.Run(async () =>
        {
            try
            {
                await RunMigrationsAsync(ct);
                _migratedTcs.Ignited();
            }
            catch (Exception ex)
            {
                _migratedTcs.IgnitionFailed(ex);
            }
        }, ct);
    }

    private async Task RunMigrationsAsync(CancellationToken ct)
    {
        // Migration logic
        await Task.Delay(5000, ct);
    }
}
```

### Pattern: Condition-Based Readiness

```csharp
public class HealthMonitorService : BackgroundService
{
    private readonly TaskCompletionSource _healthyTcs = new();
    private readonly IHttpClientFactory _httpClientFactory;

    public Task HealthyTask => _healthyTcs.Task;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        // Poll until healthy
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await client.GetAsync("http://dependency/health", ct);

                if (response.IsSuccessStatusCode)
                {
                    _healthyTcs.Ignited();
                    break;
                }
            }
            catch
            {
                // Continue polling
            }

            await Task.Delay(1000, ct);
        }

        // Continue monitoring
        await MonitorAsync(ct);
    }

    private async Task MonitorAsync(CancellationToken ct) { /* ... */ }
}
```

## Background Service Coordination

Coordinate startup with `BackgroundService` instances.

### Pattern 1: BackgroundService with ReadyTask

```csharp
public class DataProcessorService : BackgroundService
{
    private readonly TaskCompletionSource _readyTcs = new();

    public Task ReadyTask => _readyTcs.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialization
        await InitializeAsync(stoppingToken);

        // Signal readiness
        _readyTcs.Ignited();

        // Main loop
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDataAsync(stoppingToken);
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task InitializeAsync(CancellationToken ct) { /* ... */ }
    private async Task ProcessDataAsync(CancellationToken ct) { /* ... */ }
}

// Registration
builder.Services.AddSingleton<DataProcessorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DataProcessorService>());
builder.Services.AddIgnitionFor<DataProcessorService>(
    s => s.ReadyTask,
    name: "data-processor-ready");
```

### Pattern 2: Multiple BackgroundServices

```csharp
// Service 1
public class OrderProcessorService : BackgroundService
{
    private readonly TaskCompletionSource _readyTcs = new();
    public Task ReadyTask => _readyTcs.Task;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await InitializeOrderProcessingAsync(ct);
        _readyTcs.Ignited();
        await ProcessOrdersAsync(ct);
    }
}

// Service 2
public class NotificationService : BackgroundService
{
    private readonly TaskCompletionSource _readyTcs = new();
    public Task ReadyTask => _readyTcs.Task;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await InitializeNotificationsAsync(ct);
        _readyTcs.Ignited();
        await SendNotificationsAsync(ct);
    }
}

// Registration
builder.Services.AddSingleton<OrderProcessorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OrderProcessorService>());

builder.Services.AddSingleton<NotificationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<NotificationService>());

// Wait for both
builder.Services.AddIgnitionFor<OrderProcessorService>(s => s.ReadyTask, "order-processor");
builder.Services.AddIgnitionFor<NotificationService>(s => s.ReadyTask, "notifications");
```

## Conditional Signal Execution

Register signals based on configuration or environment.

### Environment-Based Registration

```csharp
if (builder.Environment.IsProduction())
{
    builder.Services.AddIgnitionSignal<ProductionCacheWarmupSignal>();
    builder.Services.AddIgnitionSignal<PerformanceMonitoringSignal>();
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddIgnitionSignal<DevSeedDataSignal>();
}
```

### Configuration-Based Registration

```csharp
var enableCaching = builder.Configuration.GetValue<bool>("Features:Caching");
if (enableCaching)
{
    builder.Services.AddIgnitionSignal<CacheWarmupSignal>();
}

var enableReplication = builder.Configuration.GetValue<bool>("Database:Replication");
if (enableReplication)
{
    builder.Services.AddIgnitionSignal<ReplicaDatabaseSignal>();
}
```

### Feature Flag-Based Registration

```csharp
var featureManager = builder.Services.BuildServiceProvider().GetRequiredService<IFeatureManager>();

if (await featureManager.IsEnabledAsync("RecommendationEngine"))
{
    builder.Services.AddIgnitionSignal<RecommendationEngineSignal>();
}
```

## Dynamic Signal Registration

Register signals at runtime based on discovered services or configuration.

### Discovery-Based Registration

```csharp
// Discover all queue names from configuration
var queueNames = builder.Configuration.GetSection("Queues").GetChildren()
    .Select(x => x.Value)
    .ToList();

foreach (var queueName in queueNames)
{
    builder.Services.AddIgnitionFromTask(
        $"queue:{queueName}",
        ct => InitializeQueueAsync(queueName, ct),
        timeout: TimeSpan.FromSeconds(10));
}
```

### Plugin-Based Registration

```csharp
// Discover plugin assemblies
var pluginAssemblies = Directory.GetFiles("plugins", "*.dll")
    .Select(Assembly.LoadFrom)
    .ToList();

foreach (var assembly in pluginAssemblies)
{
    var signalTypes = assembly.GetTypes()
        .Where(t => typeof(IIgnitionSignal).IsAssignableFrom(t) && !t.IsAbstract);

    foreach (var signalType in signalTypes)
    {
        builder.Services.AddIgnitionSignal(signalType);
    }
}
```

## Testing Ignition Signals

### Unit Testing Individual Signals

```csharp
public class DatabaseSignalTests
{
    [Fact]
    public async Task WaitAsync_Succeeds_When_Database_Available()
    {
        // Arrange
        var mockConnection = Substitute.For<IDbConnection>();
        mockConnection.OpenAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var signal = new DatabaseSignal(mockConnection);

        // Act
        await signal.WaitAsync();

        // Assert
        await mockConnection.Received(1).OpenAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_Throws_When_Database_Unavailable()
    {
        // Arrange
        var mockConnection = Substitute.For<IDbConnection>();
        mockConnection.OpenAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var signal = new DatabaseSignal(mockConnection);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_Respects_CancellationToken()
    {
        // Arrange
        var mockConnection = Substitute.For<IDbConnection>();
        mockConnection.OpenAsync(Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(10000, ct); // Long operation
            });

        var signal = new DatabaseSignal(mockConnection);
        var cts = new CancellationTokenSource();
        cts.CancelAfter(100);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            signal.WaitAsync(cts.Token));
    }
}
```

### Integration Testing with Coordinator

```csharp
public class IgnitionIntegrationTests
{
    [Fact]
    public async Task All_Signals_Complete_Successfully()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition(options =>
        {
            options.GlobalTimeout = TimeSpan.FromSeconds(10);
            options.Policy = IgnitionPolicy.FailFast;
        });

        services.AddIgnitionFromTask("signal1", async ct => await Task.Delay(100, ct));
        services.AddIgnitionFromTask("signal2", async ct => await Task.Delay(200, ct));

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

    [Fact]
    public async Task FailFast_Throws_On_Signal_Failure()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIgnition(options =>
        {
            options.Policy = IgnitionPolicy.FailFast;
        });

        services.AddIgnitionFromTask("good", ct => Task.CompletedTask);
        services.AddIgnitionFromTask("bad", ct =>
            Task.FromException(new InvalidOperationException("Failure")));

        var sp = services.BuildServiceProvider();
        var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AggregateException>(() =>
            coordinator.WaitAllAsync());

        ex.InnerExceptions.Should().ContainSingle()
            .Which.Should().BeOfType<InvalidOperationException>();
    }
}
```

### Mocking Strategies

#### Mock Signal Implementation

```csharp
public class MockSignal : IIgnitionSignal
{
    private readonly string _name;
    private readonly Func<CancellationToken, Task> _implementation;

    public MockSignal(string name, Func<CancellationToken, Task> implementation)
    {
        _name = name;
        _implementation = implementation;
    }

    public string Name => _name;
    public TimeSpan? Timeout => null;
    public Task WaitAsync(CancellationToken ct) => _implementation(ct);
}

// Usage in tests
var mockSignal = new MockSignal("test", async ct =>
{
    await Task.Delay(100, ct);
    // Custom test logic
});
```

#### Using Test Doubles

```csharp
public class TestDatabaseSignal : IIgnitionSignal
{
    public string Name => "database";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public bool WasCalled { get; private set; }

    public Task WaitAsync(CancellationToken ct)
    {
        WasCalled = true;
        return Task.CompletedTask; // Always succeeds in tests
    }
}
```

### Testing Timeout Behavior

```csharp
[Fact]
public async Task Signal_Times_Out_When_Exceeding_Timeout()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddIgnition(options =>
    {
        options.GlobalTimeout = TimeSpan.FromSeconds(10);
        options.CancelIndividualOnTimeout = true;
    });

    services.AddIgnitionFromTask(
        "slow-signal",
        async ct => await Task.Delay(10000, ct), // 10 seconds
        timeout: TimeSpan.FromSeconds(1)); // 1 second timeout

    var sp = services.BuildServiceProvider();
    var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

    // Act
    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();

    // Assert
    var slowSignal = result.Results.First(r => r.Name == "slow-signal");
    slowSignal.Status.Should().Be(IgnitionSignalStatus.TimedOut);
    result.TimedOut.Should().BeTrue();
}
```

### Testing DAG Dependencies

```csharp
[Fact]
public async Task DAG_Skips_Dependents_When_Prerequisite_Fails()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddIgnition(options =>
    {
        options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
        options.Policy = IgnitionPolicy.BestEffort;
    });

    services.AddIgnitionFromTask(
        "database",
        ct => Task.FromException(new InvalidOperationException("DB failed")));

    services.AddIgnitionFromTask("cache", ct => Task.CompletedTask);

    services.AddIgnitionGraph((builder, sp) =>
    {
        var signals = sp.GetServices<IIgnitionSignal>();
        var db = signals.First(s => s.Name == "database");
        var cache = signals.First(s => s.Name == "cache");

        builder.AddSignals(new[] { db, cache });
        builder.DependsOn(cache, db); // cache depends on database
    });

    var sp = services.BuildServiceProvider();
    var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

    // Act
    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();

    // Assert
    var dbResult = result.Results.First(r => r.Name == "database");
    var cacheResult = result.Results.First(r => r.Name == "cache");

    dbResult.Status.Should().Be(IgnitionSignalStatus.Failed);
    cacheResult.Status.Should().Be(IgnitionSignalStatus.Skipped);
    cacheResult.FailedDependencies.Should().Contain("database");
}
```

## Integration Testing Patterns

### WebApplicationFactory Pattern

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real signals with test doubles
            services.RemoveAll<IIgnitionSignal>();

            services.AddIgnitionFromTask("test-signal", ct => Task.CompletedTask);
        });
    }
}

public class IntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public IntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Application_Starts_With_Ignition()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
```

### Testcontainers Pattern

```csharp
public class DatabaseIgnitionTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task DatabaseSignal_Connects_To_Real_Database()
    {
        // Arrange
        var connectionString = _postgres.GetConnectionString();
        var connection = new NpgsqlConnection(connectionString);

        var signal = new DatabaseSignal(connection);

        // Act
        await signal.WaitAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Open);
    }
}
```

## Performance Profiling Patterns

### Measuring Signal Duration

```csharp
var result = await coordinator.GetResultAsync();

var signalTimings = result.Results
    .OrderByDescending(r => r.Duration)
    .Select(r => new
    {
        r.Name,
        DurationMs = r.Duration.TotalMilliseconds,
        PercentOfTotal = (r.Duration.TotalSeconds / result.TotalDuration.TotalSeconds) * 100
    });

foreach (var timing in signalTimings)
{
    Console.WriteLine($"{timing.Name}: {timing.DurationMs:F0}ms ({timing.PercentOfTotal:F1}%)");
}
```

### Benchmarking with BenchmarkDotNet

```csharp
[MemoryDiagnoser]
public class IgnitionBenchmarks
{
    private IServiceProvider _serviceProvider;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition();

        services.AddIgnitionFromTask("signal1", ct => Task.Delay(10, ct));
        services.AddIgnitionFromTask("signal2", ct => Task.Delay(20, ct));

        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark]
    public async Task WaitAllAsync()
    {
        var coordinator = _serviceProvider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();
    }
}
```

## Related Topics

- [Getting Started](getting-started.md) - Basic signal patterns
- [Bundles](bundles.md) - Packaging reusable patterns
- [Dependency-Aware Execution](dependency-aware-execution.md) - DAG patterns
- [Observability](observability.md) - Monitoring patterns
- [Performance Guide](performance.md) - Optimization patterns
