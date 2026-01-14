# Testcontainers Multi-Service Sample

This sample demonstrates a comprehensive **multi-stage, multi-service** startup orchestration using **Testcontainers** to spin up real infrastructure.

## What This Sample Demonstrates

### Infrastructure (via Testcontainers)
- **PostgreSQL** (postgres:17-alpine)
- **Redis** (redis:7-alpine)
- **RabbitMQ** (rabbitmq:4.0-alpine)
- **MongoDB** (mongo:8)
- **SQL Server** (mcr.microsoft.com/mssql/server:2022-latest)

### Ignition Features
- **Multi-stage execution** (4 stages with sequential stage progression)
- **Parallel execution within stages** (databases start in parallel in Stage 1)
- **Modern DI patterns**:
  - PostgreSQL: `NpgsqlDataSource`
  - SQL Server: `Func<SqlConnection>`
  - Redis: `IConnectionMultiplexer`
  - RabbitMQ: `IConnectionFactory`
  - MongoDB: Connection string
- **Full observability**: Tracing, logging, slow signal detection
- **Health check integration**
- **Fail-fast policy** with timeout management

## Architecture

### Stage 1: Databases (Parallel)
All database services start concurrently:
- PostgreSQL with validation query
- SQL Server with validation query
- MongoDB with database verification

### Stage 2: Caches
After databases are ready:
- Redis with ping verification

### Stage 3: Message Queues
After caches are ready:
- RabbitMQ with queue verification

### Stage 4: Application Services (Parallel)
Final application initialization:
- Application component initialization
- Cache warmup
- Background services startup

## Prerequisites

- **.NET 10 SDK** or later
- **Docker** running and accessible
- **~2GB free memory** for containers

## Running the Sample

```bash
# Ensure Docker is running
docker ps

# Run the sample
dotnet run --project samples/TestcontainersDemo/TestcontainersDemo.csproj

# In dev containers, set environment variable
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal
dotnet run --project samples/TestcontainersDemo/TestcontainersDemo.csproj
```

## Expected Output

```
╔════════════════════════════════════════════════════════════════════════════╗
║  Veggerby.Ignition - Testcontainers Multi-Service Demo                    ║
║                                                                            ║
║  Demonstrates:                                                             ║
║    • Multi-stage execution (4 stages)                                      ║
║    • PostgreSQL, Redis, RabbitMQ, MongoDB, SQL Server                      ║
║    • Sequential & parallel execution                                       ║
║    • Full observability                                                    ║
╚════════════════════════════════════════════════════════════════════════════╝

🐳 Starting Testcontainers infrastructure...
  🐘 Starting PostgreSQL...
  🔴 Starting Redis...
  🐰 Starting RabbitMQ...
  🍃 Starting MongoDB...
  🗄️  Starting SQL Server...
  ✅ PostgreSQL ready at localhost:xxxxx
  ✅ Redis ready at localhost:xxxxx
  ✅ RabbitMQ ready at localhost:xxxxx
  ✅ MongoDB ready at localhost:xxxxx
  ✅ SQL Server ready at localhost:xxxxx
✅ All containers started successfully!

📋 Registering Stage 1: Databases (PostgreSQL, SQL Server, MongoDB)
📋 Registering Stage 2: Caches (Redis)
📋 Registering Stage 3: Message Queues (RabbitMQ)
📋 Registering Stage 4: Application Services

═══════════════════════════════════════════════════════════════════════════
  STARTING IGNITION SEQUENCE
═══════════════════════════════════════════════════════════════════════════

[Stage 1 executes - databases verify in parallel]
[Stage 2 executes - Redis verifies]
[Stage 3 executes - RabbitMQ verifies]
[Stage 4 executes - Application services initialize in parallel]

═══════════════════════════════════════════════════════════════════════════
  IGNITION RESULTS
═══════════════════════════════════════════════════════════════════════════

  Total Duration:      2500ms
  Timed Out:           NO ✅

  Stage 1: Databases:
    ✅ postgres-readiness              250ms
    ✅ sqlserver-readiness             280ms
    ✅ mongodb-readiness               220ms

  Stage 2: Caches:
    ✅ redis-readiness                 180ms

  Stage 3: Message Queues:
    ✅ rabbitmq-readiness              300ms

  Stage 4: Application Services:
    ✅ app-initialization              500ms
    ✅ cache-warmup                    800ms
    ✅ background-services             300ms

  Summary:
    Total Signals:       11
    ✅ Succeeded:        11
    ❌ Failed:           0
    ⏱️  Timed Out:        0

═══════════════════════════════════════════════════════════════════════════

🧹 Cleaning up Testcontainers...
  🐘 Stopping PostgreSQL...
  🔴 Stopping Redis...
  🐰 Stopping RabbitMQ...
  🍃 Stopping MongoDB...
  🗄️  Stopping SQL Server...
✅ Cleanup complete!
```

