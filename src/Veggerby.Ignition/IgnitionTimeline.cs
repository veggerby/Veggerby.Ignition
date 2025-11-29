using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Veggerby.Ignition;

/// <summary>
/// Represents a single event in the ignition timeline.
/// </summary>
/// <param name="SignalName">Name of the signal.</param>
/// <param name="Status">Final status of the signal.</param>
/// <param name="StartMs">Start time in milliseconds from ignition start.</param>
/// <param name="EndMs">End time in milliseconds from ignition start.</param>
/// <param name="DurationMs">Duration in milliseconds.</param>
/// <param name="Stage">Optional stage number if using staged execution.</param>
/// <param name="Dependencies">Names of signals this signal depends on (dependency-aware mode only).</param>
/// <param name="FailedDependencies">Names of dependencies that failed, preventing this signal from executing.</param>
/// <param name="ConcurrentGroup">Identifier for signals that executed concurrently (signals with same group ran in parallel).</param>
public sealed record IgnitionTimelineEvent(
    string SignalName,
    string Status,
    double StartMs,
    double EndMs,
    double DurationMs,
    int? Stage = null,
    IReadOnlyList<string>? Dependencies = null,
    IReadOnlyList<string>? FailedDependencies = null,
    int? ConcurrentGroup = null);

/// <summary>
/// Represents a timeline boundary marker (e.g., global timeout).
/// </summary>
/// <param name="Type">Type of the boundary (e.g., "GlobalTimeout", "StageStart", "StageEnd").</param>
/// <param name="TimeMs">Time in milliseconds from ignition start when this boundary occurred.</param>
/// <param name="Details">Optional additional details about the boundary.</param>
public sealed record IgnitionTimelineBoundary(
    string Type,
    double TimeMs,
    string? Details = null);

/// <summary>
/// Represents stage information in the timeline.
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
public sealed record IgnitionTimelineStage(
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
/// A time-aligned sequence of startup events for analysis and visualization.
/// Provides a Gantt-like view of the ignition process including signal timing,
/// dependencies, concurrent execution groups, and timeout boundaries.
/// </summary>
/// <remarks>
/// <para>
/// The timeline is designed for export to JSON format for use with visualization tools,
/// debugging, profiling, container warmup analysis, or CI timing regression detection.
/// </para>
/// <para>
/// All time values are represented as milliseconds from the start of ignition (time zero).
/// This enables straightforward visualization as a Gantt chart where signals can be
/// plotted on a common timeline axis.
/// </para>
/// </remarks>
public sealed class IgnitionTimeline
{
    /// <summary>
    /// Gets or sets the schema version for forward compatibility.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

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
    /// Gets or sets the execution mode used during ignition.
    /// </summary>
    [JsonPropertyName("executionMode")]
    public string? ExecutionMode { get; init; }

    /// <summary>
    /// Gets or sets the configured global timeout in milliseconds.
    /// </summary>
    [JsonPropertyName("globalTimeoutMs")]
    public double? GlobalTimeoutMs { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when ignition started (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when ignition completed (ISO 8601 format).
    /// </summary>
    [JsonPropertyName("completedAt")]
    public string? CompletedAt { get; init; }

    /// <summary>
    /// Gets or sets the list of signal events in the timeline.
    /// </summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<IgnitionTimelineEvent> Events { get; init; } = [];

    /// <summary>
    /// Gets or sets boundary markers such as global timeout.
    /// </summary>
    [JsonPropertyName("boundaries")]
    public IReadOnlyList<IgnitionTimelineBoundary> Boundaries { get; init; } = [];

    /// <summary>
    /// Gets or sets stage information when using staged execution.
    /// </summary>
    [JsonPropertyName("stages")]
    public IReadOnlyList<IgnitionTimelineStage>? Stages { get; init; }

    /// <summary>
    /// Gets or sets summary statistics for the timeline.
    /// </summary>
    [JsonPropertyName("summary")]
    public IgnitionTimelineSummary? Summary { get; init; }

    /// <summary>
    /// Exports the timeline to a JSON string.
    /// </summary>
    /// <param name="indented">Whether to format the JSON with indentation for readability.</param>
    /// <returns>A JSON string representation of the timeline.</returns>
    public string ToJson(bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Creates a timeline from JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized timeline, or null if deserialization fails.</returns>
    public static IgnitionTimeline? FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Deserialize<IgnitionTimeline>(json, options);
    }
}

/// <summary>
/// Summary statistics for the ignition timeline.
/// </summary>
/// <param name="TotalSignals">Total number of signals in the ignition.</param>
/// <param name="SucceededCount">Number of signals that succeeded.</param>
/// <param name="FailedCount">Number of signals that failed.</param>
/// <param name="TimedOutCount">Number of signals that timed out.</param>
/// <param name="SkippedCount">Number of signals that were skipped.</param>
/// <param name="CancelledCount">Number of signals that were cancelled.</param>
/// <param name="MaxConcurrency">Maximum number of signals executing concurrently.</param>
/// <param name="SlowestSignal">Name of the slowest signal.</param>
/// <param name="SlowestDurationMs">Duration of the slowest signal in milliseconds.</param>
/// <param name="FastestSignal">Name of the fastest signal.</param>
/// <param name="FastestDurationMs">Duration of the fastest signal in milliseconds.</param>
/// <param name="AverageDurationMs">Average signal duration in milliseconds.</param>
public sealed record IgnitionTimelineSummary(
    int TotalSignals,
    int SucceededCount,
    int FailedCount,
    int TimedOutCount,
    int SkippedCount,
    int CancelledCount,
    int MaxConcurrency,
    string? SlowestSignal,
    double? SlowestDurationMs,
    string? FastestSignal,
    double? FastestDurationMs,
    double? AverageDurationMs);
