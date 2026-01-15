## Project Overview

Veggerby.Ignition is a lightweight, extensible .NET library for coordinating application startup readiness. Applications register one or more asynchronous readiness operations called ignition signals (`IIgnitionSignal`). The ignition coordinator (`IIgnitionCoordinator`) awaits them collectively, applying configurable global and per-signal timeouts, execution policies, optional Activity tracing, health check reporting, parallelism limiting and structured diagnostics. Evaluation is idempotent: signals are executed at most once and results are cached for subsequent inspection.

### What It Provides
* Simple readiness abstraction: `IIgnitionSignal` (Name, optional `Timeout`, `WaitAsync`).
* Coordinated waiting via `IIgnitionCoordinator` with cached aggregated result (`IgnitionResult`).
* Configurable global timeout (soft vs. hard via `CancelOnGlobalTimeout`).
* Per-signal timeout classification with optional cancellation (`CancelIndividualOnTimeout`).
* Policies (`IgnitionPolicy`): `FailFast`, `BestEffort`, `ContinueOnTimeout` controlling failure/timeout continuation behavior.
* Execution modes: `Parallel` or `Sequential` plus optional concurrency limiting (`MaxDegreeOfParallelism`).
* Activity tracing toggle (`EnableTracing`).
* Health check integration registering `ignition-readiness` (`IgnitionHealthCheck`).
* Slow signal logging (top N durations) for startup performance insight.
* Factory helpers (`IgnitionSignal.FromTask`, `.FromTaskFactory`) for adapting existing tasks or cancellable task factories.
* Deterministic classification semantics for global vs. per-signal timeout outcomes (soft vs. hard).

### What It Explicitly Does Not Provide
* Generic workflow/job orchestration beyond startup readiness.
* Runtime dependency graph resolution or ordering heuristics (sequential mode is explicit order only).
* Automatic retries/backoff strategies (user code implements if desired inside signal).
* Distributed coordination or clustering primitives.
* Hidden side-effects beyond DI registrations; no mutable static global state.

### Mental Model (Authoritative)
Signals represent asynchronous readiness gates ("component is initialized"). The coordinator evaluates all registered signals once, producing a snapshot of per-signal outcomes (`Succeeded`, `Failed`, `TimedOut`) plus overall timing and timeout classification. Configuration (`IgnitionOptions`) shapes scheduling (parallel/sequential), deadlines (global vs. per-signal), cancellation behavior, logging and tracing. Determinism: identical set of signal tasks + identical option values => identical aggregated classification outcomes. The library itself does not introduce randomness; signal implementations may perform I/O but their outcomes are externally determined. Global timeout is soft unless explicitly promoted to hard cancellation (`CancelOnGlobalTimeout = true`); soft elapse alone does not force a timed-out result unless at least one signal times out.

## Folder / Layer Structure

Core Library (`src/Veggerby.Ignition`): public abstractions (interfaces, enums, options), coordinator implementation, factory helpers, DI extensions, health check.

Tests (`test/Veggerby.Ignition.Tests`): unit tests covering coordinator paths (policies, execution modes, global/per-signal timeouts, concurrency limiting, idempotency), factory helpers, signal behaviors.

Docs: Inline XML documentation; root `README.md` (usage, semantics). Add or update when introducing new public concepts (e.g., new policies, execution modes, health reporting changes).

## Libraries & Frameworks

