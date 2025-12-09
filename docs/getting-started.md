# Getting Started with Veggerby.Ignition

This guide will help you get up and running with Veggerby.Ignition, a lightweight startup readiness
coordination library for .NET applications.

> 💡 **Looking for ready-to-use patterns?** Check out the **[Integration Recipes](integration-recipes.md)** for copy-paste-ready code for ASP.NET Core Web APIs, Generic Host Workers, and Console applications.

## What is Veggerby.Ignition?

Veggerby.Ignition coordinates asynchronous startup tasks (called "signals") in your application. It ensures
all critical components are ready before your application begins serving requests, with support for:

- Timeouts and deadline management
- Failure handling policies
- Dependency-aware execution
- Health check integration
- Distributed tracing
- Rich diagnostics

## Installation

### NuGet Package

Install the package via NuGet Package Manager:

```bash
dotnet add package Veggerby.Ignition
```

Or add it to your `.csproj` file:

```xml
<PackageReference Include="Veggerby.Ignition" Version="1.0.0" />
```

## Your First Ignition Signal

### Step 1: Create a Signal

Create a class implementing `IIgnitionSignal`:

```csharp
using Veggerby.Ignition;

public class DatabaseConnectionSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseConnectionSignal> _logger;
    private readonly DatabaseContext _dbContext;

    public DatabaseConnectionSignal(
        ILogger<DatabaseConnectionSignal> logger,
        DatabaseContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public string Name => "database-connection";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to database...");

        // Ensure database is accessible
        await _dbContext.Database.CanConnectAsync(cancellationToken);

        _logger.LogInformation("Database connection established");
    }
}
```

### Step 2: Register the Signal

In your `Program.cs` or startup configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Ignition with configuration
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.FailFast;
    options.EnableTracing = true;
});

// Register your signal
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();

var app = builder.Build();
```

### Step 3: Wait for Readiness

Before starting your application, wait for all signals to complete:

```csharp
// Get the coordinator
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();

// Wait for all signals to complete
await coordinator.WaitAllAsync();

// Now your application is ready
await app.RunAsync();
```

## Common Patterns

### Database Connection

Ensure database connectivity before accepting requests:

```csharp
public class DatabaseReadySignal : IIgnitionSignal
{
    private readonly IDbConnection _connection;

    public DatabaseReadySignal(IDbConnection connection)
    {
        _connection = connection;
    }

    public string Name => "database-ready";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(15);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _connection.OpenAsync(cancellationToken);

        // Optionally verify schema or run health check query
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        await cmd.ExecuteScalarAsync(cancellationToken);
    }
}
```

### HTTP Endpoint Warmup

Verify external dependencies are available:

```csharp
public class ExternalApiSignal : IIgnitionSignal
{
    private readonly HttpClient _httpClient;
    private readonly string _healthEndpoint;

    public ExternalApiSignal(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient();
        _healthEndpoint = config["ExternalApi:HealthEndpoint"];
    }

    public string Name => "external-api-health";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(_healthEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
```

Or use the built-in `HttpDependencyBundle`:

```csharp
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle("https://api.example.com/health", TimeSpan.FromSeconds(10)));
```

### Cache Warming

Pre-populate caches before serving requests:

```csharp
public class CacheWarmingSignal : IIgnitionSignal
{
    private readonly IDistributedCache _cache;
    private readonly IDataService _dataService;

    public CacheWarmingSignal(IDistributedCache cache, IDataService dataService)
    {
        _cache = cache;
        _dataService = dataService;
    }

    public string Name => "cache-warmup";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(20);

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        var criticalData = await _dataService.GetCriticalDataAsync(cancellationToken);

        foreach (var item in criticalData)
        {
            await _cache.SetStringAsync(
                item.Key,
                JsonSerializer.Serialize(item.Value),
                cancellationToken);
        }
    }
}
```

### Background Service Ready

Coordinate with `BackgroundService` instances:

```csharp
public class MessageProcessorService : BackgroundService
{
    private readonly TaskCompletionSource _readyTcs = new();

    public Task ReadyTask => _readyTcs.Task;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Perform initialization
        await InitializeQueueConnectionAsync(stoppingToken);

        // Signal readiness
        _readyTcs.Ignited();

        // Continue with background work
        await ProcessMessagesAsync(stoppingToken);
    }

    private async Task InitializeQueueConnectionAsync(CancellationToken ct)
    {
        // Connection logic
    }

    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        // Processing loop
    }
}

