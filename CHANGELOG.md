# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Kafka Integration Package (`Veggerby.Ignition.Kafka`)**: Enterprise event streaming platform readiness verification
  - Supports multiple verification strategies: `ClusterMetadata` (fast broker connectivity), `TopicMetadata` (topic existence), `ProducerTest` (end-to-end message delivery), `ConsumerGroupCheck` (consumer group registration)
  - Configurable retry policies with exponential backoff (default: 3 retries, 200ms initial delay)
  - Optional Schema Registry verification for Confluent Platform deployments
  - Factory pattern support for Testcontainers and staged execution scenarios
  - Extension methods: `AddKafkaReadiness(bootstrapServers)`, `AddKafkaReadiness(producerConfig)`, `AddKafkaReadiness(bootstrapServersFactory)`
  - 26 unit tests and 10 integration tests with Testcontainers.Kafka
  - Comprehensive README with examples for all verification strategies
  - Dependencies: Confluent.Kafka 2.8.0 (official .NET client)
- **Official Metrics Packages (Prometheus & OpenTelemetry)**: Production-ready metrics integration packages
  - `Veggerby.Ignition.Metrics.Prometheus`: Prometheus metrics implementation using prometheus-net library
    - Exposes `ignition_signal_duration_seconds` (histogram), `ignition_signal_total` (counter), `ignition_total_duration_seconds` (histogram)
    - Extension method: `AddPrometheusIgnitionMetrics()`
    - Comprehensive README with Grafana PromQL examples
  - `Veggerby.Ignition.Metrics.OpenTelemetry`: OpenTelemetry metrics implementation using System.Diagnostics.Metrics
    - Exposes `ignition.signal.duration` (histogram), `ignition.signal.status` (counter), `ignition.total.duration` (histogram)
    - Extension method: `AddOpenTelemetryIgnitionMetrics()`
    - Compatible with all standard OTEL exporters (Prometheus, Console, OTLP, Jaeger, Zipkin)
  - Both packages maintain zero-dependency principle for core library
  - Thread-safe implementations optimized for low overhead
  - 26 comprehensive unit tests (12 Prometheus + 14 OpenTelemetry)
- **Custom Policy Support (`IIgnitionPolicy`)**: Extensible policy system for custom failure handling strategies
  - `IIgnitionPolicy` interface with `ShouldContinue(IgnitionPolicyContext)` method for policy decisions
  - `IgnitionPolicyContext` class providing signal result, completed signals, total count, elapsed time, and execution mode
  - Built-in policy implementations: `FailFastPolicy`, `BestEffortPolicy`, `ContinueOnTimeoutPolicy`
  - `IgnitionOptions.CustomPolicy` property to override built-in `Policy` enum
  - `GetEffectivePolicy()` method for backward-compatible policy resolution
  - DI registration methods: `AddIgnitionPolicy(instance)`, `AddIgnitionPolicy(factory)`, `AddIgnitionPolicy<TPolicy>()`
  - Simple Mode API methods: `WithCustomPolicy(instance)`, `WithCustomPolicy(factory)`, `WithCustomPolicy<TPolicy>()`
  - Enables custom logic: retry strategies, circuit breakers, conditional fail-fast, percentage-based thresholds
  - Comprehensive test coverage: 18 new tests covering custom policies, DI integration, and Simple Mode API
- **Retry Policy Standardization**: All 13 integration packages now include consistent retry policy support
  - New `MaxRetries` and `RetryDelay` properties in all `*ReadinessOptions` classes (default: 3 retries with 1-second delay)
  - Graceful handling of transient startup failures with configurable retry behavior
  - Enhanced logging: Changed from "not ready yet" to "failed (transient, attempt X/Y)" for clarity
  - Factory pattern (`IIgnitionSignalFactory`) implementations for all integration packages enabling DI-based configuration
  - Factory-based extension method overloads supporting staged execution across all packages
  - Packages affected: Postgres, SqlServer, MongoDb, Marten, RabbitMq, MassTransit, Redis, Memcached, Http, Grpc, Orleans, Aws (S3), Azure (Blob, Queue, Table)
