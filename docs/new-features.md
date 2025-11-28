# 🚀 Veggerby.Ignition — Proposed Major Feature Epics

## 1. **Dependency-Aware Execution Graph (DAG-based ignition)** ✅ IMPLEMENTED

~~Right now, everything is either **parallel** or **sequential**—simple and elegant. But real startup systems often have conditional readiness: DB before Cache, Cache before Worker, etc.~~

### What the epic delivers

* Introduce a lightweight DAG model describing *dependencies between signals*
* Support automatic topological sort
* Detect cycles with clear diagnostics
* Allow independent branches to run in parallel
* Surface structured dependency failures (showing failing subtrees)

### Why it’s big

* Requires new abstractions (e.g. `IIgnitionGraph`, `SignalDependencyAttribute`, fluent builder APIs)
* New coordinator scheduling paths
* New result surfaces for dependency-aware diagnostics
* Adds real expressive power while staying lightweight

### Why it’s useful

Startup readiness becomes (optionally) declarative instead of imperative — without forcing full workflow orchestration.

**Status**: ✅ **Fully Implemented**

#### Implementation Details

* **Core Abstractions**: `IIgnitionGraph`, `SignalDependencyAttribute`, `IgnitionGraphBuilder` (fluent API)
* **New Execution Mode**: `IgnitionExecutionMode.DependencyAware`  
* **Enhanced Results**: `IgnitionSignalStatus.Skipped` + `FailedDependencies` property
* **Algorithm**: In-house topological sort using Kahn's algorithm (zero external dependencies)
* **Backward Compatible**: All 33 existing tests pass without modification
* **Comprehensive Testing**: 19 new tests (52 total) covering topological sort, cycle detection, parallel execution, failure propagation, and edge cases

---

## 2. **Ignition Warmup "Stages" (Multi-Phase Startup Pipeline)** ✅ IMPLEMENTED

~~A middle ground between DAGs and pure parallel execution: staged batches.~~

### What the epic delivers

* The ability to register signals with a “stage”/“phase” number
* Coordinator executes Stage 0 → Stage 1 → Stage 2 …
* Within each stage: parallel execution
* Cross-stage constraints: next stage starts only when previous stage meets policy thresholds
* Optional early promotion: run next stage when X% of previous stage succeeded
* Rich reporting of stage timing

### Why it’s big

* New internal scheduling algorithm
* More complex result representation
* Configurable stage fail-fast/best-effort/passthrough behaviors
* Needs careful determinism guarantees (your jam)

**Status**: ✅ **Fully Implemented**

#### Implementation Details

* **Core Abstractions**: `IStagedIgnitionSignal`, `IgnitionStagePolicy`, `IgnitionStageResult`
* **New Execution Mode**: `IgnitionExecutionMode.Staged`
* **Stage Policies**: `AllMustSucceed`, `BestEffort`, `FailFast`, `EarlyPromotion`
* **Options**: `IgnitionOptions.StagePolicy`, `IgnitionOptions.EarlyPromotionThreshold`
* **Enhanced Results**: `IgnitionResult.StageResults` with per-stage timing and outcome data
* **DI Extensions**: `AddIgnitionSignalWithStage`, `AddIgnitionFromTaskWithStage`
* **Algorithm**: Sequential stage execution with parallel signals within each stage
* **Backward Compatible**: All existing tests pass without modification
* **Comprehensive Testing**: 18 new tests (153 total) covering stage execution, policies, early promotion, and edge cases

---

## 3. **Composable Ignition Bundles / Modules** ✅ IMPLEMENTED

~~Allow reusable, packaged sets of signals—without forcing users to manually add 10 related signals individually.~~

### What the epic delivers

* New abstraction: `IIgnitionBundle`
* Bundles register a graph or set of signals + default options
* Optional per-bundle timeouts or policies
* Ability to override bundle internals without forking
* Built-in bundles for common patterns (e.g. "HTTP dependency", "database trio", etc.)

### Why it's big

* Requires a bundle loader layer
* New DI scanning paths
* New override semantics
* Balancing simplicity vs flexibility

### Why it's powerful

Allows users to build reusable ecosystem modules like:

* RedisStarterBundle
* KafkaConsumerBundle
* "Search Infrastructure Warmup Bundle"

…but without making the library heavyweight.

**Status**: ✅ **Fully Implemented**

#### Implementation Details

