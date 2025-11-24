# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.2.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.2.0
[0.1.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.1.0
