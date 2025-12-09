using System.Collections.Generic;
using System.Linq;

namespace Veggerby.Ignition.Diagnostics;

/// <summary>
/// Extension methods for creating ignition recordings from results.
/// </summary>
public static class IgnitionRecordingExtensions
{
    /// <summary>
    /// Exports the ignition result to a recording format suitable for replay and analysis.
    /// </summary>
    /// <param name="result">The ignition result to export.</param>
    /// <param name="options">Optional ignition options to capture configuration snapshot.</param>
    /// <param name="graph">Optional dependency graph to capture dependency information.</param>
    /// <param name="finalState">Optional final state of the coordinator.</param>
    /// <param name="metadata">Optional metadata to include in the recording.</param>
    /// <returns>An <see cref="IgnitionRecording"/> capturing the ignition run.</returns>
    /// <remarks>
    /// <para>
    /// The recording includes:
    /// <list type="bullet">
    ///   <item>Per-signal timing, status, and configuration</item>
    ///   <item>Dependency information if using dependency-aware execution</item>
    ///   <item>Stage information if using staged execution</item>
    ///   <item>Configuration snapshot for replay validation</item>
    ///   <item>Summary statistics</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IgnitionRecording ExportRecording(
        this IgnitionResult result,
        IgnitionOptions? options = null,
        IIgnitionGraph? graph = null,
        IgnitionState? finalState = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var signals = BuildRecordedSignals(result, graph);
        var stages = BuildRecordedStages(result);
        var summary = BuildRecordingSummary(result);
        var configuration = options is null ? null : BuildConfiguration(options);

