using System;
using System.Collections.Generic;
using System.Linq;

namespace Veggerby.Ignition;

/// <summary>
/// Extension methods for exporting ignition results to structured timeline format.
/// </summary>
public static class IgnitionTimelineExtensions
{
    /// <summary>
    /// Exports the ignition result to a structured timeline format for analysis and visualization.
    /// </summary>
    /// <param name="result">The ignition result to export.</param>
    /// <param name="executionMode">Optional execution mode description to include in the timeline metadata.</param>
    /// <param name="globalTimeout">Optional global timeout value to include as a boundary marker.</param>
    /// <param name="startedAt">Optional timestamp when ignition started (ISO 8601 format).</param>
    /// <param name="completedAt">Optional timestamp when ignition completed (ISO 8601 format).</param>
    /// <returns>An <see cref="IgnitionTimeline"/> representing the startup events.</returns>
    /// <remarks>
    /// <para>
    /// The timeline includes:
    /// <list type="bullet">
    ///   <item>Per-signal events with start/end times, status, and duration</item>
    ///   <item>Concurrent group identification (signals with overlapping execution)</item>
    ///   <item>Stage information when using staged execution mode</item>
    ///   <item>Boundary markers for global timeout</item>
    ///   <item>Summary statistics including slowest/fastest signals and concurrency</item>
    /// </list>
    /// </para>
    /// <para>
    /// All time values are relative to the start of ignition (time zero).
    /// The timeline is designed for export to JSON format using <see cref="IgnitionTimeline.ToJson"/>.
    /// </para>
    /// </remarks>
    public static IgnitionTimeline ExportTimeline(
        this IgnitionResult result,
        string? executionMode = null,
        TimeSpan? globalTimeout = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var events = BuildTimelineEvents(result.Results);
        var boundaries = BuildBoundaries(globalTimeout, result.TotalDuration, result.TimedOut);
        var stages = BuildStageTimelines(result.StageResults);
        var summary = BuildSummary(result.Results, events);

        return new IgnitionTimeline
        {
            TotalDurationMs = result.TotalDuration.TotalMilliseconds,
            TimedOut = result.TimedOut,
            ExecutionMode = executionMode,
            GlobalTimeoutMs = globalTimeout?.TotalMilliseconds,
            StartedAt = startedAt?.ToString("O"),
            CompletedAt = completedAt?.ToString("O"),
            Events = events,
            Boundaries = boundaries,
            Stages = stages,
            Summary = summary
        };
    }

    /// <summary>
    /// Exports the ignition result to a JSON string for analysis and visualization.
    /// </summary>
    /// <param name="result">The ignition result to export.</param>
    /// <param name="indented">Whether to format the JSON with indentation for readability (default: true).</param>
    /// <param name="executionMode">Optional execution mode description to include in the timeline metadata.</param>
    /// <param name="globalTimeout">Optional global timeout value to include as a boundary marker.</param>
    /// <returns>A JSON string representation of the timeline.</returns>
    /// <remarks>
    /// This is a convenience method combining <see cref="ExportTimeline"/> and <see cref="IgnitionTimeline.ToJson"/>.
    /// </remarks>
    public static string ExportTimelineJson(
        this IgnitionResult result,
        bool indented = true,
        string? executionMode = null,
        TimeSpan? globalTimeout = null)
    {
        return result.ExportTimeline(executionMode, globalTimeout).ToJson(indented);
    }

    private static List<IgnitionTimelineEvent> BuildTimelineEvents(IReadOnlyList<IgnitionSignalResult> results)
    {
        var events = new List<IgnitionTimelineEvent>(results.Count);

        // Track concurrent groups by identifying overlapping execution windows
        var groups = IdentifyConcurrentGroups(results);

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];

            double startMs = r.StartedAt?.TotalMilliseconds ?? 0;
            double endMs = r.CompletedAt?.TotalMilliseconds ?? (startMs + r.Duration.TotalMilliseconds);
            double durationMs = r.Duration.TotalMilliseconds;

            // Get stage if present
            int? stage = null;
            // Note: We don't have direct stage info per signal in the result,
            // but we can infer from StageResults if the caller provides that context