- **Comprehensive Documentation Guides**: Three major new documentation resources for advanced features
  - `docs/cancellation-scopes.md`: Hierarchical cancellation patterns, bundle-scoped cancellation, dependency-triggered cancellation (696 lines)
  - `docs/metrics-integration.md`: Production observability integration with OpenTelemetry, Prometheus, custom backends (755 lines)
  - `docs/creating-integration-packages.md`: Authoring guide for integration package developers with best practices, patterns, and testing strategies (809 lines)
  - Enhanced existing documentation: `docs/performance.md` with detailed scaling characteristics and `docs/timeout-management.md` with timeout strategy patterns
- **Documentation Standards**: AGENTS.md and copilot-instructions.md enhanced with CHANGELOG maintenance guidelines
  - Only include user-impacting changes (exclude unit test changes, internal refactoring, build/CI configuration)
  - Consolidate related changes into single entries representing net change from last release
  - Follow Keep a Changelog format with Added/Changed/Fixed sections

### Changed

- None.

### Fixed

- **FailFastPolicy Semantics**: Changed to only stop execution on `Failed` status
  - `TimedOut`, `Skipped`, and `Cancelled` statuses now treated as non-failures
  - Aligns implementation with documentation and expected behavior

## [0.5.0] - 2026-01-14

### Added

- **Integration Packages**: 13 new packages for common infrastructure dependencies
  - **Databases**: `Veggerby.Ignition.Postgres`, `Veggerby.Ignition.SqlServer`, `Veggerby.Ignition.MongoDb`, `Veggerby.Ignition.Marten`
  - **Message Brokers**: `Veggerby.Ignition.RabbitMq`, `Veggerby.Ignition.MassTransit`
  - **Caches**: `Veggerby.Ignition.Redis`, `Veggerby.Ignition.Memcached`
  - **Communication**: `Veggerby.Ignition.Http`, `Veggerby.Ignition.Grpc`, `Veggerby.Ignition.Orleans`
  - **Cloud Storage**: `Veggerby.Ignition.Aws`, `Veggerby.Ignition.Azure`
  - Each package provides readiness signals with verification strategies, retry policies, and Testcontainers integration test support
  - All signals implement `IIgnitionSignalFactory` for consistent DI registration
- **Staged Execution Fluent API**: New `AddIgnitionStage()` extension method with `IgnitionStageSignalBuilder` for grouping signals by stage
  - Simplifies multi-stage configuration with fluent syntax: `services.AddIgnitionStage(0, stage => stage.AddTaskSignal(...))`
  - `TaskSignalOptions` class with `Stage`, `Timeout`, and `ExecutionMode` properties for signal-level configuration
  - Improved discoverability and reduced boilerplate for common staged execution patterns
- **Graph Building Helper**: `AddIgnitionGraphFromFactories()` extension method simplifies DAG creation
  - Automatically resolves `IIgnitionSignalFactory` instances and creates signals
  - Optional `applyAttributeDependencies` parameter to apply `SignalDependencyAttribute` decorations
  - Optional `configure` delegate for manual dependency configuration
  - Reduces graph setup from ~10 lines to 1-2 lines of code
- **Redis Resilience Configuration**: New `ConnectTimeout` property in `RedisReadinessOptions`
  - Default 10-second timeout to handle container-ready-but-service-not-ready scenarios
  - Configurable per-signal for different environments (low-latency vs. slow startup)
  - Factory automatically applies `AbortOnConnectFail = false` for background retry resilience
- **Enhanced Integration Testing**: Testcontainers support with proper wait strategies and retry configuration
  - All integration packages include `[Trait("Category", "Integration")]` tagged tests
  - Intentional use of `Wait.ForUnixContainer()` to demonstrate service-level vs. container-level readiness
  - Configurable retry policies (`MaxRetries`, `RetryDelay`) to handle transient startup failures
  - TestcontainersDemo sample demonstrating multi-stage infrastructure startup (5 containers across 4 stages)
