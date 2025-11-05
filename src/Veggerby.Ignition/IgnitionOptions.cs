namespace Veggerby.Ignition;

/// <summary>
/// Configuration options controlling ignition (startup readiness) behavior.
/// </summary>
public sealed class IgnitionOptions
{
    /// <summary>
    /// Maximum total duration allowed for all ignition signals before the global timeout deadline elapses.
    /// By default this is a soft deadline: execution continues unless <see cref="CancelOnGlobalTimeout"/> is true.
    /// </summary>
    public TimeSpan GlobalTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Policy determining how failures or timeouts influence startup continuation.
    /// </summary>
    public IgnitionPolicy Policy { get; set; } = IgnitionPolicy.BestEffort;

    /// <summary>
    /// Enables Activity tracing for ignition execution when true.
    /// </summary>
    public bool EnableTracing { get; set; } = false;

    /// <summary>
    /// When true, logs the slowest ignition signals upon completion.
    /// </summary>
    public bool LogTopSlowHandles { get; set; } = true;

    /// <summary>
    /// Number of slow ignition signals to log when <see cref="LogTopSlowHandles"/> is enabled.
    /// </summary>
    public int SlowHandleLogCount { get; set; } = 3;

    /// <summary>
    /// Execution mode controlling scheduling strategy (parallel or sequential).
    /// </summary>
    public IgnitionExecutionMode ExecutionMode { get; set; } = IgnitionExecutionMode.Parallel;

    /// <summary>
    /// When set &gt; 0 and <see cref="ExecutionMode"/> is <see cref="IgnitionExecutionMode.Parallel"/>, limits the number of concurrently awaited signals.
    /// Ignored for sequential execution.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// When true, the global timeout becomes a hard deadline: outstanding signals are cancelled and the overall result is marked timed out.
    /// When false, the coordinator continues waiting and only marks timed out if any individual signal times out.
    /// </summary>
    public bool CancelOnGlobalTimeout { get; set; } = false;

    /// <summary>
    /// When true, a per-signal timeout cancels that specific signal's task; otherwise the timeout is classified without forcing cancellation.
    /// </summary>
    public bool CancelIndividualOnTimeout { get; set; } = false;
}