# Veggerby.Ignition.Elasticsearch

Elasticsearch readiness signals for [Veggerby.Ignition](../Veggerby.Ignition) - verify cluster health, indices, templates, and query execution during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Elasticsearch
```

## Quick Start

```csharp
using Veggerby.Ignition;
using Veggerby.Ignition.Elasticsearch;

var builder = Host.CreateApplicationBuilder(args);

// Add Elasticsearch readiness check (cluster health)
builder.Services.AddElasticsearchReadiness("http://localhost:9200");

// Add Ignition coordinator
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});

var host = builder.Build();

// Wait for Elasticsearch cluster to be ready
await host.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();

await host.RunAsync();
```

## Verification Strategies

### Cluster Health (Default)

Verifies cluster is reachable and reports health status (green/yellow/red):

```csharp
services.AddElasticsearchReadiness("http://localhost:9200", options =>
{
    options.VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth;
});
```

### Index Existence

Checks that specific indices exist in the cluster:

```csharp
services.AddElasticsearchReadiness("http://localhost:9200", options =>
{
    options.VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists;
    options.VerifyIndices.Add("logs-2024");
    options.VerifyIndices.Add("metrics-2024");
    options.FailOnMissingIndices = true; // Default behavior
});
```

### Template Validation

Validates that an index template is configured:

```csharp
services.AddElasticsearchReadiness("http://localhost:9200", options =>
{
    options.VerificationStrategy = ElasticsearchVerificationStrategy.TemplateValidation;
    options.VerifyTemplate = "logs-template";
});
```

### Query Test

Executes a test query to verify read operations work:

```csharp
services.AddElasticsearchReadiness("http://localhost:9200", options =>
{
    options.VerificationStrategy = ElasticsearchVerificationStrategy.QueryTest;
    options.TestQueryIndex = "logs-2024";
});
```

## Advanced Configuration

### Custom Client Settings

```csharp
var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
    .Authentication(new BasicAuthentication("user", "password"))
    .ServerCertificateValidationCallback((o, cert, chain, errors) => true);

services.AddElasticsearchReadiness(settings, options =>
{
    options.VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth;
    options.Timeout = TimeSpan.FromSeconds(15);
});
```

### Retry Configuration

```csharp
services.AddElasticsearchReadiness("http://localhost:9200", options =>
{
    options.MaxRetries = 5;
    options.RetryDelay = TimeSpan.FromMilliseconds(500);
});
```

### Staged Execution

Useful with Testcontainers or multi-phase startup:

```csharp
// Stage 0: Start container
var infrastructure = new InfrastructureManager();
services.AddSingleton(infrastructure);
services.AddIgnitionFromTaskWithStage(
    "elasticsearch-container",
    async ct => await infrastructure.StartElasticsearchAsync(),
    stage: 0);

// Stage 2: Verify Elasticsearch readiness
services.AddElasticsearchReadiness(
    sp =>
    {
        var infra = sp.GetRequiredService<InfrastructureManager>();
        return new ElasticsearchClientSettings(new Uri(infra.ElasticsearchUrl));
    },
    options =>
    {
        options.Stage = 2;
        options.VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists;
        options.VerifyIndices.Add("application-logs");
    });
```

## Configuration Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `VerificationStrategy` | `ElasticsearchVerificationStrategy` | `ClusterHealth` | Strategy to use for readiness check |
| `Timeout` | `TimeSpan?` | `10s` | Per-signal timeout (null uses global timeout) |
| `MaxRetries` | `int` | `3` | Maximum retry attempts for transient failures |
| `RetryDelay` | `TimeSpan` | `200ms` | Initial delay between retries (exponential backoff) |
| `Stage` | `int?` | `null` | Optional stage number for staged execution |
| `VerifyIndices` | `List<string>` | Empty | Index names to verify (IndexExists strategy) |
| `FailOnMissingIndices` | `bool` | `true` | Fail if any indices are missing (IndexExists strategy) |
| `VerifyTemplate` | `string?` | `null` | Template name to verify (TemplateValidation strategy) |
| `TestQueryIndex` | `string?` | `null` | Index name for test query (QueryTest strategy) |

## Testing with Testcontainers

```csharp
using Testcontainers.Elasticsearch;

public class ElasticsearchIntegrationTests : IAsyncLifetime
{
    private ElasticsearchContainer? _container;

    public async Task InitializeAsync()
    {
        _container = new ElasticsearchBuilder()
            .WithImage("elasticsearch:8.17.0")
            .Build();

        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    [Fact]
    public async Task ClusterHealth_Succeeds()
    {
        var services = new ServiceCollection();
        services.AddElasticsearchReadiness(_container!.GetConnectionString());
        services.AddIgnition();

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        await coordinator.WaitAllAsync();
    }
}
```

## Health Check Integration

Elasticsearch readiness status is automatically available via Ignition's health check:

```csharp
builder.Services.AddHealthChecks()
    .AddIgnitionHealthCheck(); // Includes Elasticsearch readiness

app.MapHealthChecks("/health");
```

## Activity Tracing

When Activity tracing is enabled, Elasticsearch operations are traced with tags:

- `elasticsearch.verification_strategy`
- `elasticsearch.cluster.status`
- `elasticsearch.cluster.number_of_nodes`
- `elasticsearch.cluster.active_shards`

## Best Practices

1. **Cluster Health for Most Scenarios**: The default `ClusterHealth` strategy is sufficient for most applications
2. **Index Verification for Critical Indices**: Use `IndexExists` when your application depends on specific indices
3. **Template Validation for Index Lifecycle**: Verify templates when using index lifecycle management (ILM)
4. **Query Test for Production Verification**: Use `QueryTest` in production readiness probes to verify end-to-end functionality
5. **Retry Configuration**: Increase `MaxRetries` and `RetryDelay` for Testcontainers scenarios where cluster initialization may be slow

## License

MIT - See LICENSE file for details
