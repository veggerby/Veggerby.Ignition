using System.Collections.Generic;
using System.Linq;

namespace Veggerby.Ignition.Diagnostics;

/// <summary>
/// Severity level for replay validation issues.
/// </summary>
public enum ReplayValidationSeverity
{
    /// <summary>
    /// Informational finding, not necessarily a problem.
    /// </summary>
    Info,

    /// <summary>
    /// Warning indicating potential issue that should be reviewed.
    /// </summary>
    Warning,

    /// <summary>
    /// Error indicating a violation of expected invariants.
    /// </summary>
    Error
}

/// <summary>
/// Represents a single validation issue found during replay analysis.
/// </summary>
/// <param name="Severity">The severity of the issue.</param>
/// <param name="Code">A short code identifying the issue type.</param>
/// <param name="Message">Human-readable description of the issue.</param>
/// <param name="SignalName">Name of the signal involved, if applicable.</param>
/// <param name="Details">Additional details about the issue.</param>
public sealed record ReplayValidationIssue(
    ReplayValidationSeverity Severity,
    string Code,
    string Message,
    string? SignalName = null,
    string? Details = null);

/// <summary>
/// Result of replaying and validating a recording.
/// </summary>
/// <param name="IsValid">Whether the recording passes all validation checks.</param>
/// <param name="Issues">List of validation issues found.</param>
/// <param name="Recording">The recording that was validated.</param>
public sealed record ReplayValidationResult(
    bool IsValid,
    IReadOnlyList<ReplayValidationIssue> Issues,
    IgnitionRecording Recording)
{
    /// <summary>
    /// Gets issues filtered by severity.
    /// </summary>
    public IEnumerable<ReplayValidationIssue> GetIssuesBySeverity(ReplayValidationSeverity severity)
        => Issues.Where(i => i.Severity == severity);

    /// <summary>
    /// Gets the count of errors.
    /// </summary>
    public int ErrorCount => Issues.Count(i => i.Severity == ReplayValidationSeverity.Error);

    /// <summary>
    /// Gets the count of warnings.
    /// </summary>
    public int WarningCount => Issues.Count(i => i.Severity == ReplayValidationSeverity.Warning);
}

/// <summary>
/// Result of a "what if" simulation.
/// </summary>
/// <param name="OriginalRecording">The original recording.</param>
/// <param name="SimulatedSignals">The simulated signal results after applying the scenario.</param>
/// <param name="AffectedSignals">Names of signals that would be affected by the scenario.</param>
/// <param name="Description">Description of the simulation scenario.</param>
public sealed record WhatIfSimulationResult(
    IgnitionRecording OriginalRecording,
    IReadOnlyList<IgnitionRecordedSignal> SimulatedSignals,
    IReadOnlyList<string> AffectedSignals,
    string Description);

/// <summary>
/// Comparison result between two recordings.
/// </summary>
/// <param name="Recording1">The first recording (baseline).</param>
/// <param name="Recording2">The second recording (comparison).</param>
/// <param name="DurationDifferenceMs">Difference in total duration (positive = slower).</param>
/// <param name="DurationChangePercent">Percentage change in duration.</param>
/// <param name="SignalComparisons">Per-signal comparison data.</param>
/// <param name="AddedSignals">Signals present in recording 2 but not recording 1.</param>
/// <param name="RemovedSignals">Signals present in recording 1 but not recording 2.</param>
public sealed record RecordingComparisonResult(
    IgnitionRecording Recording1,
    IgnitionRecording Recording2,
    double DurationDifferenceMs,
    double DurationChangePercent,
    IReadOnlyList<SignalComparison> SignalComparisons,
    IReadOnlyList<string> AddedSignals,
    IReadOnlyList<string> RemovedSignals);

