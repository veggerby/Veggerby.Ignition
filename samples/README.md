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

## Quick Start

Each sample includes its own README with detailed instructions. To run any sample:

```bash
# Navigate to the sample directory
cd samples/[Simple|Advanced|WebApi]

# Run the sample
dotnet run
```

## Learning Path

We recommend exploring the samples in this order:

1. **Start with Simple** - Learn the basic concepts and API
2. **Move to Advanced** - Understand configuration options and policies
3. **Explore WebApi** - See real-world integration patterns

## Key Concepts Demonstrated

### Signal Implementation

- Custom `IIgnitionSignal` implementations
- Per-signal timeout configuration
- Error handling and logging patterns

### Coordination Policies

- **FailFast**: Stop immediately on first failure
- **BestEffort**: Continue despite failures, report overall status
- **ContinueOnTimeout**: Treat timeouts as non-fatal

### Execution Modes

- **Parallel**: Run all signals simultaneously (default)
- **Sequential**: Run signals one after another in registration order

### Timeout Handling

- Global timeout for overall coordination
- Per-signal timeouts for individual operations
- Cancellation behavior configuration

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