* .NET (C#) targeting current LTS / latest (net10.0 currently).
* Microsoft.Extensions.* (Logging, Options, DependencyInjection, Diagnostics.HealthChecks).
* xUnit + AwesomeAssertions + NSubstitute for testing.
* No other runtime dependencies (keep surface minimal).

## Coding Standards

* Obey repository `.editorconfig`.
* Block namespaces with Allman braces (e.g., `namespace Veggerby.Ignition { ... }` as in current codebase).
* Indentation: 4 spaces, never tabs. No trailing whitespace.
* Using directives: `using System.*` first when present, then other framework / third-party namespaces logically grouped (avoid blank lines inside a group). Keep ordering consistent; prefer stable minimal diff over heavy reordering.
* Naming: private fields `_camelCase`; public members PascalCase; constants PascalCase.
* Always use braces for all control blocks (if/else/for/while) even if single statement.
* Expression-bodied members allowed when they materially improve clarity (small immutable factory helpers OK).
* Avoid LINQ in hot coordination paths where explicit loops aid performance and allocation clarity (current coordinator uses explicit loops except for benign `Select` when constructing final results—maintain or justify changes).
* XML docs required on all public types/members explaining semantics especially around timeout and cancellation behavior.
* Tests segmented with `// arrange`, `// act`, `// assert` comments for readability.
* When editing a file, ensure full-file compliance (formatting, spacing, brace style) not just changed lines.

## Formatting Preferences
* Single blank line between logically distinct sections (between methods, after variable declaration blocks, before returns when it aids readability—not required for trivial returns).
* Avoid multiple consecutive blank lines.
* Allman braces for types and methods (as currently implemented).
* Group inside methods: guards (early returns), setup declarations, core logic loops/awaits, classification/post-processing, each separated by one blank line.
* Avoid vertical alignment / column art; simple single spaces around operators and after commas.
* Keep coordinator hot path readable: separation between scheduling, timeout evaluation, result classification, logging blocks.
* End-of-file: no trailing blank lines, ensure newline at EOF.
* Using directives: system namespace(s) first, then a blank line, then others.
* Prefer early-return guard clauses with a blank line following the guard's block.

## Library Semantics & Invariants

* Signals are awaited at most once per coordinator instance (idempotent execution cached via lazy task).
* Classification semantics are stable: same signal tasks + same options produce identical `IgnitionResult` outcome structure.
* Per-signal timeouts only affect that signal's classification (and cancellation if configured) without mutating other signals.
* Global timeout soft vs. hard behavior strictly follows `CancelOnGlobalTimeout` flag; do not introduce implicit cancellation elsewhere.
* Health check reflects the cached result—does not trigger re-evaluation.
* Coordinator never mutates or wraps external tasks beyond awaiting and timeout classification; it must not swallow exceptions silently (exceptions captured, logged, surfaced via result or AggregateException per policy).
* No hidden global state—behavior entirely driven by injected signals and `IgnitionOptions`.
* Determinism: library internal logic does not introduce randomness; timing differences only influence measured durations, not classification logic (except whether deadlines elapse). Avoid using `DateTime.Now`; rely on `Stopwatch` for elapsed measurement.

## Testing Requirements

* Frameworks: xUnit, AwesomeAssertions, NSubstitute only.
* Cover coordinator behaviors: parallel vs. sequential, fail-fast vs. best-effort vs. continue-on-timeout, global timeout soft vs. hard, per-signal timeout cancellation, concurrency limiting, idempotent multiple waits, exception aggregation.
* Each policy path must be exercised for success and failure scenarios.
* Tests independent (no ordering reliance); no shared mutable static state.
* Use `// arrange`, `// act`, `// assert` comments.
* For new options or policies, add focused tests including edge cases (zero signals, single failing signal, mixed statuses, cancellation tokens).
* Avoid flakiness: prefer deterministic delays (small `Task.Delay`) under generous global timeouts or use manual `TaskCompletionSource` signaling.
* When measuring concurrency effects, assert qualitative properties (e.g., limited start count) rather than brittle exact timing.

### Example Test Pattern
```csharp
[Fact]
public async Task FailFast_Sequential_StopsOnFirstFailure()
{
    // arrange
    var failing = new FaultingSignal("db", new InvalidOperationException("boom"));
    var never = new CountingSignal("never-run");
    var coord = CreateCoordinator(new[] { failing, never }, o =>
    {
        o.ExecutionMode = IgnitionExecutionMode.Sequential;
        o.Policy = IgnitionPolicy.FailFast;
        o.GlobalTimeout = TimeSpan.FromSeconds(1);
    });

    // act
    AggregateException? ex = null;
    try { await coord.WaitAllAsync(); } catch (AggregateException a) { ex = a; }

    // assert
    ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
    never.InvocationCount.Should().Be(0);
}
```

## Performance Guidelines

* Minimize allocations in coordinator hot path (loop over signals, use pre-sized lists when building results).
* Avoid excessive task wrappers; prefer direct awaits and reuse of created tasks (lazy factory ensures single invocation).
* Use `Stopwatch` for timing (already done); avoid `DateTime.UtcNow` for performance measurement.
* LINQ usage permissible for non-hot aggregation steps (final result projection) but justify any LINQ introduced inside tight loops; prefer explicit loops if uncertain.
* No blocking waits (`Task.Result` / `.Wait()`)—use async throughout.
* Cancellation tokens should not allocate unbounded registrations; only register when necessary (e.g., in test helper signals) and avoid repeated registrations per await where possible.
* **Always include `nameof` parameter** in all `Argument*Exception.ThrowIf*()` calls (e.g., `ArgumentNullException.ThrowIfNull(param, nameof(param))`, `ArgumentException.ThrowIfNullOrWhiteSpace(str, nameof(str))`) for improved exception diagnostics and easier error triaging.

## Documentation Expectations

* All public types/members require XML docs describing semantics, especially timeout/cancellation interactions and policy behaviors.
* Update `README.md` for any new public policy, execution mode, health check semantics, or classification logic changes.
* Add CHANGELOG entry for user-visible changes following consolidation rules:
  - Only include important, user-impacting changes (skip internal refactoring, unit test changes, documentation-only updates)
  - Consolidate related changes: if multiple commits modify the same feature before release, combine into one entry representing the net change from last release
  - Example: "Added A" + "Changed A to B" + "Moved B to C" = "Added C" (users don't need intermediate steps)
  - Format: Follow [Keep a Changelog](https://keepachangelog.com/) with Added/Changed/Fixed sections under `[Unreleased]`
  - Be specific: include package names for integration changes, describe what changed and why it matters
* Keep examples deterministic and concise.
* Remove stale roadmap items once implemented; do not leave completed items lingering.

## Dependency Policy

* Core: zero external dependencies beyond BCL + necessary Microsoft.Extensions packages (Logging, Options, DI, HealthChecks, Diagnostics).
* Tests: xUnit, AwesomeAssertions, NSubstitute only.
* Avoid bringing in telemetry SDKs directly—expose tracing via `ActivitySource` only.

## Forbidden Patterns

* Re-running signal initialization side-effects multiple times (breaks idempotency).
* Blocking waits on async tasks (`.Result`, `.Wait()`).
* Hidden static mutable state affecting scheduling or classification.
* Silently swallowing exceptions (must classify or propagate through AggregateException as per policy).
* Random/jitter logic inside coordinator affecting classification (timing measurement only via `Stopwatch`).
* Unbounded task fan-out ignoring configured `MaxDegreeOfParallelism`.
* Heavy LINQ usage in hot path loops degrading performance.
* Health check triggering fresh evaluation instead of using cached result.
* Reflection-based dynamic invocation of signals beyond DI construction.

## Suitable / Unsuitable Tasks

Suitable:
* Add new `IgnitionPolicy` + coordinator logic + tests + README updates.
* Introduce new execution strategy (e.g., staged batches) with options & tests.
* Add signal adapter factory (e.g., wrapping `ValueTask`, channel drain, async enumerable completion).
* Enhance health check output (structured failure detail) while using cached result.
* Add option controlling slow signal threshold logging (tests + docs).
* Optimize scheduling to reduce allocations (e.g., reuse lists) with perf justification.

Unsuitable:
* Generic workflow orchestration or dependency graph resolution.
* Built-in retry/backoff frameworks (leave to user signal implementations).
* Adding external reactive or task orchestration libraries for convenience.
* Embedding telemetry exporters / metrics (use external OTEL integration instead).
* UI/dashboard or runtime management tooling.

## Definition of Done (Authoritative)

A change is DONE when ALL of the following hold:
1. `dotnet build` passes with no new warnings.
2. `dotnet test` passes (no `--no-build`) including new tests.
3. Tests cover success, failure, timeout, cancellation, idempotency and edge cases (zero signals, single signal, mixed statuses).
4. Public APIs added/modified have XML docs and README updated if semantics changed.
5. Idempotency & classification semantics preserved (global vs. per-signal timeout interplay unchanged unless intentionally improved & documented).
6. No hidden global mutable state or nondeterministic classification logic introduced.
7. Performance not regressed (reasoning or microbenchmark for significant path changes).
8. Forbidden Patterns avoided.
9. Temporary test toggles/environment adjustments restored post-test.
10. Formatting & coding standards applied to all modified files (not just diff hunks).
11. Health check remains non-blocking and uses cached result.

## Safety & Invariant Enforcement

* Guard invalid option values early (negative timeouts, zero/negative concurrency) with clear exceptions or normalization.
* Use early-return guards for trivial cases (zero signals).
* Classify `OperationCanceledException` explicitly as timeout only when due to configured cancellations.
* Aggregate exceptions only under FailFast parallel semantics; sequential fail-fast throws immediately with single failure.
* Tests must assert failure and timeout paths, not just happy success.

## Final Guidance

Prefer small, incremental enhancements. Justify new policies/options with concrete scenarios. Keep coordinator logic explicit and readable; avoid over-engineering concurrency. Maintain deterministic, reproducible outcomes—timing only influences whether deadlines elapse, not ordering decisions. Preserve minimal dependency surface.

## Explicitly Forbidden Actions (Agent Conduct)

To protect repository integrity and ensure reproducibility, the assistant must not perform the following actions unless explicitly and narrowly authorized:

Git & Pull Requests:
* No git commands (add/commit/push/pull/merge/rebase/tag/branch/checkout/cherry-pick).
* No creating/modifying/rebasing/commenting/labeling/merging PRs or Issues via CLI/API.

Build/Test Shortcuts:
* Do not run `dotnet test` with `--no-build`.
* Do not claim builds/tests succeeded without executing them this session.

Tooling/File Edits:
* Use editor patch tools; avoid shell-based file mutations.
* Keep edits minimal and targeted; avoid unrelated mass reformatting.

Scope & Invariants:
* Do not introduce UI, AI heuristics, broad orchestration, or unrelated dependencies.
* Do not compromise determinism, idempotency, timeout semantics, or introduce hidden randomness.

These rules complement Forbidden Patterns and Coding Standards. Seek clarification when uncertain.