/// <summary>
/// Comparison of a single signal between two recordings.
/// </summary>
/// <param name="SignalName">Name of the signal.</param>
/// <param name="Duration1Ms">Duration in recording 1.</param>
/// <param name="Duration2Ms">Duration in recording 2.</param>
/// <param name="DurationDifferenceMs">Difference in duration (positive = slower in recording 2).</param>
/// <param name="DurationChangePercent">Percentage change in duration.</param>
/// <param name="Status1">Status in recording 1.</param>
/// <param name="Status2">Status in recording 2.</param>
/// <param name="StatusChanged">Whether the status changed between recordings.</param>
public sealed record SignalComparison(
    string SignalName,
    double Duration1Ms,
    double Duration2Ms,
    double DurationDifferenceMs,
    double DurationChangePercent,
    string Status1,
    string Status2,
    bool StatusChanged);

/// <summary>
/// Provides replay and analysis capabilities for ignition recordings.
/// Validates invariants, simulates "what if" scenarios, and tests stage dependency correctness.
/// </summary>
/// <remarks>
/// <para>
/// The replayer enables:
/// <list type="bullet">
///   <item>Validating invariants (unexpected timing drift, inconsistent scheduling)</item>
///   <item>Simulating "what if" scenarios (e.g., what if a signal timed out earlier)</item>
///   <item>Testing stage dependency correctness</item>
///   <item>Comparing recordings (e.g., prod vs dev, before vs after)</item>
///   <item>Detecting CI regression in startup timing</item>
/// </list>
/// </para>
/// </remarks>
public sealed class IgnitionReplayer
{
    private readonly IgnitionRecording _recording;

    /// <summary>
    /// Creates a new replayer for the specified recording.
    /// </summary>
    /// <param name="recording">The recording to analyze.</param>
    /// <exception cref="ArgumentNullException">Thrown when recording is null.</exception>
    public IgnitionReplayer(IgnitionRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        _recording = recording;
    }

    /// <summary>
    /// Gets the recording being analyzed.
    /// </summary>
    public IgnitionRecording Recording => _recording;

    /// <summary>
    /// Validates the recording for invariant violations and consistency issues.
    /// </summary>
    /// <returns>A validation result containing any issues found.</returns>
    public ReplayValidationResult Validate()
    {
        var issues = new List<ReplayValidationIssue>();

        ValidateSignalTiming(issues);
        ValidateDependencyOrder(issues);
        ValidateStageExecution(issues);
        ValidateConfigurationConsistency(issues);
        ValidateCompleteness(issues);

        bool isValid = !issues.Any(i => i.Severity == ReplayValidationSeverity.Error);
        return new ReplayValidationResult(isValid, issues, _recording);
    }

    /// <summary>
    /// Simulates what would happen if the specified signal had timed out earlier.
    /// </summary>
    /// <param name="signalName">The name of the signal to simulate timing out.</param>
    /// <param name="newTimeoutMs">The new timeout to apply in milliseconds.</param>
    /// <returns>The simulation result showing affected signals.</returns>
    /// <exception cref="ArgumentException">Thrown when the signal is not found in the recording.</exception>
    public WhatIfSimulationResult SimulateEarlierTimeout(string signalName, double newTimeoutMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var signal = _recording.Signals.FirstOrDefault(s => s.SignalName == signalName);
        if (signal is null)
        {
            throw new ArgumentException($"Signal '{signalName}' not found in recording.", nameof(signalName));
        }

        var simulatedSignals = new List<IgnitionRecordedSignal>();
        var affectedSignals = new List<string>();

        // Determine if the signal would time out
        bool wouldTimeout = signal.DurationMs > newTimeoutMs;
        if (wouldTimeout)
        {
            affectedSignals.Add(signalName);
        }

        // Build dependency map for transitive dependency checking
        var dependents = BuildDependentsMap();

        foreach (var s in _recording.Signals)
        {
            if (s.SignalName == signalName)
            {
                // Apply the timeout simulation
                if (wouldTimeout)
                {
                    // Signal would have timed out
                    simulatedSignals.Add(s with
                    {
                        Status = "TimedOut",
                        DurationMs = newTimeoutMs,
                        EndMs = s.StartMs + newTimeoutMs,
                        CancellationReason = "PerSignalTimeout"
                    });
                }
                else
                {
                    // Signal would still complete in time
                    simulatedSignals.Add(s);
                }
            }
            else if (wouldTimeout && WouldBeAffectedByFailure(s.SignalName, signalName, dependents, new HashSet<string>()))
            {
                // This signal would be skipped due to dependency failure (using recursive check)
                affectedSignals.Add(s.SignalName);
                simulatedSignals.Add(s with
                {
                    Status = "Skipped",
                    DurationMs = 0,
                    FailedDependencies = new List<string> { signalName }
                });
            }
            else
            {
                simulatedSignals.Add(s);
            }
        }

        return new WhatIfSimulationResult(
            _recording,
            simulatedSignals,
            affectedSignals,
            $"Simulated {signalName} timing out at {newTimeoutMs}ms instead of completing at {signal.DurationMs}ms");
    }