* **Core Abstractions**: `IIgnitionBundle`, `IgnitionBundleOptions`
* **Extension Methods**: `AddIgnitionBundle`, `AddIgnitionBundle<T>`, `AddIgnitionBundles`
* **Built-in Bundles**: `HttpDependencyBundle`, `DatabaseTrioBundle`
* **Per-Bundle Configuration**: Timeout and policy overrides via `IgnitionBundleOptions`
* **Backward Compatible**: All 52 existing tests pass without modification
* **Comprehensive Testing**: 16 new tests (68 total) covering bundle registration, options, built-in bundles, and integration
* **Zero Dependencies**: Maintains library's lightweight philosophy

---

## 4. **Ignition State Machine with Event Hooks** ✅ IMPLEMENTED

Move from “run once and store result” → to a minimal finite-state model.

### What the epic delivers

* States: `NotStarted → Running → Completed → Failed → TimedOut`
* Coordinator exposes events:

  * `OnSignalStarted`
  * `OnSignalCompleted`
  * `OnGlobalTimeout`
  * `OnCoordinatorCompleted`
* Allows safe external observers (logging, dashboards, or… AETHER 😈)

### Why it’s big

* Requires architectural redo of coordinator internal flow
* Needs thread-safe event publication
* Needs strong guarantees around idempotency
* MUST avoid breaking existing behavior — tricky

### Why it matters

Great for systems that want progress bars, instrumentation, or live observability.

Trump wouldn’t understand it, but real engineers will.

**Status**: ✅ **Fully Implemented**

#### Implementation Details

* **Core Abstractions**: `IgnitionState` enum, `IgnitionSignalStartedEventArgs`, `IgnitionSignalCompletedEventArgs`, `IgnitionGlobalTimeoutEventArgs`, `IgnitionCoordinatorCompletedEventArgs`
* **State Property**: `IIgnitionCoordinator.State` property for checking current lifecycle state
* **Event Hooks**: Four events on `IIgnitionCoordinator` for real-time monitoring
* **Thread Safety**: Events are raised via delegate capture pattern with exception handling
* **Backward Compatible**: All 84 existing tests pass without modification
* **Comprehensive Testing**: 28 new tests (112 total) covering state transitions, event hooks, idempotency, and exception safety

---

## 5. **Ignition Replay + Historical Recordings**

Ability to record ignition runs and replay them for diagnostics/testing.

### What the epic delivers

* Record:

  * timing
  * dependencies
  * failures
  * durations
  * sequence ordering
* Serialize to a lightweight JSON record
* Provide `IgnitionReplayer` that:

  * validates invariants (unexpected timing drift, inconsistent rescheduling)
  * simulates “what if this one timed out earlier”
  * tests stage dependency correctness

### Why it’s big

* Needs a structured, stable schema
* Requires storing duration histograms or per-run metrics
* Replayer needs deterministic playback logic
* Integration with existing coordinator requires a non-invasive injection mechanism

### Why it’s useful

Perfect for diagnosing slow startup in prod vs dev, CI regression detection, or offline simulation.

---

## 6. **Ignition Metrics Adapter (Zero-Dependency, Pluggable Metrics)**

A structured internal metrics API that integrates with:

* OpenTelemetry
* Prometheus
* App Metrics
  …but without adding *any* of them as dependencies.

### What the epic delivers

* Introduce minimal metrics abstraction:

  ```csharp
  public interface IIgnitionMetrics
  {
      void RecordSignalDuration(string name, TimeSpan duration);
      void RecordSignalStatus(string name, IgnitionSignalStatus status);
      void RecordTotalDuration(TimeSpan duration);
  }
  ```

* Users can plug in their own backend
* Provide no-op default implementation
* Add option to enable metrics recording

### Why it’s big

* Affects all hot paths
* Requires careful design to avoid adding allocations
* Must preserve “no external deps” mission

### Why it’s great

It keeps Ignition small but makes it observability-friendly.

---

## 7. **Cancellation Propagation Rework (Structured Cancellation Trees)** ✅ IMPLEMENTED

~~Right now cancellation is fairly flat: global vs per-signal. Consider a more expressive model.~~

### What the epic delivers

* Cancellation tokens become a tree, where bundled signals inherit cancellation scopes
* Supports grouped cancellation semantics:

  * cancel a whole stage
  * cancel all signals dependent on a failed signal
  * cancel all signals sharing a bundle
* Provide accurate reporting: "Signal X cancelled due to group cancellation triggered by Y"

### Why it's big

* Introduces new hierarchical model
* Requires updates to DI registration
* Needs updates to result aggregation
* Must not break deterministic guarantees
* Test matrix explodes

**Status**: ✅ **Fully Implemented**

#### Implementation Details

