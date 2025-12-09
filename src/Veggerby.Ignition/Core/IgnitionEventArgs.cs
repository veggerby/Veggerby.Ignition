using System.Collections.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Event arguments for when an individual ignition signal starts execution.
/// </summary>
public sealed class IgnitionSignalStartedEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <see cref="IgnitionSignalStartedEventArgs"/>.
    /// </summary>
    /// <param name="signalName">The name of the signal that started.</param>
    /// <param name="timestamp">The timestamp when the signal started.</param>
    public IgnitionSignalStartedEventArgs(string signalName, DateTimeOffset timestamp)
    {
        SignalName = signalName ?? throw new ArgumentNullException(nameof(signalName));
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the name of the signal that started.
    /// </summary>
    public string SignalName { get; }

    /// <summary>
    /// Gets the timestamp when the signal started.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}

/// <summary>
/// Event arguments for when an individual ignition signal completes execution.
/// </summary>
public sealed class IgnitionSignalCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <see cref="IgnitionSignalCompletedEventArgs"/>.
    /// </summary>
    /// <param name="signalName">The name of the signal that completed.</param>
    /// <param name="status">The completion status of the signal.</param>
    /// <param name="duration">The time taken by the signal to complete.</param>
    /// <param name="timestamp">The timestamp when the signal completed.</param>
    /// <param name="exception">The exception that occurred, if any.</param>
    public IgnitionSignalCompletedEventArgs(
        string signalName,
        IgnitionSignalStatus status,
        TimeSpan duration,
        DateTimeOffset timestamp,
        Exception? exception = null)
    {
        SignalName = signalName ?? throw new ArgumentNullException(nameof(signalName));
        Status = status;
        Duration = duration;
        Timestamp = timestamp;
        Exception = exception;
    }

    /// <summary>
    /// Gets the name of the signal that completed.
    /// </summary>
    public string SignalName { get; }

    /// <summary>
    /// Gets the completion status of the signal.
    /// </summary>
    public IgnitionSignalStatus Status { get; }

    /// <summary>
    /// Gets the time taken by the signal to complete.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the timestamp when the signal completed.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the exception that occurred during signal execution, if any.
    /// </summary>
    public Exception? Exception { get; }
}

/// <summary>
/// Event arguments for when the global timeout is reached.
/// </summary>
public sealed class IgnitionGlobalTimeoutEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <see cref="IgnitionGlobalTimeoutEventArgs"/>.
    /// </summary>
    /// <param name="globalTimeout">The configured global timeout duration.</param>
    /// <param name="elapsed">The elapsed time when timeout was triggered.</param>
    /// <param name="timestamp">The timestamp when the timeout occurred.</param>
    /// <param name="pendingSignals">The names of signals that had not completed when timeout occurred.</param>
    public IgnitionGlobalTimeoutEventArgs(
        TimeSpan globalTimeout,
        TimeSpan elapsed,
        DateTimeOffset timestamp,
        IReadOnlyList<string> pendingSignals)
    {
        GlobalTimeout = globalTimeout;
        Elapsed = elapsed;
        Timestamp = timestamp;
        PendingSignals = pendingSignals ?? throw new ArgumentNullException(nameof(pendingSignals));
    }

    /// <summary>
    /// Gets the configured global timeout duration.
    /// </summary>
    public TimeSpan GlobalTimeout { get; }

    /// <summary>
    /// Gets the elapsed time when the timeout was triggered.
    /// </summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets the timestamp when the timeout occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the names of signals that had not completed when timeout occurred.
    /// </summary>
    public IReadOnlyList<string> PendingSignals { get; }
}

/// <summary>
/// Event arguments for when the ignition coordinator completes execution.
/// </summary>
public sealed class IgnitionCoordinatorCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Creates a new instance of <see cref="IgnitionCoordinatorCompletedEventArgs"/>.
    /// </summary>
    /// <param name="finalState">The final state of the coordinator.</param>
    /// <param name="totalDuration">The total duration of execution.</param>
    /// <param name="timestamp">The timestamp when the coordinator completed.</param>
    /// <param name="result">The aggregated ignition result.</param>
    public IgnitionCoordinatorCompletedEventArgs(
        IgnitionState finalState,
        TimeSpan totalDuration,
        DateTimeOffset timestamp,
        IgnitionResult result)
    {
        FinalState = finalState;
        TotalDuration = totalDuration;
        Timestamp = timestamp;
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Gets the final state of the coordinator.
    /// </summary>
    public IgnitionState FinalState { get; }

    /// <summary>
    /// Gets the total duration of execution.
    /// </summary>
    public TimeSpan TotalDuration { get; }

    /// <summary>
    /// Gets the timestamp when the coordinator completed.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the aggregated ignition result.
    /// </summary>
    public IgnitionResult Result { get; }
}