    /// <summary>
    /// Simulates what would happen if the specified signal had failed.
    /// </summary>
    /// <param name="signalName">The name of the signal to simulate failing.</param>
    /// <returns>The simulation result showing affected signals.</returns>
    /// <exception cref="ArgumentException">Thrown when the signal is not found in the recording.</exception>
    public WhatIfSimulationResult SimulateFailure(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        var signal = _recording.Signals.FirstOrDefault(s => s.SignalName == signalName);
        if (signal is null)
        {
            throw new ArgumentException($"Signal '{signalName}' not found in recording.", nameof(signalName));
        }

        var simulatedSignals = new List<IgnitionRecordedSignal>();
        var affectedSignals = new List<string> { signalName };

        // Build dependency map
        var dependents = BuildDependentsMap();

        foreach (var s in _recording.Signals)
        {
            if (s.SignalName == signalName)
            {
                // Mark as failed
                simulatedSignals.Add(s with
                {
                    Status = "Failed",
                    ExceptionType = "System.Exception",
                    ExceptionMessage = "Simulated failure"
                });
            }
            else if (WouldBeAffectedByFailure(s.SignalName, signalName, dependents, new HashSet<string>()))
            {
                // This signal would be skipped due to dependency failure
                affectedSignals.Add(s.SignalName);
                simulatedSignals.Add(s with
                {
                    Status = "Skipped",
                    DurationMs = 0,
                    FailedDependencies = new List<string> { signalName }
                });
            }
            else
            {
                simulatedSignals.Add(s);
            }
        }

        return new WhatIfSimulationResult(
            _recording,
            simulatedSignals,
            affectedSignals,
            $"Simulated {signalName} failing instead of {signal.Status}");
    }

    /// <summary>
    /// Compares this recording against another recording.
    /// </summary>
    /// <param name="other">The recording to compare against.</param>
    /// <returns>A comparison result showing differences.</returns>
    /// <exception cref="ArgumentNullException">Thrown when other is null.</exception>
    public RecordingComparisonResult CompareTo(IgnitionRecording other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var durationDiff = other.TotalDurationMs - _recording.TotalDurationMs;
        var durationPercent = _recording.TotalDurationMs > 0
            ? (durationDiff / _recording.TotalDurationMs) * 100
            : 0;

        var signals1 = _recording.Signals.ToDictionary(s => s.SignalName);
        var signals2 = other.Signals.ToDictionary(s => s.SignalName);

        var comparisons = new List<SignalComparison>();
        var added = new List<string>();
        var removed = new List<string>();

        // Find signals in both recordings
        foreach (var kvp in signals1)
        {
            if (signals2.TryGetValue(kvp.Key, out var s2))
            {
                var s1 = kvp.Value;
                var signalDurationDiff = s2.DurationMs - s1.DurationMs;
                var signalDurationPercent = s1.DurationMs > 0
                    ? (signalDurationDiff / s1.DurationMs) * 100
                    : 0;

                comparisons.Add(new SignalComparison(
                    SignalName: kvp.Key,
                    Duration1Ms: s1.DurationMs,
                    Duration2Ms: s2.DurationMs,
                    DurationDifferenceMs: signalDurationDiff,
                    DurationChangePercent: signalDurationPercent,
                    Status1: s1.Status,
                    Status2: s2.Status,
                    StatusChanged: s1.Status != s2.Status));
            }
            else
            {
                removed.Add(kvp.Key);
            }
        }

        // Find signals only in second recording
        added.AddRange(signals2.Keys.Where(key => !signals1.ContainsKey(key)));

        return new RecordingComparisonResult(
            _recording,
            other,
            durationDiff,
            durationPercent,
            comparisons,
            added,
            removed);
    }

