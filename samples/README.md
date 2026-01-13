# Veggerby.Ignition Samples

This directory contains sample applications demonstrating different usage patterns and integration scenarios for the Veggerby.Ignition library.

## Available Samples

### [Simple Console Application](./Simple/)

**Complexity**: Beginner
**Type**: Console Application
**Focus**: Basic usage patterns

Demonstrates:

- Basic signal registration and coordination
- Simple logging integration
- Default configuration usage
- Success/failure handling

Perfect for getting started with Ignition and understanding core concepts.

### [Testcontainers Multi-Service Demo](./TestcontainersDemo/)

**Complexity**: Advanced
**Type**: Console Application  
**Focus**: Real infrastructure with Testcontainers

Demonstrates:

- Multi-stage execution (4 stages: Databases → Caches → Message Queues → Application)
- Parallel container startup for faster initialization
- All major integration packages (PostgreSQL, Redis, RabbitMQ, MongoDB, SQL Server)
- Modern DI patterns (NpgsqlDataSource, Func<SqlConnection>, IConnectionMultiplexer, IConnectionFactory)
- Sequential stage progression with parallel execution within stages
- Full observability (tracing, slow signal detection, structured logging)
- Automatic container cleanup

Perfect for understanding production-ready multi-service orchestration with real infrastructure dependencies.

**Prerequisites**: Docker Desktop running

### [Bundles Console Application](./Bundles/)

**Complexity**: Intermediate
**Type**: Console Application
**Focus**: Composable bundle patterns

Demonstrates:

- Using built-in bundles (DatabaseTrioBundle)
- Creating custom reusable bundles
- Bundle configuration with timeout overrides
- Packaging related signals into modules
- Combining multiple bundles
- Dependency graph setup within bundles

Ideal for understanding how to create reusable signal packages for common patterns like Redis startup, message queue initialization, or database trios.

### [Advanced Console Application](./Advanced/)

**Complexity**: Intermediate
**Type**: Console Application
**Focus**: Advanced configuration and scenarios

Demonstrates:

- Multiple execution policies (FailFast, BestEffort, ContinueOnTimeout)
- Sequential vs Parallel execution modes
- Concurrency limiting with MaxDegreeOfParallelism
- Global and per-signal timeout handling
- Complex failure scenarios and error reporting

Ideal for understanding advanced configuration options and policy behaviors.

### [Timeout Strategies Console Application](./TimeoutStrategies/)

**Complexity**: Intermediate
**Type**: Console Application
**Focus**: Pluggable timeout strategies

Demonstrates:

- Creating custom `IIgnitionTimeoutStrategy` implementations
- Different timeout approaches (lenient, strict, adaptive, category-based)
- How the same signals produce different outcomes with different strategies
- Strategy registration via DI extension methods
- Comparing strategy effects side-by-side

Ideal for understanding how to customize timeout behavior for different environments or signal types.

### [Timeline Export Console Application](./TimelineExport/)

**Complexity**: Intermediate
**Type**: Console Application
**Focus**: Startup analysis and visualization

Demonstrates:

- Exporting ignition results to structured timeline format
- Gantt-like console visualization of signal execution
- JSON export for external analysis tools
- Concurrent vs sequential execution timing visualization
- Timeout scenarios in timeline view
- Summary statistics (slowest/fastest signals, max concurrency)

Ideal for debugging slow startup issues, profiling container warmup times, and detecting CI timing regressions.

### [Recording & Replay Console Application](./Replay/)

**Complexity**: Intermediate
**Type**: Console Application
**Focus**: Recording, replay, and what-if analysis

Demonstrates:

- Recording ignition runs with full timing and configuration capture
- Validating recordings for consistency and invariant violations
- Comparing recordings to detect performance regressions
- What-if simulations (timeout and failure scenarios)
- Identifying slow signals and critical path analysis
- Concurrent group identification and execution order analysis

Ideal for CI regression detection, prod vs dev comparison, failure analysis, capacity planning, and incident post-mortems.

### [Dependency Graph Console Application](./DependencyGraph/)

