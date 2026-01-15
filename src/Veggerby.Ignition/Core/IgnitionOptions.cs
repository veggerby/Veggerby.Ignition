using Veggerby.Ignition.Metrics;
using Veggerby.Ignition.Stages;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options controlling ignition (startup readiness) behavior.
/// </summary>
public sealed class IgnitionOptions
{
    private TimeSpan _globalTimeout = TimeSpan.FromSeconds(5);
    private int _slowHandleLogCount = 3;
    private int? _maxDegreeOfParallelism;
    private double _earlyPromotionThreshold = 1.0;

    /// <summary>
    /// Maximum total duration allowed for all ignition signals before the global timeout deadline elapses.
    /// By default this is a soft deadline: execution continues unless <see cref="CancelOnGlobalTimeout"/> is true.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public TimeSpan GlobalTimeout
    {
        get => _globalTimeout;
        set
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Global timeout cannot be negative.");
            }

            _globalTimeout = value;
        }
    }

    private IIgnitionPolicy? _customPolicy;

    /// <summary>
    /// Policy determining how failures or timeouts influence startup continuation.
    /// </summary>
    /// <remarks>
    /// For custom behavior, use <see cref="CustomPolicy"/> instead.
    /// When <see cref="CustomPolicy"/> is set, this property is ignored.
    /// </remarks>
    public IgnitionPolicy Policy { get; set; } = IgnitionPolicy.BestEffort;

    /// <summary>
    /// Gets or sets a custom policy implementation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this overrides the <see cref="Policy"/> property, enabling custom failure handling strategies
    /// beyond the built-in policies (FailFast, BestEffort, ContinueOnTimeout).
    /// </para>
    /// <para>
    /// Custom policies enable domain-specific logic such as retry strategies, circuit breakers,
    /// conditional fail-fast, and percentage-based thresholds.
    /// </para>
    /// <para>
    /// When <c>null</c>, the <see cref="Policy"/> enum value is used to select a built-in policy implementation.
    /// </para>
    /// </remarks>
    public IIgnitionPolicy? CustomPolicy
    {
        get => _customPolicy;
        set => _customPolicy = value;
    }

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a negative value.</exception>
    public int SlowHandleLogCount
    {
        get => _slowHandleLogCount;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Slow handle log count cannot be negative.");
            }

            _slowHandleLogCount = value;
        }
    }

    /// <summary>
    /// Execution mode controlling scheduling strategy (parallel or sequential).
    /// </summary>
    public IgnitionExecutionMode ExecutionMode { get; set; } = IgnitionExecutionMode.Parallel;

    /// <summary>
    /// When set &gt; 0 and <see cref="ExecutionMode"/> is <see cref="IgnitionExecutionMode.Parallel"/>, limits the number of concurrently awaited signals.
    /// Ignored for sequential execution.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to zero or negative value.</exception>
    public int? MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set
        {
            if (value.HasValue && value.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Max degree of parallelism must be greater than zero when specified.");
            }

            _maxDegreeOfParallelism = value;
        }
    }

    /// <summary>
    /// When true, the global timeout becomes a hard deadline: outstanding signals are cancelled and the overall result is marked timed out.
    /// When false, the coordinator continues waiting and only marks timed out if any individual signal times out.
    /// </summary>
    public bool CancelOnGlobalTimeout { get; set; } = false;

    /// <summary>
    /// When true, a per-signal timeout cancels that specific signal's task; otherwise the timeout is classified without forcing cancellation.
    /// </summary>
    /// <remarks>
    /// This property provides a global default for cancellation behavior. When a custom <see cref="TimeoutStrategy"/>
    /// is configured, that strategy's <c>cancelImmediately</c> return value takes precedence over this setting.
    /// </remarks>
    public bool CancelIndividualOnTimeout { get; set; } = false;

    /// <summary>
    /// Optional pluggable timeout strategy for determining per-signal timeout and cancellation behavior.
    /// When <c>null</c>, the default behavior uses <see cref="IIgnitionSignal.Timeout"/> and <see cref="CancelIndividualOnTimeout"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Custom strategies enable advanced scenarios such as:
    /// <list type="bullet">
    ///   <item>Exponential scaling based on failure count</item>
    ///   <item>Adaptive timeouts (e.g., slow I/O detection)</item>
    ///   <item>Dynamic per-stage deadlines</item>
    ///   <item>User-defined per-class or per-assembly defaults</item>
    /// </list>
    /// </para>
    /// <para>
    /// When set, the strategy's <see cref="IIgnitionTimeoutStrategy.GetTimeout"/> method is invoked for each signal
    /// to determine the effective timeout duration and whether to cancel on timeout.
    /// </para>
    /// </remarks>
    public IIgnitionTimeoutStrategy? TimeoutStrategy { get; set; }

    /// <summary>
    /// When <c>true</c> and using <see cref="IgnitionExecutionMode.DependencyAware"/>, cancels all dependent signals
    /// when a dependency fails (not just skips them). This enables hierarchical cancellation propagation through the dependency graph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default (when <c>false</c>), dependent signals are marked as <see cref="IgnitionSignalStatus.Skipped"/>
    /// when their dependencies fail. When set to <c>true</c>, the coordinator actively cancels the pending dependent
    /// signals, providing accurate reporting such as "Signal X cancelled due to dependency failure of Y".
    /// </para>
    /// <para>
    /// This setting only affects signals that have not yet started. Signals that are already running are not cancelled
    /// unless they implement <see cref="IScopedIgnitionSignal"/> with appropriate scope configuration.
    /// </para>
    /// </remarks>
    public bool CancelDependentsOnFailure { get; set; } = false;

    /// <summary>
    /// Policy determining how stage transitions are handled during staged execution.
    /// Only used when <see cref="ExecutionMode"/> is <see cref="IgnitionExecutionMode.Staged"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls when the coordinator moves from one stage to the next:
    /// <list type="bullet">
    ///   <item><see cref="IgnitionStagePolicy.AllMustSucceed"/>: All signals in the current stage must succeed before proceeding (default)</item>
    ///   <item><see cref="IgnitionStagePolicy.BestEffort"/>: Proceed when all signals complete, regardless of status</item>
    ///   <item><see cref="IgnitionStagePolicy.FailFast"/>: Stop immediately if any signal fails</item>
    ///   <item><see cref="IgnitionStagePolicy.EarlyPromotion"/>: Proceed when <see cref="EarlyPromotionThreshold"/> percentage of signals succeed</item>
    /// </list>
    /// </para>
    /// </remarks>
    public IgnitionStagePolicy StagePolicy { get; set; } = IgnitionStagePolicy.AllMustSucceed;

    /// <summary>
    /// Threshold (0.0 to 1.0) for early stage promotion. When <see cref="StagePolicy"/> is <see cref="IgnitionStagePolicy.EarlyPromotion"/>,
    /// the next stage starts when this percentage of signals in the current stage have succeeded.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when set to a value outside the range [0.0, 1.0].</exception>
    /// <remarks>
    /// <para>
    /// For example, setting this to 0.8 means the next stage starts when 80% of the current stage's signals succeed.
    /// Remaining signals in the current stage continue executing but don't block progression.
    /// </para>
    /// <para>
    /// Default is 1.0 (100%), meaning all signals must succeed before promotion when using <see cref="IgnitionStagePolicy.EarlyPromotion"/>.
    /// </para>
    /// </remarks>
    public double EarlyPromotionThreshold
    {
        get => _earlyPromotionThreshold;
        set
        {
            if (value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Early promotion threshold must be between 0.0 and 1.0.");
            }

            _earlyPromotionThreshold = value;
        }
    }

    /// <summary>
    /// Optional metrics adapter for recording ignition performance and outcome data.
    /// When <c>null</c>, no metrics are recorded (zero overhead).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Configuring a metrics implementation enables integration with observability systems
    /// (OpenTelemetry, Prometheus, App Metrics, etc.) without adding any of them as dependencies.
    /// </para>
    /// <para>
    /// The adapter records:
    /// <list type="bullet">
    ///   <item>Per-signal duration and status</item>
    ///   <item>Total ignition duration</item>
    /// </list>
    /// </para>
    /// <para>
    /// Implementations should be thread-safe as metrics may be recorded from multiple concurrent signals.
    /// </para>
    /// </remarks>
    public IIgnitionMetrics? Metrics { get; set; }

    /// <summary>
    /// Optional lifecycle hooks for observing ignition execution stages.
    /// When <c>null</c>, no hooks are invoked (zero overhead).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lifecycle hooks enable custom logic at key points during ignition execution:
    /// <list type="bullet">
    ///   <item>Before/after the entire ignition process</item>
    ///   <item>Before/after each individual signal</item>
    /// </list>
    /// </para>
    /// <para>
    /// Hooks provide read-only observation and cannot modify ignition behavior or results.
    /// Exceptions thrown by hooks are caught and logged but do not affect ignition outcome.
    /// </para>
    /// <para>
    /// Common use cases include telemetry enrichment, logging, cleanup, and integration with external systems.
    /// </para>
    /// </remarks>
    public IIgnitionLifecycleHooks? LifecycleHooks { get; set; }

    /// <summary>
    /// Gets the effective policy to use for ignition execution.
    /// </summary>
    /// <returns>
    /// The custom policy if <see cref="CustomPolicy"/> is set; otherwise, a built-in policy implementation
    /// corresponding to the <see cref="Policy"/> enum value.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides backward compatibility by mapping the <see cref="Policy"/> enum to
    /// <see cref="IIgnitionPolicy"/> implementations:
    /// <list type="bullet">
    ///   <item><see cref="IgnitionPolicy.FailFast"/> maps to <see cref="FailFastPolicy"/></item>
    ///   <item><see cref="IgnitionPolicy.BestEffort"/> maps to <see cref="BestEffortPolicy"/></item>
    ///   <item><see cref="IgnitionPolicy.ContinueOnTimeout"/> maps to <see cref="ContinueOnTimeoutPolicy"/></item>
    /// </list>
    /// </para>
    /// <para>
    /// When <see cref="CustomPolicy"/> is set, it takes precedence over the <see cref="Policy"/> enum value.
    /// </para>
    /// </remarks>
    internal IIgnitionPolicy GetEffectivePolicy()
    {
        if (_customPolicy is not null)
        {
            return _customPolicy;
        }

        // Map built-in enum to IIgnitionPolicy implementation
        return Policy switch
        {
            IgnitionPolicy.FailFast => new FailFastPolicy(),
            IgnitionPolicy.BestEffort => new BestEffortPolicy(),
            IgnitionPolicy.ContinueOnTimeout => new ContinueOnTimeoutPolicy(),
            _ => new BestEffortPolicy()
        };
    }
}
