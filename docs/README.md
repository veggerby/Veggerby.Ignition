# Veggerby.Ignition Documentation

Welcome to the comprehensive documentation for Veggerby.Ignition, a lightweight startup readiness coordination library for .NET applications.

## Quick Links

- [Getting Started](getting-started.md) - Installation and first steps
- [Features Overview](features.md) - Complete feature reference
- **[Cookbook](cookbook.md)** - **Battle-tested recipes for real startup problems**

## Core Guides

### Fundamentals

- [Getting Started Guide](getting-started.md)
  - Installation and setup
  - Creating your first ignition signal
  - Common patterns and use cases
  - ASP.NET Core integration
  - Basic troubleshooting

- **[Cookbook](cookbook.md)** - **Battle-tested recipes and architecture patterns**
  - External dependency readiness (multi-stage warmup)
  - Cache warmup strategies
  - Background worker orchestration
  - Kubernetes readiness & liveness probes
  - Multi-stage startup pipelines
  - Recording/replay for production diagnosis
  - OpenTelemetry metrics integration
  - DAG vs Stages decision guide
  - Graceful degradation patterns
  - Testing startup sequences

- [Integration Recipes](integration-recipes.md)
  - Copy-paste-ready patterns for ASP.NET Core Web API
  - Generic Host / Worker Service integration with IHostedService
  - Console application patterns
  - Production deployment checklists
  - Kubernetes integration examples

### Execution Strategies

- [Dependency-Aware Execution (DAG)](dependency-aware-execution.md)
  - When to use DAG mode
  - Programmatic vs attribute-based dependencies
  - Topological sort and execution order
  - Cycle detection and troubleshooting
  - Failure propagation behavior
  - Complete worked examples

### Configuration

- [Timeout Management](timeout-management.md)
  - Two-layer timeout system explained
  - Global timeout: soft vs hard semantics
  - Per-signal timeout configuration
  - Timeout classification matrix
  - Best practices and real-world examples

- [Policies and Failure Handling](policies.md)
  - FailFast: Critical startup mode
  - BestEffort: Resilient startup
  - ContinueOnTimeout: Handling slow components
  - Combining policies with timeout strategies
  - Exception handling and diagnostics

### Advanced Features

- [Ignition Bundles](bundles.md)
  - Bundle concept and benefits
  - Using built-in bundles
  - Creating custom bundles
  - Testing and publishing bundles

- [Cancellation Scopes](cancellation-scopes.md)
  - ICancellationScope and IScopedIgnitionSignal usage
  - Bundle-scoped cancellation patterns
  - Dependency-triggered cancellation
  - Hierarchical cancellation trees
  - CancelDependentsOnFailure option

- [Advanced Patterns](advanced-patterns.md)
  - Composite signals
  - Custom signal factories
  - Testing strategies
  - Background service coordination
  - Dynamic signal registration

### Operations

- [Observability and Diagnostics](observability.md)
  - Result inspection
  - Logging configuration
  - Activity tracing with OpenTelemetry
  - Health check integration
  - Monitoring and alerting

- [Metrics Integration](metrics-integration.md)
  - IIgnitionMetrics interface overview
  - OpenTelemetry integration
  - Prometheus.NET integration
  - Custom metrics backend patterns
  - Metrics emitted by coordinator
  - Production monitoring and alerting

- [Performance Guide](performance.md)
  - Concurrency limiting
  - Execution mode characteristics
  - Memory and allocation considerations
  - Benchmark results
  - Performance tuning

### Maintenance

- [Migration Guide](migration.md)
  - Upgrading between versions
  - Breaking changes
  - Feature adoption guide

- [API Reference](api-reference.md)
  - Complete API surface
  - Interfaces, classes, and enums
  - Extension methods
  - Configuration options

- [Creating Integration Packages](creating-integration-packages.md)
  - Package structure and naming conventions
  - Retry policy integration patterns
  - Factory pattern implementation
  - Testing with Testcontainers
  - XML documentation standards
  - Directory.Build.props integration
  - README template

## Additional Resources

- [Main README](../README.md) - Overview and quick start
- [Sample Projects](../samples/README.md) - Working code examples
- [Contributing Guide](../CONTRIBUTING.md) - How to contribute
- [Changelog](../CHANGELOG.md) - Version history

## Getting Help

- **Issues**: Report bugs or request features on [GitHub Issues](https://github.com/veggerby/Veggerby.Ignition/issues)
- **Discussions**: Ask questions on [GitHub Discussions](https://github.com/veggerby/Veggerby.Ignition/discussions)

## License

Veggerby.Ignition is licensed under the [MIT License](../LICENSE).
