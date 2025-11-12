using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition;

/// <summary>
/// Default implementation of <see cref="IIgnitionCoordinator"/> that evaluates registered ignition signals,
/// applying timeouts, policies, optional tracing and diagnostic logging.
/// </summary>
public sealed class IgnitionCoordinator : IIgnitionCoordinator
{
    private const string ActivitySourceName = "Veggerby.Ignition.IgnitionCoordinator";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private readonly IReadOnlyList<IIgnitionSignal> _handles;
    private readonly IgnitionOptions _options;
    private readonly ILogger<IgnitionCoordinator> _logger;
    private readonly Lazy<Task<IgnitionResult>> _lazyRun;

    /// <summary>
    /// Creates a new coordinator instance.
    /// </summary>
    /// <param name="handles">The collection of ignition signals to evaluate.</param>
    /// <param name="options">Configured ignition options.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public IgnitionCoordinator(IEnumerable<IIgnitionSignal> handles, IOptions<IgnitionOptions> options, ILogger<IgnitionCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _handles = handles.ToList();
        _options = options.Value;
        _logger = logger;
        _lazyRun = new Lazy<Task<IgnitionResult>>(ExecuteAsync, isThreadSafe: true);
    }

    /// <summary>
    /// Await completion of all signals (or timeout) according to configured options.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation to abandon waiting early.</param>
    public Task WaitAllAsync(CancellationToken cancellationToken = default)
        => _lazyRun.Value.WaitAsync(cancellationToken);
    /// <summary>
    /// Retrieve the aggregated ignition result (cached after first execution).
    /// </summary>
    public Task<IgnitionResult> GetResultAsync() => _lazyRun.Value;

    /// <summary>
    /// Executes ignition evaluation once. Applies the following semantics:
    /// 1. GlobalTimeout is treated as a soft deadline unless <see cref="IgnitionOptions.CancelOnGlobalTimeout"/> is true. When soft, the coordinator continues awaiting outstanding signals.
    /// 2. If the global timeout elapses with cancellation enabled, the overall result is marked timed out; incomplete signals are classified as timed out or failed based on cancellation state.
    /// 3. Individual per-signal timeouts are always enforced and may optionally cancel their underlying tasks when <see cref="IgnitionOptions.CancelIndividualOnTimeout"/> is true.
    /// 4. FailFast policy: Sequential mode throws immediately on first failure. Parallel mode aggregates failures and throws after all started signals finish (unless cancelled by global timeout).
    /// </summary>
    private async Task<IgnitionResult> ExecuteAsync()
    {
        if (_handles.Count == 0)
        {
            _logger.LogDebug("No startup wait handles registered; continuing immediately.");
            return IgnitionResult.EmptySuccess;
        }

        using var activity = _options.EnableTracing ? ActivitySource.StartActivity("Ignition.WaitAll") : null;

        _logger.LogDebug("Awaiting readiness of {Count} startup handle(s) using mode {Mode}.", _handles.Count, _options.ExecutionMode);
        var swGlobal = Stopwatch.StartNew();

        using var globalCts = new CancellationTokenSource();
        // Do not attach cancellation token; we want pure elapsed-time behavior independent of later cancellation.
        var globalTimeoutTask = Task.Delay(_options.GlobalTimeout);

        var (signalTasks, globalTimedOut) = _options.ExecutionMode == IgnitionExecutionMode.Sequential
            ? await RunSequentialAsync(globalCts, globalTimeoutTask)
            : await RunParallelAsync(globalCts, globalTimeoutTask);

        // Cancel timeout delay if completed
        globalCts.Cancel();

        // Build results snapshot. Unfinished tasks appear as placeholder results when a hard global cancellation occurred.
        List<IgnitionSignalResult> results;
        if (globalTimedOut && _options.CancelOnGlobalTimeout)
        {
            // Hard timeout: map each original handle name for clarity.
            results = new List<IgnitionSignalResult>(signalTasks.Count);
            for (int i = 0; i < signalTasks.Count; i++)
            {
                var task = signalTasks[i];
                var handleName = _handles[i].Name;
                if (task.IsCompletedSuccessfully)
                {
                    results.Add(task.Result);
                }
                else
                {
                    // Treat cancelled/incomplete as timed out.
                    results.Add(new IgnitionSignalResult(handleName, IgnitionSignalStatus.TimedOut, TimeSpan.Zero, task.Exception));
                }
            }
        }
        else
        {
            // Build results from non-hard-timeout scenario
            results = new List<IgnitionSignalResult>(signalTasks.Count);
            for (int i = 0; i < signalTasks.Count; i++)
            {
                var task = signalTasks[i];
                if (task.IsCompletedSuccessfully)
                {
                    results.Add(task.Result);
                }
                else
                {
                    var status = task.IsCanceled ? IgnitionSignalStatus.TimedOut : IgnitionSignalStatus.Failed;
                    results.Add(new IgnitionSignalResult("unknown", status, TimeSpan.Zero, task.Exception));
                }
            }
        }
        // Option B semantics: a pure global timeout does not mark TimedOut unless any individual handle timed out (i.e. hard cancellation or per-signal timeout).
        if (globalTimedOut)
        {
            if (_options.CancelOnGlobalTimeout)
            {
                // Hard global timeout => always classify as timed out.
                return IgnitionResult.FromTimeout(results, swGlobal.Elapsed);
            }
            // Soft timeout: only classify if any per-signal timed out.
            bool hasTimedOut = false;
            foreach (var r in results)
            {
                if (r.Status == IgnitionSignalStatus.TimedOut)
                {
                    hasTimedOut = true;
                    break;
                }
            }

            if (hasTimedOut)
            {
                return IgnitionResult.FromTimeout(results, swGlobal.Elapsed);
            }
            _logger.LogWarning("Global timeout elapsed (soft) with no per-signal timeouts; treating as success per Option B semantics.");
            return IgnitionResult.FromResults(results, swGlobal.Elapsed);
        }

        // Collect failed results with explicit loop
        var failed = new List<IgnitionSignalResult>();
        foreach (var r in results)
        {
            if (r.Status == IgnitionSignalStatus.Failed)
            {
                failed.Add(r);
            }
        }

        if (failed.Count > 0)
        {
            foreach (var f in failed)
            {
                _logger.LogError(f.Exception, "Startup handle '{Name}' failed after {Ms} ms.", f.Name, f.Duration.TotalMilliseconds);
            }

            if (_options.Policy == IgnitionPolicy.FailFast && _options.ExecutionMode == IgnitionExecutionMode.Parallel)
            {
                // In parallel mode we aggregate and throw.
                var exceptions = new List<Exception>();
                foreach (var f in failed)
                {
                    if (f.Exception is not null)
                    {
                        exceptions.Add(f.Exception);
                    }
                }
                throw new AggregateException(exceptions);
            }
        }

        if (_options.LogTopSlowHandles)
        {
            foreach (var s in results.OrderByDescending(r => r.Duration).Take(_options.SlowHandleLogCount))
            {
                _logger.LogDebug("Startup handle '{Name}' took {Ms} ms.", s.Name, s.Duration.TotalMilliseconds);
            }
        }

        _logger.LogDebug("Startup readiness finished in {Ms} ms (failures: {Failures}).", swGlobal.Elapsed.TotalMilliseconds, failed.Count);
        return IgnitionResult.FromResults(results, swGlobal.Elapsed);
    }

    private async Task<(List<Task<IgnitionSignalResult>> Tasks, bool TimedOut)> RunSequentialAsync(CancellationTokenSource globalCts, Task globalTimeoutTask)
    {
        var list = new List<Task<IgnitionSignalResult>>();
        foreach (var h in _handles)
        {
            var t = WaitOneAsync(h, globalCts.Token);
            list.Add(t);
            var completed = await Task.WhenAny(t, globalTimeoutTask);
            if (completed == globalTimeoutTask)
            {
                if (_options.CancelOnGlobalTimeout)
                {
                    _logger.LogWarning("Global timeout after {Seconds:F1}s during sequential execution (cancelling).", _options.GlobalTimeout.TotalSeconds);
                    globalCts.Cancel();
                    return (list, true); // timed out only when we cancel
                }
                else
                {
                    // Option B: ignore and await the handle to finish.
                    _logger.LogDebug("Global timeout elapsed in sequential mode but CancelOnGlobalTimeout=false; continuing to await '{Name}'.", h.Name);
                    await t; // ensure completion before proceeding
                }
            }

            if (_options.Policy == IgnitionPolicy.FailFast && t.IsCompleted && t.Result.Status == IgnitionSignalStatus.Failed)
            {
                _logger.LogError(t.Result.Exception, "Ignition signal '{Name}' failed in sequential mode; aborting.", h.Name);
                // Fail-fast sequential semantics: throw immediately with the single failure.
                throw new AggregateException(t.Result.Exception!);
            }
        }
        return (list, false);
    }

    private async Task<(List<Task<IgnitionSignalResult>> Tasks, bool TimedOut)> RunParallelAsync(CancellationTokenSource globalCts, Task globalTimeoutTask)
    {
        var list = new List<Task<IgnitionSignalResult>>();
        SemaphoreSlim? gate = null;
        if (_options.MaxDegreeOfParallelism.HasValue && _options.MaxDegreeOfParallelism > 0)
        {
            gate = new SemaphoreSlim(_options.MaxDegreeOfParallelism.Value);
        }

        foreach (var h in _handles)
        {
            if (gate is not null)
            {
                await gate.WaitAsync(globalCts.Token);
            }

            var task = Task.Run(async () =>
            {
                try
                {
                    return await WaitOneAsync(h, globalCts.Token);
                }
                finally
                {
                    gate?.Release();
                }
            }, globalCts.Token);

            list.Add(task);
        }

        var allTask = Task.WhenAll(list);
        var completed = await Task.WhenAny(allTask, globalTimeoutTask);
        if (completed == globalTimeoutTask)
        {
            if (_options.CancelOnGlobalTimeout)
            {
                _logger.LogWarning("Startup readiness global timeout after {Seconds:F1}s (cancelling outstanding handles).", _options.GlobalTimeout.TotalSeconds);
                globalCts.Cancel();
                return (list, true);
            }
            else
            {
                _logger.LogDebug("Global timeout elapsed but CancelOnGlobalTimeout=false; waiting for remaining parallel handles.");
                try { await allTask; } catch { /* swallow */ }
                return (list, false);
            }
        }

        try
        {
            await allTask; // exceptions handled by caller
        }
        catch
        {
            // swallow: results gathered later
        }
        return (list, false);
    }

    private async Task<IgnitionSignalResult> WaitOneAsync(IIgnitionSignal h, CancellationToken globalToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var perHandleCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            Task work = h.WaitAsync(perHandleCts.Token);

            if (h.Timeout.HasValue)
            {
                var timeoutTask = Task.Delay(h.Timeout.Value, perHandleCts.Token);
                var completed = await Task.WhenAny(work, timeoutTask);
                if (completed == timeoutTask)
                {
                    if (_options.CancelIndividualOnTimeout)
                    {
                        perHandleCts.Cancel();
                    }
                    return new IgnitionSignalResult(h.Name, IgnitionSignalStatus.TimedOut, sw.Elapsed);
                }
            }

            await work; // propagate exceptions if failed
            return new IgnitionSignalResult(h.Name, IgnitionSignalStatus.Succeeded, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            // Treat cancellations (global or individual) as timeouts for classification.
            return new IgnitionSignalResult(h.Name, IgnitionSignalStatus.TimedOut, sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new IgnitionSignalResult(h.Name, IgnitionSignalStatus.Failed, sw.Elapsed, ex);
        }
    }
}