* **Core Abstractions**: `ICancellationScope`, `CancellationScope`, `IScopedIgnitionSignal`, `CancellationReason` enum
* **New Signal Status**: `IgnitionSignalStatus.Cancelled` for hierarchical cancellation scenarios
* **Enhanced Results**: `IgnitionSignalResult` extended with `CancellationReason` and `CancelledBySignal` properties
* **Options**: `IgnitionOptions.CancelDependentsOnFailure` for dependency-aware cancellation propagation
* **Bundle Support**: `IgnitionBundleOptions.EnableScopedCancellation` and `CancellationScope` properties
* **DI Extensions**: `AddIgnitionCancellationScope`, `AddIgnitionSignalWithScope`, `AddIgnitionFromTaskWithScope`
* **Backward Compatible**: All existing tests pass without modification
* **Comprehensive Testing**: 19 new tests covering scope hierarchies, cancellation propagation, DI registration, and result classification

---

## 8. **Structured Startup Timeline Export (Gantt-like Output)**

Export a time-aligned sequence of startup events.

### What the epic delivers

* Produce JSON timeline of:

  * signal start
  * signal end
  * durations
  * dependency ordering
  * concurrent groups
  * global timeout boundaries
* Provide extension:

  ```csharp
  var timeline = result.ExportTimeline();
  ```

* Ship a small optional CLI or HTML viewer (still lightweight if opt-in)

### Why it's big

* Requires internal timestamp capture, not just duration
* Needs stable schema
* Coordinator must publish start-time metadata
* Visualization support (even if barebones) is non-trivial

### Why users love it

This is amazing for startup debugging, profiling, container warmup analysis, or CI timing regression detection.

---

## 9. **Timeout Strategy Plugins** ✅ IMPLEMENTED

Timeout semantics today are “global” vs “per-signal”. Add pluggable strategy modules.

### What the epic delivers

Define:

```csharp
public interface IIgnitionTimeoutStrategy
{
    (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options);
}
```

Support custom strategies:

* exponential scaling based on failure count
* adaptive timeouts (e.g. slow I/O detection)
* dynamic per-stage deadlines
* user-defined per-class/per-assembly defaults

### Why it’s big

* A whole new strategy interface, new registration/DI model
* Coordinator logic refactor
* Options model must support fallback / override
* Must remain deterministic and predictable

### Why it’s powerful

Makes Ignition adaptable to real-world startup complexities—while still tiny.

**Status**: ✅ **Fully Implemented**

#### Implementation Details

* **Core Abstractions**: `IIgnitionTimeoutStrategy`, `DefaultIgnitionTimeoutStrategy`
* **Options Integration**: `IgnitionOptions.TimeoutStrategy` property for custom strategy configuration
* **DI Registration**: `AddIgnitionTimeoutStrategy`, `AddIgnitionTimeoutStrategy<T>`, and factory overloads
* **Backward Compatible**: Default behavior preserved when no strategy is configured
* **Comprehensive Testing**: 16 new tests covering strategy behavior, DI registration, and cancellation control

---

# Summary Table

| Epic                         | Value        | Complexity | Lightweight-friendly | Status               |
| ---------------------------- | ------------ | ---------- | -------------------- | -------------------- |
| DAG-based execution          | 🔥 Very high | 🔥🔥🔥     | ✔                    | ✅ **IMPLEMENTED**  |
| Staged execution             | High         | 🔥🔥       | ✔                    | ✅ **IMPLEMENTED**  |
| Bundles/modules              | Medium-high  | 🔥🔥       | ✔                    | ✅ **IMPLEMENTED**  |
| Event-based state machine    | High         | 🔥🔥🔥     | ✔                    | ✅ **IMPLEMENTED**  |
| Replay & historical analysis | High         | 🔥🔥🔥     | ✔                    | 📋 Proposed         |
| Metrics adapter              | Medium       | 🔥         | ✔                    | 📋 Proposed         |
| Cancellation trees           | High         | 🔥🔥🔥     | ✔                    | ✅ **IMPLEMENTED**  |
| Timeline exporter            | High         | 🔥🔥       | ✔                    | 📋 Proposed         |
| Timeout strategy plugins     | Medium-high  | 🔥🔥       | ✔                    | ✅ **IMPLEMENTED**  |

---

# If I had to pick **3 headline epics**

If Veggerby.Ignition were to *level up* without becoming bloated, the most impactful additions are:

1. **Dependency-aware DAG execution** ✅
2. **Composable bundles/modules** ✅
3. **Timeout strategy plugins** ✅
4. **Staged (multi-phase) ignition pipeline** ✅
5. **Ignition state machine + event hooks** ✅

These add massive expressive power while preserving your clean architectural DNA—and unlike Elon’s product launches, they’ll actually work.
