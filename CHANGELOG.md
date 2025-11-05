# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added (0.1.0)

- (Placeholder) Additional ignition policies or execution strategies under consideration.
- (Placeholder) Potential signal adapters (e.g., async enumerable, channel drain) not yet implemented.

### Changed (0.1.0)

- None.

### Deprecated (0.1.0)

- None.

### Removed (0.1.0)

- None.

### Fixed (0.1.0)

- None.

### Security (0.1.0)

- None.

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