## Key Learnings

### 1. Testcontainers Integration
Containers are started **before** Ignition registration, ensuring connection strings are available for DI configuration.

### 2. Modern DI Patterns
Each infrastructure type uses its recommended modern pattern:
- **PostgreSQL**: `NpgsqlDataSource` (recommended for Npgsql 7.0+)
- **SQL Server**: `Func<SqlConnection>` (factory pattern)
- **Redis**: `IConnectionMultiplexer` (standard StackExchange.Redis)
- **RabbitMQ**: `IConnectionFactory` (standard RabbitMQ.Client)

### 3. Stage Progression
Stages execute **sequentially** with **parallel execution within each stage**:
1. All databases verify concurrently
2. Caches verify after databases ready
3. Message queues verify after caches ready
4. Application services initialize after queues ready

### 4. Cleanup
`finally` block ensures containers are cleaned up even if ignition fails.

## Customization

### Add More Services
```csharp
// Add Memcached in Stage 2
builder.Services.AddMemcachedReadiness(options => ...);
builder.Services.AddIgnitionFromTaskWithStage("memcached-stage", _ => Task.CompletedTask, stage: 2);
```

### Change Execution Mode
```csharp
// Switch to dependency-aware execution
options.ExecutionMode = IgnitionExecutionMode.DependencyAware;

// Build dependency graph
builder.Services.AddIgnitionGraph((graph, sp) =>
{
    graph.AddSignals(sp.GetServices<IIgnitionSignal>());
    graph.AddDependency("redis-readiness", "postgres-readiness");
});
```

### Adjust Timeouts
```csharp
// Longer global timeout for slow environments
options.GlobalTimeout = TimeSpan.FromSeconds(180);

// Per-signal timeout overrides
builder.Services.AddPostgresReadiness(options =>
{
    options.Timeout = TimeSpan.FromSeconds(60);
});
```

## Performance Notes

- **Container startup**: ~5-15 seconds (parallel)
- **Signal verification**: ~1-3 seconds (depends on infrastructure)
- **Total execution**: Typically 10-20 seconds end-to-end
- **Memory usage**: ~2GB for all containers

## Troubleshooting

### Docker not running
```
Error: Cannot connect to Docker daemon
```
→ Start Docker Desktop or Docker daemon

### Containers fail to start
```
Error: Container failed to become ready
```
→ Check Docker resources (memory, CPU limits)
→ Increase timeout values

### Dev container networking
```
Error: Connection refused
```
→ Set `export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`

## See Also

- [Staged Execution Documentation](../../docs/features.md#staged-execution)
- [Dependency-Aware Execution](../../docs/dependency-aware-execution.md)
- [Integration Recipes](../../docs/integration-recipes.md)
- [PostgreSQL Integration](../../src/Veggerby.Ignition.Postgres/README.md)
- [SQL Server Integration](../../src/Veggerby.Ignition.SqlServer/README.md)
- [Redis Integration](../../src/Veggerby.Ignition.Redis/README.md)
- [RabbitMQ Integration](../../src/Veggerby.Ignition.RabbitMq/README.md)
- [MongoDB Integration](../../src/Veggerby.Ignition.MongoDb/README.md)