- **Lifecycle Hooks (`IIgnitionLifecycleHooks`)**: Extensible observation points for custom logic during ignition execution
  - `OnBeforeIgnitionAsync()`: Invoked once before any signals execute (global setup, telemetry initialization)
  - `OnAfterIgnitionAsync(IgnitionResult)`: Invoked once after all signals complete (cleanup, final telemetry recording)
  - `OnBeforeSignalAsync(signalName)`: Invoked before each individual signal executes (per-signal setup, logging)
  - `OnAfterSignalAsync(IgnitionSignalResult)`: Invoked after each individual signal completes (per-signal telemetry, conditional logging)
  - Hooks are read-only observers and cannot modify ignition behavior or results
  - Exceptions thrown by hooks are caught and logged without affecting execution or results
  - Configure via `IgnitionOptions.LifecycleHooks` or `AddIgnitionLifecycleHooks()` extension method with DI factory support
  - Common use cases: OpenTelemetry enrichment, custom metrics, cleanup operations, external system integration
- **Documentation**: Comprehensive `AGENTS.md` for project setup, development workflow, testing patterns, and coding standards

### Changed

- **BREAKING**: Removed `*WithStage()` extension methods from all integration packages in favor of options-based staging
  - Old: `services.AddRedisReadinessWithStage(connectionString, 1)`
  - New: `services.AddRedisReadiness(connectionString, options => options.Stage = 1)`
  - Applies to all 13 integration packages (Postgres, SqlServer, MongoDb, Marten, RabbitMq, MassTransit, Redis, Memcached, Http, Grpc, Orleans, Aws, Azure)
  - Provides consistency with other configuration options and better extensibility
- **BREAKING**: All `*ReadinessSignal` implementations are now `internal` and only accessible via `IIgnitionSignalFactory`
  - Improves encapsulation and maintains abstraction boundaries between modules
  - Test assemblies use `InternalsVisibleTo` attribute for testing
  - Users must register signals through extension methods (e.g., `AddRedisReadiness()`)
- **Enhanced Retry Logging**: Changed `RetryPolicy` log messages from `"not ready yet"` to `"failed (transient, attempt X/Y)"`
  - More accurately reflects that an exception occurred while indicating it's expected behavior
  - Helps distinguish between unexpected errors and normal startup retry conditions
- **XML Documentation**: Improved parameter documentation for `AddIgnitionGraphFromFactories()`
  - Clarified `applyAttributeDependencies` behavior when `true` vs. `false`
  - Documented use cases for complete manual control and attribute scanning overhead concerns
- **Internal Cleanup**: Removed unused `_executionMode` field from `IgnitionStageSignalBuilder` with explanatory comment for future use

### Fixed

- **gRPC Test Performance**: Reduced gRPC unit test execution time from 27 seconds to 0.5 seconds (54x improvement)
  - Added `CreateFastTimeoutChannel()` helper with 1-second `ConnectTimeout` and `SocketsHttpHandler` configuration
  - Replaced invalid hostnames with `127.0.0.1` on unlikely ports to avoid DNS resolution delays
  - Removed `[Trait("Speed", "Slow")]` attributes as tests now complete in milliseconds
- **Redis Integration Test Stability**: Eliminated flaky Redis integration tests in CI environments
  - Added `abortConnect=false` and `connectTimeout=10000` to connection strings
  - Configured `RedisReadinessSignalFactory` to set `AbortOnConnectFail = false` and use `ConnectTimeout` from options
  - Increased integration test retry settings (`MaxRetries = 10`, `RetryDelay = 500ms`) to handle transient startup failures
  - Tests now reliably handle the container-ready-before-service-ready window in containerized environments

## [0.4.1] - 2025-12-18

### Added

