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
    private readonly IIgnitionGraph? _graph;
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
        : this(handles, graph: null, options, logger)
    {
    }

    /// <summary>
    /// Creates a new coordinator instance with optional dependency graph.
    /// </summary>
    /// <param name="handles">The collection of ignition signals to evaluate.</param>
    /// <param name="graph">Optional dependency graph for dependency-aware execution.</param>
    /// <param name="options">Configured ignition options.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public IgnitionCoordinator(IEnumerable<IIgnitionSignal> handles, IIgnitionGraph? graph, IOptions<IgnitionOptions> options, ILogger<IgnitionCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(handles);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _handles = handles.ToList();
        _graph = graph;
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

        var (signalTasks, globalTimedOut) = _options.ExecutionMode switch
        {
            IgnitionExecutionMode.Sequential => await RunSequentialAsync(globalCts, globalTimeoutTask),
            IgnitionExecutionMode.DependencyAware => await RunDependencyAwareAsync(globalCts, globalTimeoutTask),
            _ => await RunParallelAsync(globalCts, globalTimeoutTask)
        };

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

    private async Task<(List<Task<IgnitionSignalResult>> Tasks, bool TimedOut)> RunDependencyAwareAsync(CancellationTokenSource globalCts, Task globalTimeoutTask)
    {
        if (_graph is null)
        {
            throw new InvalidOperationException(
                "Dependency-aware execution mode requires an IIgnitionGraph to be registered. " +
                "Use IgnitionGraphBuilder to create a graph and register it in the DI container.");
        }

        _logger.LogDebug("Starting dependency-aware execution for {Count} signal(s).", _graph.Signals.Count);

        var syncLock = new object();
        var results = new Dictionary<IIgnitionSignal, Task<IgnitionSignalResult>>();
        var completed = new Dictionary<IIgnitionSignal, IgnitionSignalResult>();
        var failed = new HashSet<IIgnitionSignal>();
        var skipped = new HashSet<IIgnitionSignal>();

        // Track signals by their dependency count
        var pendingDependencies = new Dictionary<IIgnitionSignal, int>();
        foreach (var signal in _graph.Signals)
        {
            var deps = _graph.GetDependencies(signal);
            pendingDependencies[signal] = deps.Count;
        }

        // Queue for signals ready to execute
        var readyQueue = new Queue<IIgnitionSignal>();
        foreach (var signal in _graph.Signals)
        {
            if (pendingDependencies[signal] == 0)
            {
                readyQueue.Enqueue(signal);
            }
        }

        // Execute signals as their dependencies complete
        var activeTasks = new List<Task>();
        SemaphoreSlim? gate = null;
        if (_options.MaxDegreeOfParallelism.HasValue && _options.MaxDegreeOfParallelism > 0)
        {
            gate = new SemaphoreSlim(_options.MaxDegreeOfParallelism.Value);
        }

        while (true)
        {
            IIgnitionSignal? signalToStart = null;
            
            lock (syncLock)
            {
                if (readyQueue.Count == 0 && activeTasks.Count == 0)
                {
                    break; // All done
                }

                if (readyQueue.Count > 0)
                {
                    signalToStart = readyQueue.Dequeue();
                }
            }

            if (signalToStart is not null)
            {
                bool gateAcquired = false;
                try
                {
                    if (gate is not null)
                    {
                        await gate.WaitAsync(globalCts.Token);
                        gateAcquired = true;
                    }

                    var signal = signalToStart;

                    // Check if any dependencies failed
                    var deps = _graph.GetDependencies(signal);
                    var failedDeps = new List<string>();
                    lock (syncLock)
                    {
                        foreach (var dep in deps)
                        {
                            if (failed.Contains(dep))
                            {
                                failedDeps.Add(dep.Name);
                            }
                        }
                    }

                    if (failedDeps.Count > 0)
                    {
                        // Skip this signal due to failed dependencies
                        var skipResult = new IgnitionSignalResult(
                            signal.Name,
                            IgnitionSignalStatus.Skipped,
                            TimeSpan.Zero,
                            Exception: null,
                            FailedDependencies: failedDeps);
                        
                        lock (syncLock)
                        {
                            completed[signal] = skipResult;
                            skipped.Add(signal);
                            
                            // Mark dependents as ready if all their other dependencies are done
                            foreach (var dependent in _graph.GetDependents(signal))
                            {
                                pendingDependencies[dependent]--;
                                if (pendingDependencies[dependent] == 0)
                                {
                                    readyQueue.Enqueue(dependent);
                                }
                            }
                        }

                        _logger.LogWarning(
                            "Skipping signal '{Name}' due to failed dependencies: {Dependencies}",
                            signal.Name,
                            string.Join(", ", failedDeps));
                        continue;
                    }

                    // Start the signal - gate released in task's finally block
                    gateAcquired = false; // Transfer ownership to task
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await WaitOneAsync(signal, globalCts.Token);
                            lock (syncLock)
                            {
                                completed[signal] = result;
                                if (result.Status == IgnitionSignalStatus.Failed || result.Status == IgnitionSignalStatus.TimedOut)
                                {
                                    failed.Add(signal);

                                    if (_options.Policy == IgnitionPolicy.FailFast)
                                    {
                                        _logger.LogError(result.Exception, "Signal '{Name}' failed in dependency-aware mode; aborting per FailFast policy.", signal.Name);
                                        globalCts.Cancel();
                                    }
                                }

                                // Notify dependents
                                foreach (var dependent in _graph.GetDependents(signal))
                                {
                                    pendingDependencies[dependent]--;
                                    if (pendingDependencies[dependent] == 0)
                                    {
                                        readyQueue.Enqueue(dependent);
                                    }
                                }
                            }
                            return result;
                        }
                        finally
                        {
                            gate?.Release();
                        }
                    }, globalCts.Token);

                    lock (syncLock)
                    {
                        results[signal] = task;
                        activeTasks.Add(task);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Global cancellation occurred while waiting for gate
                    if (gateAcquired)
                    {
                        gate?.Release();
                    }
                    throw;
                }
            }
            else if (activeTasks.Count > 0)
            {
                // Wait for at least one task to complete or global timeout
                Task<Task> anyTask;
                lock (syncLock)
                {
                    anyTask = Task.WhenAny(activeTasks);
                }
                
                var completedTask = await Task.WhenAny(anyTask, globalTimeoutTask);

                if (completedTask == globalTimeoutTask)
                {
                    if (_options.CancelOnGlobalTimeout)
                    {
                        _logger.LogWarning("Global timeout in dependency-aware execution (cancelling).");
                        globalCts.Cancel();

                        // Wait for all active tasks to finish
                        Task[] tasksSnapshot;
                        lock (syncLock)
                        {
                            tasksSnapshot = activeTasks.ToArray();
                        }
                        
                        try
                        {
                            await Task.WhenAll(tasksSnapshot);
                        }
                        catch
                        {
                            // Exceptions handled in results
                        }

                        // Build final results list
                        var taskList = new List<Task<IgnitionSignalResult>>();
                        foreach (var signal in _graph.Signals)
                        {
                            if (results.TryGetValue(signal, out var t))
                            {
                                taskList.Add(t);
                            }
                            else
                            {
                                // Signal never started
                                var timedOutResult = Task.FromResult(
                                    new IgnitionSignalResult(signal.Name, IgnitionSignalStatus.TimedOut, TimeSpan.Zero));
                                taskList.Add(timedOutResult);
                            }
                        }

                        return (taskList, true);
                    }
                    else
                    {
                        _logger.LogDebug("Global timeout in dependency-aware mode but CancelOnGlobalTimeout=false; continuing.");
                    }
                }
                else
                {
                    // Remove completed task from active list
                    var finished = await anyTask;
                    lock (syncLock)
                    {
                        activeTasks.Remove(finished);
                    }
                }
            }
        }

        // Build final results in original graph order
        var finalResults = new List<Task<IgnitionSignalResult>>();
        foreach (var signal in _graph.Signals)
        {
            if (results.TryGetValue(signal, out var task))
            {
                finalResults.Add(task);
            }
            else if (completed.TryGetValue(signal, out var result))
            {
                finalResults.Add(Task.FromResult(result));
            }
            else
            {
                // Should not happen, but handle defensively
                finalResults.Add(Task.FromResult(
                    new IgnitionSignalResult(signal.Name, IgnitionSignalStatus.Failed, TimeSpan.Zero,
                        new InvalidOperationException("Signal was not executed"))));
            }
        }

        return (finalResults, false);
    }

    private async Task<IgnitionSignalResult> WaitOneAsync(IIgnitionSignal h, CancellationToken globalToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var perHandleCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
            Task work = h.WaitAsync(perHandleCts.Token);

            // Determine timeout and cancellation behavior from strategy or defaults
            TimeSpan? effectiveTimeout;
            bool cancelOnTimeout;

            if (_options.TimeoutStrategy is not null)
            {
                (effectiveTimeout, cancelOnTimeout) = _options.TimeoutStrategy.GetTimeout(h, _options);
            }
            else
            {
                effectiveTimeout = h.Timeout;
                cancelOnTimeout = _options.CancelIndividualOnTimeout;
            }

            if (effectiveTimeout.HasValue)
            {
                var timeoutTask = Task.Delay(effectiveTimeout.Value, perHandleCts.Token);
                var completed = await Task.WhenAny(work, timeoutTask);
                if (completed == timeoutTask)
                {
                    if (cancelOnTimeout)
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