        return new IgnitionRecording
        {
            RecordedAt = DateTimeOffset.UtcNow.ToString("O"),
            TotalDurationMs = result.TotalDuration.TotalMilliseconds,
            TimedOut = result.TimedOut,
            FinalState = finalState?.ToString(),
            Configuration = configuration,
            Signals = signals,
            Stages = stages,
            Summary = summary,
            Metadata = metadata
        };
    }

    /// <summary>
    /// Exports the ignition result to a recording JSON string.
    /// </summary>
    /// <param name="result">The ignition result to export.</param>
    /// <param name="options">Optional ignition options to capture configuration snapshot.</param>
    /// <param name="graph">Optional dependency graph to capture dependency information.</param>
    /// <param name="finalState">Optional final state of the coordinator.</param>
    /// <param name="metadata">Optional metadata to include in the recording.</param>
    /// <param name="indented">Whether to format the JSON with indentation.</param>
    /// <returns>A JSON string representation of the recording.</returns>
    public static string ExportRecordingJson(
        this IgnitionResult result,
        IgnitionOptions? options = null,
        IIgnitionGraph? graph = null,
        IgnitionState? finalState = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool indented = true)
    {
        return result.ExportRecording(options, graph, finalState, metadata).ToJson(indented);
    }

    /// <summary>
    /// Creates a replayer from an ignition result.
    /// </summary>
    /// <param name="result">The ignition result to create a replayer for.</param>
    /// <param name="options">Optional ignition options to capture configuration snapshot.</param>
    /// <param name="graph">Optional dependency graph to capture dependency information.</param>
    /// <returns>An <see cref="IgnitionReplayer"/> for analyzing the result.</returns>
    public static IgnitionReplayer ToReplayer(
        this IgnitionResult result,
        IgnitionOptions? options = null,
        IIgnitionGraph? graph = null)
    {
        var recording = result.ExportRecording(options, graph);
        return new IgnitionReplayer(recording);
    }

    private static List<IgnitionRecordedSignal> BuildRecordedSignals(IgnitionResult result, IIgnitionGraph? graph)
    {
        var signals = new List<IgnitionRecordedSignal>(result.Results.Count);
        var dependenciesBySignal = BuildDependenciesLookup(graph);

        foreach (var r in result.Results)
        {
            double startMs = r.StartedAt?.TotalMilliseconds ?? 0;
            double endMs = r.CompletedAt?.TotalMilliseconds ?? (startMs + r.Duration.TotalMilliseconds);
            double durationMs = r.Duration.TotalMilliseconds;

            // Get stage from staged signal if present
            int? stage = null;
            // Try to get stage from stage results if available
            if (result.StageResults is not null)
            {
                var matchingStageResult = result.StageResults
                    .FirstOrDefault(stageResult => stageResult.Results.Any(sr => sr.Name == r.Name));
                if (matchingStageResult is not null)
                {
                    stage = matchingStageResult.StageNumber;
                }
            }

            signals.Add(new IgnitionRecordedSignal(
                SignalName: r.Name,
                Status: r.Status.ToString(),
                StartMs: startMs,
                EndMs: endMs,
                DurationMs: durationMs,
                Stage: stage,
                Dependencies: dependenciesBySignal.GetValueOrDefault(r.Name),
                FailedDependencies: r.FailedDependencies,
                CancellationReason: r.CancellationReason != CancellationReason.None ? r.CancellationReason.ToString() : null,
                CancelledBySignal: r.CancelledBySignal,
                ExceptionType: r.Exception?.GetType().FullName,
                ExceptionMessage: r.Exception?.Message));
        }

        return signals;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildDependenciesLookup(IIgnitionGraph? graph)
    {
        var lookup = new Dictionary<string, IReadOnlyList<string>>();

        if (graph is null)
        {
            return lookup;
        }

        foreach (var signal in graph.Signals)
        {
            var deps = graph.GetDependencies(signal);
            if (deps.Count > 0)
            {
                lookup[signal.Name] = deps.Select(d => d.Name).ToList();
            }
        }

        return lookup;
    }

    private static IReadOnlyList<IgnitionRecordedStage>? BuildRecordedStages(IgnitionResult result)
    {
        if (result.StageResults is null || result.StageResults.Count == 0)
        {
            return null;
        }

        var stages = new List<IgnitionRecordedStage>(result.StageResults.Count);
        double cumulativeStartMs = 0;

        foreach (var stage in result.StageResults)
        {
            double durationMs = stage.Duration.TotalMilliseconds;
            double endMs = cumulativeStartMs + durationMs;

            stages.Add(new IgnitionRecordedStage(
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

    private static IgnitionRecordingSummary BuildRecordingSummary(IgnitionResult result)
    {
        int total = result.Results.Count;
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

        foreach (var r in result.Results)
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

            // Track durations for executed signals only
            bool isExecuted = r.Status is not (IgnitionSignalStatus.Skipped or IgnitionSignalStatus.Cancelled);

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

        // Calculate max concurrency
        int maxConcurrency = CalculateMaxConcurrency(result.Results);

        double? averageDurationMs = executedCount > 0 ? executedTotalDurationMs / executedCount : null;

        // Handle edge cases
        double? actualFastestDuration = null;
        if (executedCount > 0 && fastestDurationMs < double.MaxValue)
        {
            actualFastestDuration = fastestDurationMs;
        }
        else
        {
            fastestSignal = null;
        }

        return new IgnitionRecordingSummary(
            TotalSignals: total,
            SucceededCount: succeeded,
            FailedCount: failed,
            TimedOutCount: timedOut,
            SkippedCount: skipped,
            CancelledCount: cancelled,
            MaxConcurrency: maxConcurrency,
            SlowestSignalName: slowestSignal,
            SlowestDurationMs: executedCount > 0 ? slowestDurationMs : null,
            FastestSignalName: fastestSignal,
            FastestDurationMs: actualFastestDuration,
            AverageDurationMs: averageDurationMs);
    }

    private static int CalculateMaxConcurrency(IReadOnlyList<IgnitionSignalResult> results)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        var timePoints = new List<(double time, int delta)>();

        foreach (var r in results)
        {
            double start = r.StartedAt?.TotalMilliseconds ?? 0;
            double end = r.CompletedAt?.TotalMilliseconds ?? (start + r.Duration.TotalMilliseconds);
            timePoints.Add((start, 1));
            timePoints.Add((end, -1));
        }

        timePoints.Sort((a, b) =>
        {
            int cmp = a.time.CompareTo(b.time);
            if (cmp != 0)
            {
                return cmp;
            }
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

    private static IgnitionRecordingConfiguration BuildConfiguration(IgnitionOptions options)
    {
        return new IgnitionRecordingConfiguration(
            ExecutionMode: options.ExecutionMode.ToString(),
            Policy: options.Policy.ToString(),
            GlobalTimeoutMs: options.GlobalTimeout.TotalMilliseconds,
            CancelOnGlobalTimeout: options.CancelOnGlobalTimeout,
            CancelIndividualOnTimeout: options.CancelIndividualOnTimeout,
            MaxDegreeOfParallelism: options.MaxDegreeOfParallelism,
            StagePolicy: options.StagePolicy.ToString(),
            EarlyPromotionThreshold: options.EarlyPromotionThreshold,
            CancelDependentsOnFailure: options.CancelDependentsOnFailure);
    }
}
