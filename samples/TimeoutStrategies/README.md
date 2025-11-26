# Timeout Strategies Sample

This sample demonstrates the `IIgnitionTimeoutStrategy` plugin system by running the same startup scenario with different timeout strategies to show how they affect the outcome.

## What it demonstrates

- Creating custom `IIgnitionTimeoutStrategy` implementations
- Different timeout approaches for different use cases
- How the same signals produce different outcomes with different strategies
- Strategy registration via DI

## Timeout Strategies Included

### 1. Default Strategy (no custom strategy)
Uses each signal's own `Timeout` property. This is the backward-compatible behavior.

### 2. Lenient Strategy
Gives all signals a generous timeout (10 seconds) and doesn't cancel immediately. Useful for:
- Development environments
- Unpredictable network conditions
- Initial testing

### 3. Strict Strategy
Enforces a tight timeout (1 second) and cancels immediately on timeout. Useful for:
- Production environments
- Fast-fail scenarios
- Containerized deployments with health probes

### 4. Adaptive Strategy
Assigns different timeouts based on signal name patterns:
- Signals with "slow", "heavy", or "warmup" in their name get longer timeouts
- All other signals get short timeouts

### 5. Category Strategy
Uses signal name prefixes to determine timeouts:
- `db:*` signals: 5 seconds (database operations are slow)
- `cache:*` signals: 3 seconds (cache operations are medium)
- `svc:*` signals: 2 seconds (service checks should be fast)

## Signals Used

All scenarios use the same four signals:

| Signal | Duration | Purpose |
|--------|----------|---------|
| `db:connection` | 800ms | Database connection |
| `cache:slow-warmup` | 1500ms | Cache warming (intentionally slow) |
| `svc:health-check` | 300ms | Service health check (fast) |
| `svc:heavy-initialization` | 3000ms | Heavy initialization (very slow) |

## Running the sample

```bash
cd samples/TimeoutStrategies
dotnet run
```

## Expected Output

```
╔════════════════════════════════════════════════════════════════════════════╗
║             Ignition Timeout Strategies Sample                            ║
╚════════════════════════════════════════════════════════════════════════════╝

This sample demonstrates how different timeout strategies affect startup
outcomes when running the SAME set of signals.
...

┌─────────────────────────────────────────────────────────────────────────────┐
│ Scenario 1: DEFAULT Strategy (no custom strategy)                          │
│ Uses each signal's own Timeout property                                    │
└─────────────────────────────────────────────────────────────────────────────┘
   
   🔌 [db:connection] Connecting to database...
   ✅ [db:connection] Database connected (800ms)
   ...

   ╔══════════════════════════════════════════════════════════════════╗
   ║ RESULTS                                                          ║
   ╠══════════════════════════════════════════════════════════════════╣
   ║ Total Duration:     3000ms                                       ║
   ║ Timed Out:      NO                                               ║
   ╠══════════════════════════════════════════════════════════════════╣
   ║ ✅ Succeeded:    4/4                                             ║
   ╚══════════════════════════════════════════════════════════════════╝

...

┌─────────────────────────────────────────────────────────────────────────────┐
│ Scenario 3: STRICT Strategy                                                │
│ All signals get only 1 second timeout, immediate cancellation              │
└─────────────────────────────────────────────────────────────────────────────┘
   
   ...
   ❌ [svc:heavy-initialization] Cancelled during initialization

   ╔══════════════════════════════════════════════════════════════════╗
   ║ RESULTS                                                          ║
   ╠══════════════════════════════════════════════════════════════════╣
   ║ Total Duration:     1500ms                                       ║
   ║ Timed Out:      NO                                               ║
   ╠══════════════════════════════════════════════════════════════════╣
   ║ ✅ Succeeded:    2/4                                             ║
   ║ ⏰ Timed Out:    2                                               ║
   ╚══════════════════════════════════════════════════════════════════╝
```

## Key Concepts

### Creating a Custom Strategy

```csharp
public sealed class MyCustomStrategy : IIgnitionTimeoutStrategy
{
    public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(
        IIgnitionSignal signal, 
        IgnitionOptions options)
    {
        // Your custom logic here
        var timeout = DetermineTimeout(signal);
        return (timeout, cancelImmediately: true);
    }
}
```

### Registering the Strategy

```csharp
// Option 1: Instance
services.AddIgnitionTimeoutStrategy(new MyCustomStrategy());

// Option 2: Type (DI resolves dependencies)
services.AddIgnitionTimeoutStrategy<MyCustomStrategy>();

// Option 3: Factory
services.AddIgnitionTimeoutStrategy(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ConfigurableTimeoutStrategy(config);
});
```

### Return Values

- `signalTimeout`: The timeout duration for the signal, or `null` for no timeout
- `cancelImmediately`: When `true`, cancels the signal's task upon timeout; when `false`, just classifies as timed out

## Real-World Use Cases

1. **Environment-based**: Use strict timeouts in production, lenient in development
2. **Service-aware**: Database signals get more time than cache signals
3. **Retry-aware**: Increase timeout for signals that have failed before
4. **Load-aware**: Reduce timeouts during high-load periods
5. **Feature-flag**: Enable/disable timeout for specific signals dynamically