    /// <summary>
    /// Identifies signals that might be causing startup slowdown.
    /// Returns signals ordered by their contribution to total duration.
    /// </summary>
    /// <param name="minDurationMs">Minimum duration in milliseconds to consider a signal slow.</param>
    /// <returns>List of slow signals with their timing data.</returns>
    public IReadOnlyList<IgnitionRecordedSignal> IdentifySlowSignals(double minDurationMs = 100)
    {
        return _recording.Signals
            .Where(s => s.DurationMs >= minDurationMs && s.Status == "Succeeded")
            .OrderByDescending(s => s.DurationMs)
            .ToList();
    }

    /// <summary>
    /// Identifies signals that are on the critical path (signals whose duration directly affects total duration).
    /// </summary>
    /// <returns>List of signals on the critical path.</returns>
    public IReadOnlyList<IgnitionRecordedSignal> IdentifyCriticalPath()
    {
        if (_recording.Signals.Count == 0)
        {
            return [];
        }

        // For sequential execution, all signals are on the critical path
        if (_recording.Configuration?.ExecutionMode == "Sequential")
        {
            return _recording.Signals.ToList();
        }

        // For parallel/staged, find signals that end at or near the total duration
        var totalDuration = _recording.TotalDurationMs;
        const double tolerance = 10; // 10ms tolerance

        return _recording.Signals
            .Where(s => Math.Abs(s.EndMs - totalDuration) < tolerance)
            .OrderByDescending(s => s.DurationMs)
            .ToList();
    }

    /// <summary>
    /// Gets the execution order of signals as recorded.
    /// </summary>
    /// <returns>List of signal names in the order they started.</returns>
    public IReadOnlyList<string> GetExecutionOrder()
    {
        return _recording.Signals
            .OrderBy(s => s.StartMs)
            .ThenBy(s => s.SignalName)
            .Select(s => s.SignalName)
            .ToList();
    }

    /// <summary>
    /// Gets signals that executed concurrently.
    /// </summary>
    /// <returns>List of groups, where each group contains signals that overlapped in execution.</returns>
    public IReadOnlyList<IReadOnlyList<string>> GetConcurrentGroups()
    {
        if (_recording.Signals.Count == 0)
        {
            return [];
        }

        var groups = new List<List<string>>();
        var sorted = _recording.Signals.OrderBy(s => s.StartMs).ToList();
        var currentGroup = new List<string>();
        var currentGroupEnd = double.MinValue;

        foreach (var signal in sorted)
        {
            if (signal.StartMs < currentGroupEnd)
            {
                // Overlaps with current group
                currentGroup.Add(signal.SignalName);
                currentGroupEnd = Math.Max(currentGroupEnd, signal.EndMs);
            }
            else
            {
                // Start a new group
                if (currentGroup.Count > 0)
                {
                    groups.Add(currentGroup);
                }
                currentGroup = new List<string> { signal.SignalName };
                currentGroupEnd = signal.EndMs;
            }
        }

        if (currentGroup.Count > 0)
        {
            groups.Add(currentGroup);
        }

        return groups;
    }