// Registration
builder.Services.AddSingleton<MessageProcessorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageProcessorService>());
builder.Services.AddIgnitionFor<MessageProcessorService>(
    s => s.ReadyTask,
    name: "message-processor-ready");
```

## ASP.NET Core Integration

### Complete Example

Here's a complete ASP.NET Core application with Ignition:

```csharp
using Veggerby.Ignition;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient();

// Configure Ignition
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.Policy = IgnitionPolicy.BestEffort;
    options.ExecutionMode = IgnitionExecutionMode.Parallel;
    options.EnableTracing = true;
    options.SlowHandleLogCount = 3; // Log 3 slowest signals
});

// Register signals
builder.Services.AddIgnitionSignal<DatabaseConnectionSignal>();
builder.Services.AddIgnitionSignal<CacheWarmingSignal>();
builder.Services.AddIgnitionBundle(
    new HttpDependencyBundle("https://api.partner.com/health", TimeSpan.FromSeconds(5)));

// Health checks (ignition-readiness automatically registered)
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map health checks endpoint
app.MapHealthChecks("/health");

// Wait for ignition before accepting requests
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

// Inspect results
var result = await coordinator.GetResultAsync();
if (result.TimedOut)
{
    app.Logger.LogWarning("Startup completed with timeouts");
}

foreach (var r in result.Results.Where(x => x.Status != IgnitionSignalStatus.Succeeded))
{
    app.Logger.LogWarning("Signal {Name} finished with status {Status}", r.Name, r.Status);
}

app.Run();
```

### Health Check Integration

Ignition automatically registers a health check named `ignition-readiness`:

```csharp
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map health endpoint
app.MapHealthChecks("/health");
```

The health check reflects the cached ignition result:

- **Healthy**: All signals succeeded
- **Degraded**: Soft global timeout (no failures)
- **Unhealthy**: Signal failures or hard timeout

### Startup Error Handling

Handle startup failures gracefully:

```csharp
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();

try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex) when (options.Policy == IgnitionPolicy.FailFast)
{
    app.Logger.LogCritical(ex, "Critical startup failure");

    foreach (var inner in ex.InnerExceptions)
    {
        app.Logger.LogError(inner, "Signal failed: {Message}", inner.Message);
    }

    // Optionally exit with error code
    Environment.Exit(1);
}

var result = await coordinator.GetResultAsync();
if (!result.TimedOut && result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded))
{
    app.Logger.LogInformation("All startup signals completed successfully");
}
```

## Task-Based Registration

When you don't need a full `IIgnitionSignal` implementation, use convenience methods:

### From Existing Task

```csharp
var warmupTask = Task.Run(async () =>
{
    await Task.Delay(1000);
    // Warmup logic
});

builder.Services.AddIgnitionFromTask("warmup", warmupTask, timeout: TimeSpan.FromSeconds(5));
```

### From Cancellable Task Factory

```csharp
builder.Services.AddIgnitionFromTask(
    name: "index-build",
    readyTaskFactory: async ct =>
    {
        var indexBuilder = new SearchIndexBuilder();
        await indexBuilder.BuildAsync(ct);
    },
    timeout: TimeSpan.FromSeconds(30));
```

### From Service Instance

```csharp
// Register service
builder.Services.AddSingleton<CachePrimer>();

// Add ignition signal that waits for service's ReadyTask property
builder.Services.AddIgnitionFor<CachePrimer>(
    c => c.ReadyTask,
    name: "cache-primer");
```

### Composite from Multiple Services

```csharp
// Wait for all ShardIndexer instances to complete
builder.Services.AddIgnitionForAll<ShardIndexer>(
    i => i.ReadyTask,
    groupName: "shard-indexers[*]");