**Complexity**: Advanced
**Type**: Console Application
**Focus**: Dependency-aware (DAG) execution mode

Demonstrates:

- Dependency-aware execution with automatic topological sorting
- Declarative dependencies using `[SignalDependency]` attributes
- Programmatic dependencies using fluent API (`builder.DependsOn()`)
- Parallel execution of independent signal branches
- Automatic failure propagation and signal skipping
- Cycle detection and validation

Best for understanding complex startup sequences with inter-signal dependencies.

### [Web API Application](./WebApi/)

**Complexity**: Advanced
**Type**: ASP.NET Core Web API
**Focus**: Production integration patterns

Demonstrates:

- ASP.NET Core integration with dependency injection
- Health check integration with Ignition readiness
- RESTful endpoints for monitoring startup status
- Real-world signals (database, configuration, external services)
- Best-effort policy for web application scenarios
- Swagger/OpenAPI documentation

Best for understanding how to integrate Ignition in production web applications.

### [Worker Service Application](./Worker/)

**Complexity**: Advanced
**Type**: Generic Host / Worker Service
**Focus**: Background worker integration patterns

Demonstrates:

- Generic Host integration with IHostedService blocking pattern
- Worker readiness coordination using TaskCompletionSource
- Startup blocking behavior (host doesn't reach "running" until ready)
- FailFast policy for worker scenarios
- Real-world worker signals (message queue, database, distributed cache)
- Background service readiness integration

Best for understanding how to integrate Ignition in background workers and long-running services.

## Quick Start

Each sample includes its own README with detailed instructions. To run any sample:

```bash
# Navigate to the sample directory
cd samples/[Simple|Advanced|DependencyGraph|TimelineExport|Replay|WebApi]

# Run the sample
dotnet run
```

## Learning Path

We recommend exploring the samples in this order:

1. **Start with Simple** - Learn the basic concepts and API
2. **Explore Bundles** - Understand composable signal packages
3. **Move to Advanced** - Deep dive into configuration options and policies
4. **Try TimelineExport** - Visualize and analyze startup timing
5. **Try Replay** - Learn recording, replay, and what-if analysis
6. **Study DependencyGraph** - Master dependency-aware execution (DAG mode)
7. **Review WebApi** - See real-world web application integration patterns
8. **Review Worker** - See real-world background worker integration patterns

## Key Concepts Demonstrated

### Signal Implementation

- Custom `IIgnitionSignal` implementations
- Custom `IIgnitionBundle` implementations for reusable packages
- Per-signal timeout configuration
- Error handling and logging patterns

### Coordination Policies

- **FailFast**: Stop immediately on first failure
- **BestEffort**: Continue despite failures, report overall status
- **ContinueOnTimeout**: Treat timeouts as non-fatal

### Execution Modes

- **Parallel**: Run all signals simultaneously (default)
- **Sequential**: Run signals one after another in registration order
- **DependencyAware**: Run signals based on their dependency relationships (DAG mode)

### Timeout Handling

- Global timeout for overall coordination
- Per-signal timeouts for individual operations
- Cancellation behavior configuration

### Timeline Export & Analysis

- Export results to JSON for external tools
- Console-based Gantt visualization
- Concurrent group identification
- Summary statistics

### Recording & Replay

- Recording ignition runs for offline analysis
- Validating recordings for consistency
- Comparing recordings (regression detection)
- What-if simulations (timeout/failure scenarios)
- Critical path analysis

### Integration Patterns

- Dependency injection registration
- Health check integration
- Logging and monitoring
- Configuration management

## Common Signal Patterns

The samples include realistic signal implementations you can use as templates:

- **Database Connection**: Connection pool initialization
- **Configuration Validation**: Application settings verification
- **External Service Check**: Dependency connectivity validation
- **Background Service**: Worker service startup coordination
- **Cache Warmup**: Performance optimization initialization

## Additional Resources

- [Main Library Documentation](../README.md)
- [API Reference](../src/Veggerby.Ignition/README.md)
- [Contributing Guidelines](../CONTRIBUTING.md)