- **Ignition Cookbook**: Comprehensive production-ready recipes documentation (`docs/cookbook.md`) with 10 battle-tested startup patterns:
  - External Dependency Readiness (multi-stage warmup: Redis → SQL → Elasticsearch)
  - Cache Warmup Strategies (critical vs optional caches with concurrency limiting)
  - Background Worker Orchestration (`BackgroundService` coordination patterns)
  - Kubernetes Integration (complete deployment YAML with startup/readiness/liveness probes)
  - Multi-Stage Pipelines (5-phase startup orchestration)
  - Recording/Replay Workflows (production diagnosis with comparison, validation, what-if simulation)
  - OpenTelemetry Metrics Integration (`IIgnitionMetrics` implementation with Prometheus)
  - DAG vs Stages Decision Matrix (criteria and examples for choosing execution model)
  - Graceful Degradation Patterns (optional service failure handling)
  - Testing Patterns (unit/integration test helpers)
  - Each recipe includes problem statement, configuration pattern, expected behavior, and usage guidance
- **Performance Benchmarks & Contract**:
  - New `benchmarks/Veggerby.Ignition.Benchmarks` project using BenchmarkDotNet
  - 6 comprehensive benchmark suites covering all execution modes:
    - `CoordinatorOverheadBenchmarks` with memory diagnostics
    - `ExecutionModeBenchmarks` (Parallel, Sequential, 1-1000 signals)
    - `DependencyAwareExecutionBenchmarks` (DAG with 10-100 signals)
    - `StagedExecutionBenchmarks` (2-10 stages)
    - `ObservabilityOverheadBenchmarks` (tracing enabled/disabled)
    - `ConcurrencyLimitingBenchmarks` (MaxDegreeOfParallelism variations)
  - Performance contract documentation (`docs/performance-contract.md`) with official guarantees:
    - Overhead per signal characteristics (< 1ms per signal for small counts, < 100ms for 1000 signals)
    - Scaling characteristics for all execution modes
    - Recommendations for signal count limits per execution mode
    - Determinism and serialization stability contracts
  - 9 new determinism and stability tests in `DeterminismAndStabilityTests.cs`:
    - Schema version validation (v1.0)
    - JSON serialization round-trip stability
    - Classification determinism verification
    - Backward compatibility testing
  - Updated `docs/performance.md` with cross-references to performance contract

### Changed

- None.

### Fixed

- Set `IsPackable` to `false` in test and benchmark project files to prevent accidental NuGet packaging of non-library projects.

## [0.4.0] - 2025-12-09

### Added

- **Simple Mode API (Opinionated Startup Facade)**: New minimal-configuration API for 80-90% of use cases.
  - `IIgnitionBuilder` fluent builder interface with single entry point
  - `AddSimpleIgnition()` extension method for streamlined configuration
  - Pre-configured profiles for common application types:
    - `.UseWebApiProfile()`: 30s timeout, BestEffort policy, Parallel execution, Tracing enabled
    - `.UseWorkerProfile()`: 60s timeout, FailFast policy, Parallel execution, Tracing enabled
    - `.UseCliProfile()`: 15s timeout, FailFast policy, Sequential execution, Tracing disabled
  - Fluent signal registration: `.AddSignal(name, factory)`, `.AddSignal<TSignal>()`
  - Override capabilities: `.WithGlobalTimeout()`, `.WithDefaultSignalTimeout()`, `.WithTracing()`
  - Advanced feature access via `.ConfigureAdvanced(options => ...)` for power users
  - Production-ready setup in fewer than 10 lines of code
  - Full backward compatibility with existing `AddIgnition()` API
  - New `SimpleMode` sample demonstrating all profiles and customization options

### Changed

