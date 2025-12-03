using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veggerby.Ignition;

/// <summary>
/// Represents a recorded signal event with detailed information for replay.
/// </summary>
/// <param name="SignalName">Name of the signal.</param>
/// <param name="Status">Final status of the signal.</param>
/// <param name="StartMs">Start time in milliseconds from ignition start.</param>
/// <param name="EndMs">End time in milliseconds from ignition start.</param>
/// <param name="DurationMs">Duration in milliseconds.</param>
/// <param name="Stage">Optional stage number if using staged execution.</param>
/// <param name="Dependencies">Names of signals this signal depends on.</param>
/// <param name="FailedDependencies">Names of dependencies that failed, preventing this signal from executing.</param>
/// <param name="CancellationReason">Reason for cancellation if signal was cancelled.</param>
/// <param name="CancelledBySignal">Name of the signal that triggered the cancellation.</param>
/// <param name="ExceptionType">Type name of the exception if failed.</param>
/// <param name="ExceptionMessage">Exception message if failed.</param>
/// <param name="ConfiguredTimeoutMs">The configured timeout in milliseconds for this signal.</param>
public sealed record IgnitionRecordedSignal(
    string SignalName,
    string Status,
    double StartMs,
    double EndMs,
    double DurationMs,
    int? Stage = null,
    IReadOnlyList<string>? Dependencies = null,
    IReadOnlyList<string>? FailedDependencies = null,
    string? CancellationReason = null,
    string? CancelledBySignal = null,
    string? ExceptionType = null,
    string? ExceptionMessage = null,
    double? ConfiguredTimeoutMs = null);

/// <summary>
/// Configuration snapshot captured at recording time.
/// </summary>
/// <param name="ExecutionMode">The execution mode used.</param>
/// <param name="Policy">The ignition policy used.</param>
/// <param name="GlobalTimeoutMs">Global timeout in milliseconds.</param>
/// <param name="CancelOnGlobalTimeout">Whether global timeout causes cancellation.</param>
/// <param name="CancelIndividualOnTimeout">Whether per-signal timeout causes cancellation.</param>
/// <param name="MaxDegreeOfParallelism">Max parallelism if configured.</param>
/// <param name="StagePolicy">Stage policy if using staged execution.</param>
/// <param name="EarlyPromotionThreshold">Early promotion threshold if configured.</param>
/// <param name="CancelDependentsOnFailure">Whether to cancel dependents on failure.</param>
public sealed record IgnitionRecordingConfiguration(
    string ExecutionMode,
    string Policy,
    double GlobalTimeoutMs,
    bool CancelOnGlobalTimeout,
    bool CancelIndividualOnTimeout,
    int? MaxDegreeOfParallelism = null,
    string? StagePolicy = null,
    double? EarlyPromotionThreshold = null,
    bool? CancelDependentsOnFailure = null);

/// <summary>
/// Represents a stage in the recording.
/// </summary>
/// <param name="StageNumber">The stage number (0-based).</param>
/// <param name="StartMs">Start time of the stage in milliseconds from ignition start.</param>
/// <param name="EndMs">End time of the stage in milliseconds from ignition start.</param>
/// <param name="DurationMs">Duration of the stage in milliseconds.</param>
/// <param name="SignalCount">Number of signals in this stage.</param>
/// <param name="SucceededCount">Number of signals that succeeded.</param>
/// <param name="FailedCount">Number of signals that failed.</param>
/// <param name="TimedOutCount">Number of signals that timed out.</param>
/// <param name="EarlyPromoted">Whether the next stage was started before this stage fully completed.</param>
public sealed record IgnitionRecordedStage(
    int StageNumber,
    double StartMs,
    double EndMs,
    double DurationMs,
    int SignalCount,
    int SucceededCount,
    int FailedCount,
    int TimedOutCount,
    bool EarlyPromoted);

/// <summary>
/// Summary statistics for the recording.
/// </summary>
/// <param name="TotalSignals">Total number of signals in the ignition.</param>
/// <param name="SucceededCount">Number of signals that succeeded.</param>
/// <param name="FailedCount">Number of signals that failed.</param>
/// <param name="TimedOutCount">Number of signals that timed out.</param>
/// <param name="SkippedCount">Number of signals that were skipped.</param>
/// <param name="CancelledCount">Number of signals that were cancelled.</param>
/// <param name="MaxConcurrency">Maximum number of signals executing concurrently.</param>
/// <param name="SlowestSignalName">Name of the slowest signal.</param>
/// <param name="SlowestDurationMs">Duration of the slowest signal in milliseconds.</param>
/// <param name="FastestSignalName">Name of the fastest signal.</param>
/// <param name="FastestDurationMs">Duration of the fastest signal in milliseconds.</param>
/// <param name="AverageDurationMs">Average signal duration in milliseconds.</param>
public sealed record IgnitionRecordingSummary(
    int TotalSignals,
    int SucceededCount,
    int FailedCount,
    int TimedOutCount,
    int SkippedCount,
    int CancelledCount,
    int MaxConcurrency,
    string? SlowestSignalName,
    double? SlowestDurationMs,
    string? FastestSignalName,
    double? FastestDurationMs,
    double? AverageDurationMs);

