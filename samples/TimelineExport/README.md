# Timeline Export Sample

This sample demonstrates the **Timeline Export** feature for analyzing and visualizing ignition startup timing.

## What This Sample Shows

1. **Parallel Execution Timeline** - Shows how concurrent signal execution appears in a Gantt-like visualization
2. **Sequential Execution Timeline** - Shows signals executing one after another
3. **Timeout Scenarios** - Shows how timeouts appear in the timeline

## Running the Sample

```bash
cd samples/TimelineExport
dotnet run
```

## Features Demonstrated

### Console Visualization
The `timeline.WriteToConsole()` method provides a Gantt-like ASCII visualization:
- Visual bar chart of signal execution timing
- Status indicators (✅ Succeeded, ❌ Failed, ⏰ TimedOut)
- Summary statistics (slowest/fastest signals, concurrency)

### JSON Export
The `timeline.ToJson()` method exports structured JSON data:
- Schema version for forward compatibility
- Per-signal start/end times relative to ignition start
- Concurrent group identification
- Summary statistics

## Use Cases

- **Debugging**: Identify which signals are slow or causing bottlenecks
- **Profiling**: Measure container warmup times
- **CI Regression Detection**: Compare JSON exports between builds
- **Visualization**: Export to external tools (Chrome DevTools, Perfetto)

## Sample Output

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                    IGNITION TIMELINE                                         ║
╠══════════════════════════════════════════════════════════════════════════════╣
║ Total Duration:     1250.5ms                                               ║
║ Timed Out:      NO                                                         ║
║ Execution Mode: Parallel                                                   ║
╠══════════════════════════════════════════════════════════════════════════════╣
║                                                                              ║
║ SIGNAL TIMELINE (Gantt View)                                                 ║
║                                                                              ║
║ ✅ database-connection  [████████████████                                  ]  800ms ║
║ ✅ cache-warmup         [████████████████████████                          ] 1200ms ║
║ ✅ configuration-load   [████████                                          ]  400ms ║
║ ✅ external-service     [████████████                                      ]  600ms ║
╠══════════════════════════════════════════════════════════════════════════════╣
║ SUMMARY                                                                      ║
║   Total Signals:        4                                                    ║
║   ✅ Succeeded:         4                                                    ║
║   Max Concurrency:      4                                                    ║
║   🐢 Slowest:       cache-warmup (1200ms)                                   ║
║   🚀 Fastest:       configuration-load (400ms)                              ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

## Related Documentation

- [Timeline Export Documentation](../../docs/observability.md)
- [Features Overview](../../docs/features.md)