```

## Configuration Options

### Execution Modes

```csharp
builder.Services.AddIgnition(options =>
{
    // Parallel: All signals run concurrently (default)
    options.ExecutionMode = IgnitionExecutionMode.Parallel;

    // Sequential: Signals run one at a time in registration order
    options.ExecutionMode = IgnitionExecutionMode.Sequential;

    // DependencyAware: Respects signal dependencies (DAG)
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
});
```

### Policies

```csharp
builder.Services.AddIgnition(options =>
{
    // BestEffort: Log failures but continue (default)
    options.Policy = IgnitionPolicy.BestEffort;

    // FailFast: Throw on first failure
    options.Policy = IgnitionPolicy.FailFast;

    // ContinueOnTimeout: Proceed when global timeout elapses
    options.Policy = IgnitionPolicy.ContinueOnTimeout;
});
```

### Timeouts

```csharp
builder.Services.AddIgnition(options =>
{
    // Global timeout (soft by default)
    options.GlobalTimeout = TimeSpan.FromSeconds(30);

    // Make global timeout hard (cancel outstanding signals)
    options.CancelOnGlobalTimeout = true;

    // Cancel individual signals on their timeout
    options.CancelIndividualOnTimeout = true;
});
```

### Concurrency Limiting

```csharp
builder.Services.AddIgnition(options =>
{
    // Limit parallel signal execution
    options.MaxDegreeOfParallelism = 4;
});
```

### Diagnostics

```csharp
builder.Services.AddIgnition(options =>
{
    // Enable Activity tracing for OpenTelemetry
    options.EnableTracing = true;

    // Log N slowest signals
    options.SlowHandleLogCount = 5;
});
```

## Basic Troubleshooting

### Signal Never Completes

**Symptom**: Application hangs during startup

**Common Causes**:

1. **Blocking I/O**: Ensure all operations are truly async
2. **Deadlock**: Avoid `Task.Result` or `.Wait()` calls
3. **Infinite Loop**: Check for logic errors in signal implementation
4. **Timeout Too Short**: Increase signal or global timeout

**Solution**:

```csharp
// Enable detailed logging
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Use slow signal logging
builder.Services.AddIgnition(options =>
{
    options.SlowHandleLogCount = 10;
});

// Inspect results for timing information
var result = await coordinator.GetResultAsync();
foreach (var r in result.Results)
{
    Console.WriteLine($"{r.Name}: {r.Status} ({r.Duration.TotalSeconds:F2}s)");
}
```

### Signal Fails Immediately

**Symptom**: Signal completes with `Failed` status

**Common Causes**:

1. **Dependency not available**: Database, API, or service unreachable
2. **Configuration error**: Missing or invalid connection strings
3. **Permission issues**: Insufficient access rights

**Solution**:

```csharp
var result = await coordinator.GetResultAsync();
foreach (var r in result.Results.Where(x => x.Status == IgnitionSignalStatus.Failed))
{
    Console.WriteLine($"{r.Name} failed: {r.Exception?.Message}");
    Console.WriteLine(r.Exception?.StackTrace);
}
```

### Application Exits on Startup

**Symptom**: Application terminates during ignition

**Common Causes**:

1. **FailFast policy**: Any signal failure causes exception
2. **Unhandled exception**: Exception not caught in signal

**Solution**:

```csharp
// Use BestEffort during development
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort;
});

// Wrap WaitAllAsync in try-catch
try
{
    await coordinator.WaitAllAsync();
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Startup failed");
    // Handle or rethrow
}
```

### Health Check Reports Unhealthy

**Symptom**: `/health` endpoint returns unhealthy status

**Solution**:

```csharp
// Inspect ignition result
var result = await coordinator.GetResultAsync();

if (result.TimedOut)
{
    Console.WriteLine("Global timeout occurred");
}

foreach (var r in result.Results)
{
    if (r.Status != IgnitionSignalStatus.Succeeded)
    {
        Console.WriteLine($"{r.Name}: {r.Status}");
        if (r.Exception != null)
        {
            Console.WriteLine($"  Error: {r.Exception.Message}");
        }
    }
}
```

## Next Steps

Now that you have the basics, explore more advanced features:

- **[Dependency-Aware Execution](dependency-aware-execution.md)**: Coordinate signals with complex dependencies
- **[Timeout Management](timeout-management.md)**: Master the two-layer timeout system
- **[Bundles](bundles.md)**: Create reusable signal packages
- **[Policies](policies.md)**: Choose the right failure handling strategy
- **[Observability](observability.md)**: Set up monitoring and tracing

## Sample Projects

Check out the [sample projects](../samples/README.md) for complete working examples:

- [Simple](../samples/Simple/README.md) - Basic usage
- [Advanced](../samples/Advanced/README.md) - Complex scenarios
- [DependencyGraph](../samples/DependencyGraph/README.md) - DAG execution
- [Bundles](../samples/Bundles/README.md) - Bundle usage
- [WebApi](../samples/WebApi/README.md) - ASP.NET Core integration