            events.Add(new IgnitionTimelineEvent(
                SignalName: r.Name,
                Status: r.Status.ToString(),
                StartMs: startMs,
                EndMs: endMs,
                DurationMs: durationMs,
                Stage: stage,
                Dependencies: null, // Could be enhanced with graph info
                FailedDependencies: r.FailedDependencies,
                ConcurrentGroup: groups.TryGetValue(i, out var group) ? group : null));
        }

        return events;
    }

    private static Dictionary<int, int> IdentifyConcurrentGroups(IReadOnlyList<IgnitionSignalResult> results)
    {
        var groups = new Dictionary<int, int>();

        if (results.Count == 0)
        {
            return groups;
        }

        // Simple concurrent group identification based on overlapping time windows
        // Signals with overlapping [start, end] intervals are in the same concurrent group
        var signalWindows = new List<(int index, double start, double end)>();

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            double start = r.StartedAt?.TotalMilliseconds ?? 0;
            double end = r.CompletedAt?.TotalMilliseconds ?? (start + r.Duration.TotalMilliseconds);
            signalWindows.Add((i, start, end));
        }

        // Sort by start time
        signalWindows.Sort((a, b) => a.start.CompareTo(b.start));

        int currentGroup = 0;
        double groupEndTime = double.MinValue;

        foreach (var (index, start, end) in signalWindows)
        {
            if (start < groupEndTime)
            {
                // Overlaps with current group
                groups[index] = currentGroup;
                groupEndTime = Math.Max(groupEndTime, end);
            }
            else
            {
                // New group starts - no overlap with previous
                currentGroup++;
                groups[index] = currentGroup;
                groupEndTime = end;
            }
        }

        return groups;
    }

    private static List<IgnitionTimelineBoundary> BuildBoundaries(TimeSpan? globalTimeout, TimeSpan totalDuration, bool timedOut)
    {
        var boundaries = new List<IgnitionTimelineBoundary>();

        if (globalTimeout.HasValue)
        {
            boundaries.Add(new IgnitionTimelineBoundary(
                Type: "GlobalTimeoutConfigured",
                TimeMs: globalTimeout.Value.TotalMilliseconds,
                Details: timedOut ? "Timeout was reached" : "Timeout was not reached"));
        }

        boundaries.Add(new IgnitionTimelineBoundary(
            Type: "IgnitionComplete",
            TimeMs: totalDuration.TotalMilliseconds,
            Details: timedOut ? "Completed with timeout" : "Completed successfully"));

        return boundaries;
    }

    private static IReadOnlyList<IgnitionTimelineStage>? BuildStageTimelines(IReadOnlyList<IgnitionStageResult>? stageResults)
    {
        if (stageResults is null || stageResults.Count == 0)
        {
            return null;
        }

        var stages = new List<IgnitionTimelineStage>(stageResults.Count);
        double cumulativeStartMs = 0;

        foreach (var stage in stageResults)
        {
            double durationMs = stage.Duration.TotalMilliseconds;
            double endMs = cumulativeStartMs + durationMs;

            stages.Add(new IgnitionTimelineStage(
                StageNumber: stage.StageNumber,
                StartMs: cumulativeStartMs,
                EndMs: endMs,
                DurationMs: durationMs,
                SignalCount: stage.TotalSignals,
                SucceededCount: stage.SucceededCount,
                FailedCount: stage.FailedCount,
                TimedOutCount: stage.TimedOutCount,
                EarlyPromoted: stage.Promoted));

            cumulativeStartMs = endMs;
        }

        return stages;
    }

    private static IgnitionTimelineSummary BuildSummary(
        IReadOnlyList<IgnitionSignalResult> results,
        IReadOnlyList<IgnitionTimelineEvent> events)
    {
        int total = results.Count;
        int succeeded = 0;
        int failed = 0;
        int timedOut = 0;
        int skipped = 0;
        int cancelled = 0;

        string? slowestSignal = null;
        double slowestDurationMs = 0;
        string? fastestSignal = null;
        double fastestDurationMs = double.MaxValue;
        double executedTotalDurationMs = 0;
        int executedCount = 0;

        foreach (var r in results)
        {
            switch (r.Status)
            {
                case IgnitionSignalStatus.Succeeded:
                    succeeded++;
                    break;
                case IgnitionSignalStatus.Failed:
                    failed++;
                    break;
                case IgnitionSignalStatus.TimedOut:
                    timedOut++;
                    break;
                case IgnitionSignalStatus.Skipped:
                    skipped++;
                    break;
                case IgnitionSignalStatus.Cancelled:
                    cancelled++;
                    break;
            }

            double durationMs = r.Duration.TotalMilliseconds;

            // Track durations for executed signals only (exclude skipped/cancelled)
            bool isExecuted = r.Status != IgnitionSignalStatus.Skipped &&
                              r.Status != IgnitionSignalStatus.Cancelled;

            if (isExecuted)
            {
                executedTotalDurationMs += durationMs;
                executedCount++;

                if (durationMs > slowestDurationMs)
                {
                    slowestDurationMs = durationMs;
                    slowestSignal = r.Name;
                }

                if (durationMs < fastestDurationMs)
                {
                    fastestDurationMs = durationMs;
                    fastestSignal = r.Name;
                }
            }
        }

        // Calculate max concurrency from events
        int maxConcurrency = CalculateMaxConcurrency(events);

        double? averageDurationMs = executedCount > 0 ? executedTotalDurationMs / executedCount : null;

        // Handle edge cases - use comparison with tolerance for floating point
        double? actualFastestDuration = null;
        if (executedCount > 0 && fastestDurationMs < double.MaxValue)
        {
            actualFastestDuration = fastestDurationMs;
        }
        else
        {
            fastestSignal = null;
        }

        return new IgnitionTimelineSummary(
            TotalSignals: total,
            SucceededCount: succeeded,
            FailedCount: failed,
            TimedOutCount: timedOut,
            SkippedCount: skipped,
            CancelledCount: cancelled,
            MaxConcurrency: maxConcurrency,
            SlowestSignal: slowestSignal,
            SlowestDurationMs: executedCount > 0 ? slowestDurationMs : null,
            FastestSignal: fastestSignal,
            FastestDurationMs: actualFastestDuration,
            AverageDurationMs: averageDurationMs);
    }

    private static int CalculateMaxConcurrency(IReadOnlyList<IgnitionTimelineEvent> events)
    {
        if (events.Count == 0)
        {
            return 0;
        }

        // Create a list of time points with +1 for start and -1 for end
        var timePoints = new List<(double time, int delta)>();

        foreach (var e in events)
        {
            timePoints.Add((e.StartMs, 1));  // Signal starts
            timePoints.Add((e.EndMs, -1));   // Signal ends
        }

        // Sort by time, with ends before starts at same time to correctly count concurrency
        // When a signal ends and another starts at the exact same time, we process the end first
        // to avoid counting them as concurrent when they're actually sequential
        timePoints.Sort((a, b) =>
        {
            int cmp = a.time.CompareTo(b.time);
            if (cmp != 0)
            {
                return cmp;
            }
            // At same time, process ends (-1) before starts (+1): -1 < +1
            return a.delta.CompareTo(b.delta);
        });

        int current = 0;
        int max = 0;

        foreach (var (_, delta) in timePoints)
        {
            current += delta;
            if (current > max)
            {
                max = current;
            }
        }

        return max;
    }

    /// <summary>
    /// Formats the timeline as a console-friendly Gantt-like visualization.
    /// </summary>
    /// <param name="timeline">The timeline to format.</param>
    /// <param name="width">The width of the Gantt bar section (default: 50 characters).</param>
    /// <returns>A string containing the formatted timeline visualization.</returns>
    /// <remarks>
    /// The output includes:
    /// <list type="bullet">
    ///   <item>Header with timeline metadata (duration, timeout status, execution mode)</item>
    ///   <item>A Gantt-like bar chart showing signal execution timing</item>
    ///   <item>Summary statistics (slowest/fastest signals, concurrency)</item>
    /// </list>
    /// </remarks>
    public static string ToConsoleString(this IgnitionTimeline timeline, int width = 50)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        var sb = new System.Text.StringBuilder();
        var totalMs = timeline.TotalDurationMs;
        var hasDuration = totalMs > 0;

        // Header
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                    IGNITION TIMELINE                                         ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║ Total Duration: {totalMs,10:F1}ms                                               ║");
        sb.AppendLine($"║ Timed Out:      {(timeline.TimedOut ? "YES" : "NO "),-10}                                               ║");
        if (timeline.ExecutionMode != null)
        {
            sb.AppendLine($"║ Execution Mode: {timeline.ExecutionMode,-10}                                               ║");
        }
        if (timeline.GlobalTimeoutMs.HasValue)
        {
            sb.AppendLine($"║ Global Timeout: {timeline.GlobalTimeoutMs.Value,10:F1}ms                                               ║");
        }
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");

        // Timeline header
        sb.AppendLine("║                                                                              ║");
        sb.AppendLine("║ SIGNAL TIMELINE (Gantt View)                                                 ║");
        sb.AppendLine("║                                                                              ║");

        // Scale indicator
        var scaleWidth = width;
        var scaleStep = totalMs / 5;
        sb.Append("║    ");
        sb.Append("0".PadRight(scaleWidth / 5));
        for (int i = 1; i < 5; i++)
        {
            sb.Append($"{(scaleStep * i):F0}".PadRight(scaleWidth / 5));
        }
        sb.AppendLine($"{totalMs:F0}ms".PadLeft(10) + "      ║");

        sb.Append("║    ");
        sb.Append('|');
        for (int i = 1; i < 5; i++)
        {
            sb.Append(new string('-', scaleWidth / 5 - 1) + '|');
        }
        sb.AppendLine(new string('-', scaleWidth / 5 - 1) + "|      ║");

        // Events sorted by start time
        var sortedEvents = timeline.Events
            .OrderBy(e => e.StartMs)
            .ThenBy(e => e.SignalName)
            .ToList();

        foreach (var e in sortedEvents)
        {
            var statusIcon = e.Status switch
            {
                "Succeeded" => "✅",
                "Failed" => "❌",
                "TimedOut" => "⏰",
                "Skipped" => "⏭️",
                "Cancelled" => "🚫",
                _ => "❓"
            };

            // Calculate bar position (use cached totalMs check)
            var startPos = hasDuration ? (int)((e.StartMs / totalMs) * scaleWidth) : 0;
            var endPos = hasDuration ? (int)((e.EndMs / totalMs) * scaleWidth) : 0;
            var barLength = Math.Max(1, endPos - startPos);

            // Build the bar
            var bar = new string(' ', startPos) + new string('█', barLength) + new string(' ', Math.Max(0, scaleWidth - startPos - barLength));

            var signalName = TruncateSignalName(e.SignalName, 20);
            sb.AppendLine($"║ {statusIcon} {signalName} [{bar}] {e.DurationMs,7:F0}ms ║");
        }

        sb.AppendLine("║                                                                              ║");
        sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════╣");

        // Summary section
        sb.AppendLine("║ SUMMARY                                                                      ║");
        sb.AppendLine("║                                                                              ║");

        if (timeline.Summary != null)
        {
            var s = timeline.Summary;
            sb.AppendLine($"║   Total Signals:    {s.TotalSignals,5}                                                  ║");
            sb.AppendLine($"║   ✅ Succeeded:     {s.SucceededCount,5}                                                  ║");
            if (s.FailedCount > 0)
            {
                sb.AppendLine($"║   ❌ Failed:        {s.FailedCount,5}                                                  ║");
            }
            if (s.TimedOutCount > 0)
            {
                sb.AppendLine($"║   ⏰ Timed Out:     {s.TimedOutCount,5}                                                  ║");
            }
            if (s.SkippedCount > 0)
            {
                sb.AppendLine($"║   ⏭️  Skipped:       {s.SkippedCount,5}                                                  ║");
            }
            if (s.CancelledCount > 0)
            {
                sb.AppendLine($"║   🚫 Cancelled:     {s.CancelledCount,5}                                                  ║");
            }
            sb.AppendLine($"║   Max Concurrency:  {s.MaxConcurrency,5}                                                  ║");

            if (s.SlowestSignal != null)
            {
                var slowestName = TruncateSignalName(s.SlowestSignal, 20);
                sb.AppendLine($"║   🐢 Slowest:       {slowestName,-20} ({s.SlowestDurationMs:F0}ms)                ║");
            }
            if (s.FastestSignal != null)
            {
                var fastestName = TruncateSignalName(s.FastestSignal, 20);
                sb.AppendLine($"║   🚀 Fastest:       {fastestName,-20} ({s.FastestDurationMs:F0}ms)                 ║");
            }
            if (s.AverageDurationMs.HasValue)
            {
                sb.AppendLine($"║   📊 Avg Duration:  {s.AverageDurationMs.Value,10:F1}ms                                       ║");
            }
        }

        sb.AppendLine("║                                                                              ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    /// <summary>
    /// Writes the timeline to the console as a Gantt-like visualization.
    /// </summary>
    /// <param name="timeline">The timeline to display.</param>
    /// <param name="width">The width of the Gantt bar section (default: 50 characters).</param>
    public static void WriteToConsole(this IgnitionTimeline timeline, int width = 50)
    {
        Console.WriteLine(timeline.ToConsoleString(width));
    }

    /// <summary>
    /// Truncates a signal name to fit within the specified width.
    /// </summary>
    private static string TruncateSignalName(string name, int maxLength)
    {
        if (name.Length > maxLength)
        {
            return name[..(maxLength - 3)] + "...";
        }
        return name.PadRight(maxLength);
    }
}
