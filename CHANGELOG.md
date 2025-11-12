# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added (Unreleased)

- Fluent readiness adapters: `AddIgnitionFor<T>(Func<T, Task>)`, cancellable variant, and `AddIgnitionForAll<T>` / `AddIgnitionForAllScoped<T>` composite variants (replace earlier verbosely named service adapters before first release).
- Provider composition: `AddIgnitionFromFactory(Func<IServiceProvider, Task>, name)`.
- Task adaptation: `AddIgnitionFromTask(name, Task)` plus cancellable factory overload.
- TaskCompletionSource helpers: `Ignited()`, `IgnitionFailed(Exception)`, generic variants and `IgnitionCanceled<T>()`.

### Changed (Unreleased)

- Multi-instance readiness simplified to a composite pattern (`AddIgnitionForAll`) and scoped composite (`AddIgnitionForAllScoped`); removed pre-release per-index strategy.
- Renamed pre-release APIs (`AddIgnitionServiceReadyTask*`, `AddIgnitionServicesReadyTasks`) to concise fluent forms prior to first stable tag.

### Removed (Unreleased)

- Pre-release indexed multi-instance adapter (superseded by composite variant). No public package release contained the removed API.

### Fixed (Unreleased)

- Eliminated duplicate extension class ambiguity (`TaskExtensions` vs `TaskCompletionSourceExtensions`).

### Security (Unreleased)

- No changes.

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

[Unreleased]: https://github.com/veggerby/Veggerby.Ignition/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/veggerby/Veggerby.Ignition/releases/tag/v0.1.0