/// <summary>
/// A complete recording of an ignition run for replay and diagnostics.
/// Captures timing, dependencies, failures, durations, and sequence ordering
/// in a structured format that can be serialized, stored, and replayed.
/// </summary>
/// <remarks>
/// <para>
/// The recording extends the timeline concept with additional data needed for:
/// <list type="bullet">
///   <item>Validating invariants (unexpected timing drift, inconsistent rescheduling)</item>
///   <item>Simulating "what if" scenarios (e.g., what if a signal timed out earlier)</item>
///   <item>Testing stage dependency correctness</item>
///   <item>Diagnosing slow startup in prod vs dev</item>
///   <item>CI regression detection</item>
///   <item>Offline simulation</item>
/// </list>
/// </para>
/// <para>
/// All time values are represented as milliseconds from the start of ignition (time zero).
/// </para>
/// </remarks>
public sealed class IgnitionRecording
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions CompactSerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Gets or sets the schema version for forward compatibility.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    /// <summary>
    /// Gets or sets a unique identifier for this recording.
    /// </summary>
    [JsonPropertyName("recordingId")]
    public string RecordingId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets the timestamp when the recording was created (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("recordedAt")]
    public string? RecordedAt { get; init; }

    /// <summary>
    /// Gets or sets the total duration of the ignition process in milliseconds.
    /// </summary>
    [JsonPropertyName("totalDurationMs")]
    public double TotalDurationMs { get; init; }

    /// <summary>
    /// Gets or sets whether the ignition timed out.
    /// </summary>
    [JsonPropertyName("timedOut")]
    public bool TimedOut { get; init; }

    /// <summary>
    /// Gets or sets the final state of the coordinator.
    /// </summary>
    [JsonPropertyName("finalState")]
    public string? FinalState { get; init; }

    /// <summary>
    /// Gets or sets the configuration used during the ignition run.
    /// </summary>
    [JsonPropertyName("configuration")]
    public IgnitionRecordingConfiguration? Configuration { get; init; }

    /// <summary>
    /// Gets or sets the list of recorded signal events.
    /// </summary>
    [JsonPropertyName("signals")]
    public IReadOnlyList<IgnitionRecordedSignal> Signals { get; init; } = [];

    /// <summary>
    /// Gets or sets stage information when using staged execution.
    /// </summary>
    [JsonPropertyName("stages")]
    public IReadOnlyList<IgnitionRecordedStage>? Stages { get; init; }

    /// <summary>
    /// Gets or sets summary statistics for the recording.
    /// </summary>
    [JsonPropertyName("summary")]
    public IgnitionRecordingSummary? Summary { get; init; }

    /// <summary>
    /// Gets or sets optional metadata for the recording (e.g., environment, version).
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Exports the recording to a JSON string.
    /// </summary>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the recording.</returns>
    public string ToJson(bool indented = true)
    {
        var options = indented ? DefaultSerializerOptions : CompactSerializerOptions;
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Creates a recording from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized recording, or null if deserialization fails.</returns>
    public static IgnitionRecording? FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return JsonSerializer.Deserialize<IgnitionRecording>(json, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts this recording to a timeline for visualization.
    /// </summary>
    /// <returns>An <see cref="IgnitionTimeline"/> representation of this recording.</returns>
    public IgnitionTimeline ToTimeline()
    {
        var events = new List<IgnitionTimelineEvent>();

        foreach (var signal in Signals)
        {
            events.Add(new IgnitionTimelineEvent(
                SignalName: signal.SignalName,
                Status: signal.Status,
                StartMs: signal.StartMs,
                EndMs: signal.EndMs,
                DurationMs: signal.DurationMs,
                Stage: signal.Stage,
                Dependencies: signal.Dependencies,
                FailedDependencies: signal.FailedDependencies,
                ConcurrentGroup: null)); // Would need to be recalculated
        }

        var stages = Stages?.Select(s => new IgnitionTimelineStage(
            StageNumber: s.StageNumber,
            StartMs: s.StartMs,
            EndMs: s.EndMs,
            DurationMs: s.DurationMs,
            SignalCount: s.SignalCount,
            SucceededCount: s.SucceededCount,
            FailedCount: s.FailedCount,
            TimedOutCount: s.TimedOutCount,
            EarlyPromoted: s.EarlyPromoted)).ToList();

        var summary = Summary is null ? null : new IgnitionTimelineSummary(
            TotalSignals: Summary.TotalSignals,
            SucceededCount: Summary.SucceededCount,
            FailedCount: Summary.FailedCount,
            TimedOutCount: Summary.TimedOutCount,
            SkippedCount: Summary.SkippedCount,
            CancelledCount: Summary.CancelledCount,
            MaxConcurrency: Summary.MaxConcurrency,
            SlowestSignal: Summary.SlowestSignalName,
            SlowestDurationMs: Summary.SlowestDurationMs,
            FastestSignal: Summary.FastestSignalName,
            FastestDurationMs: Summary.FastestDurationMs,
            AverageDurationMs: Summary.AverageDurationMs);

        return new IgnitionTimeline
        {
            SchemaVersion = SchemaVersion,
            TotalDurationMs = TotalDurationMs,
            TimedOut = TimedOut,
            ExecutionMode = Configuration?.ExecutionMode,
            GlobalTimeoutMs = Configuration?.GlobalTimeoutMs,
            StartedAt = RecordedAt,
            Events = events,
            Stages = stages,
            Summary = summary
        };
    }
}
