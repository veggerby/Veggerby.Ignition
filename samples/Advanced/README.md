# Advanced Ignition Sample

This sample demonstrates advanced usage patterns and configuration options of the Veggerby.Ignition library.

## What it demonstrates

- Different execution policies (FailFast, BestEffort, ContinueOnTimeout)
- Sequential vs Parallel execution modes
- Concurrency limiting with MaxDegreeOfParallelism
- Global and per-signal timeout handling
- Cancellation behavior configuration
- Activity tracing enablement
- Complex failure scenarios and error handling

## Signals included

1. **CacheWarmupSignal** - Fast initialization (300ms, 2s timeout)
2. **DatabaseMigrationSignal** - Slower operation (2s, 8s timeout)
3. **ExternalServiceSignal** - May randomly fail (1.5s, 5s timeout)
4. **SlowServiceSignal** - Always times out (3s delay, 1s timeout)

## Scenarios

### Scenario 1: Parallel BestEffort

- **Policy**: BestEffort (tolerates failures)
- **Execution**: Parallel (all signals start simultaneously)
- **Behavior**: Continues even if some signals fail or timeout

### Scenario 2: Sequential FailFast

- **Policy**: FailFast (stops on first failure)
- **Execution**: Sequential (signals run one after another)
- **Behavior**: Stops immediately when a signal fails

### Scenario 3: Limited Concurrency with ContinueOnTimeout

- **Policy**: ContinueOnTimeout (ignores timeouts)
- **Execution**: Parallel with max 2 concurrent signals
- **Behavior**: Timeouts don't cause failures, but other exceptions do

## Running the sample

```bash
cd samples/Advanced
dotnet run
```

## Expected output

The application will run three scenarios sequentially, showing different behaviors:

```text
=== Advanced Ignition Sample ===

🔄 Scenario 1: Parallel BestEffort (tolerates failures)
Expected: All signals run in parallel, continues despite failures

info: Advanced.CacheWarmupSignal[0]
      Warming up cache...
info: Advanced.DatabaseMigrationSignal[0]
      Running database migrations...
info: Advanced.ExternalServiceSignal[0]
      Connecting to external service...
info: Advanced.SlowServiceSignal[0]
      Starting slow service initialization...
info: Advanced.CacheWarmupSignal[0]
      Cache warmup completed
warn: Advanced.SlowServiceSignal[0]
      Slow service initialization was cancelled
info: Advanced.ExternalServiceSignal[0]
      External service connected successfully
info: Advanced.DatabaseMigrationSignal[0]
      Database migrations completed

📊 Scenario 1 Results:
   Overall Success: False
   Total Duration: 2000ms
   Global Timeout: NO
   Policy Applied: BestEffort

📋 Individual Signal Results:
   ✅ cache-warmup: Succeeded (300ms)
   ✅ database-migration: Succeeded (2000ms)
   ✅ external-service: Succeeded (1500ms)
   ⏰ slow-service: TimedOut (1000ms)

... (scenarios 2 and 3 continue)
```

## Key concepts demonstrated

- **Multiple policies**: How different policies affect failure handling
- **Execution modes**: Parallel vs Sequential behavior differences
- **Timeout handling**: Global vs per-signal timeouts and cancellation
- **Concurrency control**: Limiting parallel execution with MaxDegreeOfParallelism
- **Error reporting**: Detailed status reporting and exception handling
- **Idempotency**: Multiple coordinator calls return cached results
