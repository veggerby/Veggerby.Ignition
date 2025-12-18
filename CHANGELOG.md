# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- None.

### Changed

- None.

### Fixed

- None.

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