    private void ValidateSignalTiming(List<ReplayValidationIssue> issues)
    {
        foreach (var signal in _recording.Signals)
        {
            // Check for negative durations
            if (signal.DurationMs < 0)
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Error,
                    "NEGATIVE_DURATION",
                    $"Signal has negative duration: {signal.DurationMs}ms",
                    signal.SignalName));
            }

            // Check for end before start
            if (signal.EndMs < signal.StartMs)
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Error,
                    "INVALID_TIME_RANGE",
                    $"Signal end time ({signal.EndMs}ms) is before start time ({signal.StartMs}ms)",
                    signal.SignalName));
            }

            // Check for duration mismatch
            var expectedDuration = signal.EndMs - signal.StartMs;
            var durationDrift = Math.Abs(signal.DurationMs - expectedDuration);
            if (durationDrift > 1) // 1ms tolerance
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Warning,
                    "DURATION_DRIFT",
                    $"Duration ({signal.DurationMs}ms) doesn't match end-start ({expectedDuration}ms), drift: {durationDrift:F2}ms",
                    signal.SignalName));
            }

            // Check signal started before total duration ended
            if (signal.StartMs > _recording.TotalDurationMs)
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Error,
                    "START_AFTER_TOTAL",
                    $"Signal started at {signal.StartMs}ms but total duration is {_recording.TotalDurationMs}ms",
                    signal.SignalName));
            }
        }
    }

    private void ValidateDependencyOrder(List<ReplayValidationIssue> issues)
    {
        var signalsByName = _recording.Signals.ToDictionary(s => s.SignalName);

        foreach (var signal in _recording.Signals)
        {
            if (signal.Dependencies is null || signal.Dependencies.Count == 0)
            {
                continue;
            }

            foreach (var dep in signal.Dependencies)
            {
                if (!signalsByName.TryGetValue(dep, out var depSignal))
                {
                    issues.Add(new ReplayValidationIssue(
                        ReplayValidationSeverity.Warning,
                        "MISSING_DEPENDENCY",
                        $"Signal declares dependency on '{dep}' which is not in the recording",
                        signal.SignalName));
                    continue;
                }

                // In dependency-aware mode, a signal should start after its dependencies complete
                if (signal.StartMs < depSignal.EndMs && signal.Status != "Skipped")
                {
                    issues.Add(new ReplayValidationIssue(
                        ReplayValidationSeverity.Error,
                        "DEPENDENCY_ORDER_VIOLATION",
                        $"Signal started at {signal.StartMs}ms before dependency '{dep}' completed at {depSignal.EndMs}ms",
                        signal.SignalName,
                        $"Dependency: {dep}"));
                }
            }
        }
    }

    private void ValidateStageExecution(List<ReplayValidationIssue> issues)
    {
        if (_recording.Stages is null || _recording.Stages.Count == 0)
        {
            return;
        }

        for (int i = 1; i < _recording.Stages.Count; i++)
        {
            var prevStage = _recording.Stages[i - 1];
            var currStage = _recording.Stages[i];

            // Current stage should start after or at the same time as previous stage ends (unless early promoted)
            if (!prevStage.EarlyPromoted && currStage.StartMs < prevStage.EndMs)
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Error,
                    "STAGE_ORDER_VIOLATION",
                    $"Stage {currStage.StageNumber} started at {currStage.StartMs}ms before stage {prevStage.StageNumber} ended at {prevStage.EndMs}ms",
                    Details: "This may indicate incorrect stage execution order"));
            }
        }

        // Validate signals are in correct stages
        foreach (var signal in _recording.Signals)
        {
            if (!signal.Stage.HasValue)
            {
                continue;
            }

            var stage = _recording.Stages.FirstOrDefault(s => s.StageNumber == signal.Stage.Value);
            if (stage is null)
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Warning,
                    "ORPHAN_STAGE_SIGNAL",
                    $"Signal assigned to stage {signal.Stage.Value} which is not in the stages list",
                    signal.SignalName));
                continue;
            }

            // Signal should execute within its stage's time window (with some tolerance)
            const double tolerance = 10; // 10ms tolerance
            if (signal.StartMs < stage.StartMs - tolerance)
            {
                issues.Add(new ReplayValidationIssue(
                    ReplayValidationSeverity.Warning,
                    "SIGNAL_BEFORE_STAGE",
                    $"Signal started at {signal.StartMs}ms before stage {stage.StageNumber} started at {stage.StartMs}ms",
                    signal.SignalName));
            }
        }
    }

    private void ValidateConfigurationConsistency(List<ReplayValidationIssue> issues)
    {
        var config = _recording.Configuration;
        if (config is null)
        {
            issues.Add(new ReplayValidationIssue(
                ReplayValidationSeverity.Warning,
                "MISSING_CONFIGURATION",
                "Recording does not include configuration snapshot"));
            return;
        }

        // Check if timeout and actual duration are consistent
        if (_recording.TimedOut && config.GlobalTimeoutMs > 0 && _recording.TotalDurationMs < config.GlobalTimeoutMs - 100) // 100ms tolerance
        {
            issues.Add(new ReplayValidationIssue(
                ReplayValidationSeverity.Warning,
                "PREMATURE_TIMEOUT",
                $"Recording marked as timed out but total duration ({_recording.TotalDurationMs}ms) is less than global timeout ({config.GlobalTimeoutMs}ms)"));
        }
    }

    private void ValidateCompleteness(List<ReplayValidationIssue> issues)
    {
        var summary = _recording.Summary;
        if (summary is null)
        {
            return;
        }

        var actualCounts = new Dictionary<string, int>
        {
            ["Succeeded"] = 0,
            ["Failed"] = 0,
            ["TimedOut"] = 0,
            ["Skipped"] = 0,
            ["Cancelled"] = 0
        };

        foreach (var signal in _recording.Signals)
        {
            if (actualCounts.TryGetValue(signal.Status, out var count))
            {
                actualCounts[signal.Status] = count + 1;
            }
        }

        // Verify counts match summary
        if (summary.SucceededCount != actualCounts["Succeeded"])
        {
            issues.Add(new ReplayValidationIssue(
                ReplayValidationSeverity.Warning,
                "COUNT_MISMATCH",
                $"Summary shows {summary.SucceededCount} succeeded but found {actualCounts["Succeeded"]}"));
        }

        if (summary.FailedCount != actualCounts["Failed"])
        {
            issues.Add(new ReplayValidationIssue(
                ReplayValidationSeverity.Warning,
                "COUNT_MISMATCH",
                $"Summary shows {summary.FailedCount} failed but found {actualCounts["Failed"]}"));
        }

        if (summary.TotalSignals != _recording.Signals.Count)
        {
            issues.Add(new ReplayValidationIssue(
                ReplayValidationSeverity.Warning,
                "TOTAL_MISMATCH",
                $"Summary shows {summary.TotalSignals} total signals but recording contains {_recording.Signals.Count}"));
        }
    }

    private Dictionary<string, List<string>> BuildDependentsMap()
    {
        var dependents = new Dictionary<string, List<string>>();

        foreach (var signal in _recording.Signals)
        {
            if (signal.Dependencies is null)
            {
                continue;
            }

            foreach (var dep in signal.Dependencies)
            {
                if (!dependents.ContainsKey(dep))
                {
                    dependents[dep] = new List<string>();
                }
                dependents[dep].Add(signal.SignalName);
            }
        }

        return dependents;
    }

    private bool WouldBeAffectedByFailure(string signalName, string failedSignal, Dictionary<string, List<string>> dependents, HashSet<string> visited)
    {
        if (visited.Contains(signalName))
        {
            return false;
        }
        visited.Add(signalName);

        var signal = _recording.Signals.FirstOrDefault(s => s.SignalName == signalName);
        if (signal?.Dependencies?.Contains(failedSignal) == true)
        {
            return true;
        }

        // Check transitive dependencies
        if (signal?.Dependencies is not null)
        {
            foreach (var dep in signal.Dependencies)
            {
                if (WouldBeAffectedByFailure(dep, failedSignal, dependents, visited))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