- **Package Restructuring**: Reorganized source files into logical folders with updated namespaces for improved code organization and discoverability.
  - **Core/** (`Veggerby.Ignition`): Essential startup coordination (interfaces, coordinator, builder, options, policies, results)
  - **Graph/** (`Veggerby.Ignition`): Dependency-aware execution (DAG builder, graph interface, dependency attributes)
  - **Stages/** (`Veggerby.Ignition.Stages`): Multi-phase execution (staged signals, stage policies, stage results)
  - **Diagnostics/** (`Veggerby.Ignition.Diagnostics`): Recording, replay, and timeline export functionality
  - **HealthChecks/** (`Veggerby.Ignition.HealthChecks`): ASP.NET health check integration
  - **Metrics/** (`Veggerby.Ignition.Metrics`): Observability and metrics interfaces
  - **Extensions/** (`Veggerby.Ignition`): Extension points (bundles, timeout strategies, DI extensions, helpers)
  - **Bundles/** (`Veggerby.Ignition.Bundles`): Built-in bundle implementations
  - **BREAKING**: Namespace changes for specialized functionality:
    - `Veggerby.Ignition.Diagnostics`: Timeline, Recording, and Replay types (add `using Veggerby.Ignition.Diagnostics;`)
    - `Veggerby.Ignition.Stages`: Staged execution types like `IStagedIgnitionSignal`, `IgnitionStagePolicy`, `IgnitionStageResult`
    - `Veggerby.Ignition.Bundles`: Bundle implementations like `DatabaseTrioBundle`, `HttpDependencyBundle`
    - `Veggerby.Ignition.Metrics`: Metrics interfaces like `IIgnitionMetrics`
    - `Veggerby.Ignition.HealthChecks`: Internal health check implementation (not typically referenced by users)
  - Core types remain in `Veggerby.Ignition` namespace (backward compatible)

### Fixed

- None.

## [0.3.1] - 2025-12-03

### Added

- **Ignition Replay + Historical Recordings**: Ability to record ignition runs and replay them for diagnostics/testing.
  - `IgnitionRecording` record for serializable execution records
  - `IgnitionRecordedSignal` record capturing per-signal timing, dependencies, failures, and cancellation details
  - `RecordExecution()` extension method on `IgnitionResult` to produce structured recordings
  - `ExportRecordingJson()` convenience method for JSON serialization
  - `IgnitionReplayer` class for validating and analyzing recorded executions
  - Replay validation features:
    - Timing drift detection (signals completing in unexpected order relative to recording)
    - Dependency consistency validation
    - Invariant checking (e.g., all dependencies completed before dependent signals)
  - JSON schema v1.0 for stable serialization format
  - New `Replay` sample demonstrating recording and replay features
- New sample project: `Replay` demonstrating recording, serialization, and replay analysis

### Changed

- **Framework Support**: Added .NET 10.0 target while maintaining .NET 8.0 and .NET 9.0 support
  - All projects now multi-target `net10.0;net9.0;net8.0`
- **Dependency Updates**: Updated all Microsoft.Extensions.* packages to version 10.0.x
- **Package Updates**:
  - Swashbuckle.AspNetCore updated to 10.0.1
  - Microsoft.NET.Test.Sdk updated to 18.0.1
  - xunit updated to 2.9.3
  - xunit.runner.visualstudio updated to 3.1.5
  - NSubstitute updated to 5.3.0
  - AwesomeAssertions updated to 9.3.0

## [0.3.0] - 2025-12-02

### Added

- **Staged Execution Mode (Multi-Phase Startup Pipeline)**: New `IgnitionExecutionMode.Staged` supporting sequential stages with parallel execution within each stage.
  - `IStagedIgnitionSignal` interface with `Stage` property for declarative stage assignment
  - `IgnitionStagePolicy` enum controlling cross-stage transition behavior (`AllSucceeded`, `AnySucceeded`, `BestEffort`)
  - `IgnitionStageResult` record providing per-stage outcome diagnostics
  - Configurable stage behavior via `IgnitionOptions.StagePolicy`
  - Signals without stage info default to Stage 0
  - Rich stage-level diagnostics in result aggregation
- **Hierarchical Cancellation Scopes (Structured Cancellation Trees)**: Enhanced cancellation model beyond flat global vs. per-signal.
  - `ICancellationScope` interface for hierarchical cancellation token management
  - `CancellationScope` implementation with parent-child relationships
  - `CancellationReason` enum for tracking cancellation causes
  - `IScopedIgnitionSignal` interface for signals associated with specific scopes
  - Support for grouped cancellation semantics:
    - Cancel entire stage
    - Cancel all signals dependent on a failed signal
    - Cancel all signals sharing a bundle or scope
  - Accurate reporting: "Signal X cancelled due to group cancellation triggered by Y"
- **State Machine with Event Hooks**: Ignition coordinator now exposes observable lifecycle events.
  - `IgnitionState` enum representing coordinator state (`NotStarted`, `Running`, `Completed`, `Failed`, `TimedOut`)
  - `IgnitionEventArgs` event argument types for lifecycle events
  - Observable events:
    - `OnSignalStarted` - Fired when a signal begins execution
    - `OnSignalCompleted` - Fired when a signal finishes (success, failure, or timeout)
    - `OnGlobalTimeout` - Fired when global timeout elapses
    - `OnCoordinatorCompleted` - Fired when all signals complete
  - Thread-safe event publication
  - Non-blocking event handler execution
  - Real-time progress tracking support
- **Timeout Strategy Plugins**: Pluggable timeout determination via `IIgnitionTimeoutStrategy`.
  - `IIgnitionTimeoutStrategy` interface for custom timeout logic
  - `DefaultIgnitionTimeoutStrategy` implementation preserving existing behavior
  - Configurable via `IgnitionOptions.TimeoutStrategy`
  - Enables:
    - Exponential scaling based on failure count
    - Adaptive timeouts (e.g., slow I/O detection)
    - Dynamic per-stage deadlines
    - User-defined per-class/per-assembly defaults
  - Deterministic and thread-safe contract
- **Metrics Adapter (Zero-Dependency, Pluggable Metrics)**: Structured internal metrics API for observability integration.
  - `IIgnitionMetrics` interface with methods:
    - `RecordSignalDuration(name, duration)`
    - `RecordSignalStatus(name, status)`
    - `RecordTotalDuration(duration)`
  - `NullIgnitionMetrics` default no-op implementation (zero overhead)
  - Configurable via `IgnitionOptions.Metrics`
  - Users can plug in OpenTelemetry, Prometheus, App Metrics, or custom backends
  - Thread-safe contract designed for hot-path efficiency
- **Timeline Export (Gantt-like Output)**: New structured timeline export for startup analysis and visualization.
  - `IgnitionTimeline` record with comprehensive timing data
  - `ExportTimeline()` extension method on `IgnitionResult` produces structured timeline
  - `ExportTimelineJson()` convenience method for direct JSON export
  - `ToConsoleString()` and `WriteToConsole()` methods for Gantt-like ASCII visualization
  - Timeline data includes: signal start/end times, concurrent groups, stage information, boundary markers
  - Summary statistics: slowest/fastest signals, max concurrency, average duration
  - JSON schema v1.0 for forward compatibility with external visualization tools
  - New `TimelineExport` sample demonstrating all timeline features
- Extended `IgnitionSignalResult` with `StartedAt` and `CompletedAt` timestamps (backward compatible)
- Extended `IgnitionResult` with `HasTimelineData` property for timeline availability checking
- Extended `IgnitionOptions` with `StagePolicy`, `TimeoutStrategy`, and `Metrics` properties
- New samples: `TimelineExport`, `TimeoutStrategies` demonstrating new features

### Changed

- Coordinator internal flow refactored to support state machine transitions and event publication
- Event hooks added to signal execution lifecycle without breaking existing behavior
- Metrics recording integrated into hot path with minimal allocation overhead
- Timeline timestamp capture now mandatory (previously duration-only)

### Fixed

- Improved thread-safety of event publication mechanisms
- Refined stage transition logic for edge cases (empty stages, all-failed stages)

### Performance

- Metrics integration designed for zero overhead when using default `NullIgnitionMetrics`
- Event publication uses non-blocking fire-and-forget pattern
- Timeline data collection optimized for minimal allocation in hot path

## [0.2.0] - 2025-11-24

### Added

- **Dependency-Aware Execution (DAG)**: New `IgnitionExecutionMode.DependencyAware` supporting directed acyclic graph execution with automatic parallel execution of independent branches.
- **Dependency Graph Builder**: `IgnitionGraphBuilder` with fluent API for declaring signal dependencies programmatically via `DependsOn()` method.
- **Declarative Dependencies**: `[SignalDependency]` attribute for specifying dependencies by signal name or type.
- **Automatic Dependency Discovery**: `ApplyAttributeDependencies()` method to wire dependencies from attributes automatically.
- **Topological Sort**: Automatic ordering of signals based on dependency relationships.
- **Cycle Detection**: Graph validation with detailed diagnostic messages showing exact cycle path.
- **Failure Propagation**: Automatic skipping of dependent signals when prerequisites fail, with `FailedDependencies` tracking.
- **Graph Queries**: `IIgnitionGraph` interface with methods to query dependencies, dependents, root signals, and leaf signals.
- **Ignition Bundles**: `IIgnitionBundle` abstraction for composable, reusable signal packages.
- **Built-in Bundles**: `HttpDependencyBundle` for HTTP endpoint readiness verification and `DatabaseTrioBundle` for database initialization sequences.
- **Bundle Registration**: `AddIgnitionBundle()`, `AddIgnitionBundle<TBundle>()`, and `AddIgnitionBundles()` extension methods.
- **Bundle Options**: `IgnitionBundleOptions` for per-bundle timeout and policy configuration.
- **Graph Registration**: `AddIgnitionGraph()` extension method for registering dependency graphs with service provider integration.
- **Skipped Status**: New `IgnitionSignalStatus.Skipped` for signals not executed due to failed dependencies.
- **Comprehensive Documentation**: Added detailed feature documentation in `/docs/FEATURES.md` covering all library capabilities.
- Fluent readiness adapters: `AddIgnitionFor<T>(Func<T, Task>)`, cancellable variant, and `AddIgnitionForAll<T>` / `AddIgnitionForAllScoped<T>` composite variants.
- Provider composition: `AddIgnitionFromFactory(Func<IServiceProvider, Task>, name)`.
- Task adaptation: `AddIgnitionFromTask(name, Task)` plus cancellable factory overload.
- TaskCompletionSource helpers: `Ignited()`, `IgnitionFailed(Exception)`, generic variants and `IgnitionCanceled<T>()`.

### Changed

- `IgnitionSignalResult` now includes `FailedDependencies` property (empty for non-DAG modes).
- Multi-instance readiness simplified to a composite pattern (`AddIgnitionForAll`) and scoped composite (`AddIgnitionForAllScoped`).
- Renamed pre-release APIs (`AddIgnitionServiceReadyTask*`, `AddIgnitionServicesReadyTasks`) to concise fluent forms.

### Fixed

- Eliminated duplicate extension class ambiguity (`TaskExtensions` vs `TaskCompletionSourceExtensions`).
- Fixed race condition in `DependencyAware_ComplexGraph_ExecutesCorrectly` test by using thread-safe timestamp tracking instead of non-thread-safe list operations.

### Removed

- Pre-release indexed multi-instance adapter (superseded by composite variant). No public package release contained the removed API.

## [0.1.0] - 2025-11-05

### Added

- Initial public release of Veggerby.Ignition.
- Core abstractions: `IIgnitionSignal`, `IIgnitionCoordinator`, `IgnitionSignalStatus`, `IgnitionResult`.
- Policies: `FailFast`, `BestEffort`, `ContinueOnTimeout`.
- Execution modes: `Parallel`, `Sequential` plus optional `MaxDegreeOfParallelism`.
- Global timeout semantics (soft vs. hard via `CancelOnGlobalTimeout`).
- Per-signal timeout support with optional cancellation (`CancelIndividualOnTimeout`).
- Activity tracing toggle (`EnableTracing`).
- Health check integration (`ignition-readiness`).
- Slow signal logging (`LogTopSlowHandles`, `SlowHandleLogCount`).
- Factory helpers: `IgnitionSignal.FromTask`, `IgnitionSignal.FromTaskFactory`.
- Deterministic, idempotent coordination (signals executed once, cached result retrieval).

### Changed

- N/A (initial version).

### Deprecated

- None.

### Removed

- None.

### Fixed

- N/A (initial version).

### Security

- No known security issues.

[Unreleased]: https://github.com/veggerby/Veggerby.Ignition/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.4.1
[0.4.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.4.0
[0.3.1]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.3.1
[0.3.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.3.0
[0.2.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.2.0
[0.1.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.1.0
