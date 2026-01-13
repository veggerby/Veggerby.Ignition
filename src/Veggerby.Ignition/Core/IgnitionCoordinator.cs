using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Veggerby.Ignition.Stages;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Default implementation of <see cref="IIgnitionCoordinator"/> that evaluates registered ignition signals,
/// applying timeouts, policies, optional tracing and diagnostic logging.
/// </summary>
public sealed class IgnitionCoordinator : IIgnitionCoordinator
{
    private const string ActivitySourceName = "Veggerby.Ignition.IgnitionCoordinator";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private readonly IReadOnlyList<IIgnitionSignalFactory> _factories;
    private readonly IServiceProvider _serviceProvider;
    private readonly Lazy<IReadOnlyList<IIgnitionSignal>> _handles;
    private readonly IIgnitionGraph? _graph;
    private readonly IgnitionOptions _options;
    private readonly ILogger<IgnitionCoordinator> _logger;
    private readonly Lazy<Task<IgnitionResult>> _lazyRun;
    private readonly object _stateLock = new();
    private volatile IgnitionState _state = IgnitionState.NotStarted;

    /// <summary>
    /// Creates a new coordinator instance.
    /// </summary>
    /// <param name="factories">The collection of ignition signal factories for creating signals.</param>
    /// <param name="serviceProvider">Service provider for resolving dependencies when creating signals from factories.</param>
    /// <param name="options">Configured ignition options.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public IgnitionCoordinator(
        IEnumerable<IIgnitionSignalFactory> factories,
        IServiceProvider serviceProvider,
        IOptions<IgnitionOptions> options,
        ILogger<IgnitionCoordinator> logger)
        : this(factories, serviceProvider, graph: null, options, logger)
    {
    }

    /// <summary>
    /// Creates a new coordinator instance with optional dependency graph.
    /// </summary>
    /// <param name="factories">The collection of ignition signal factories for creating signals.</param>
    /// <param name="serviceProvider">Service provider for resolving dependencies when creating signals from factories.</param>
    /// <param name="graph">Optional dependency graph for dependency-aware execution.</param>
    /// <param name="options">Configured ignition options.</param>
    /// <param name="logger">Logger used for diagnostic output.</param>
    public IgnitionCoordinator(
        IEnumerable<IIgnitionSignalFactory> factories,
        IServiceProvider serviceProvider,
        IIgnitionGraph? graph,
        IOptions<IgnitionOptions> options,
        ILogger<IgnitionCoordinator> logger)
    {
        ArgumentNullException.ThrowIfNull(factories);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _factories = factories.ToList();
        _serviceProvider = serviceProvider;
        _graph = graph;
        _options = options.Value;
        _logger = logger;
        _handles = new Lazy<IReadOnlyList<IIgnitionSignal>>(CreateAllSignals, isThreadSafe: true);
        _lazyRun = new Lazy<Task<IgnitionResult>>(ExecuteAsync, isThreadSafe: true);
    }

    /// <summary>
    /// Creates all ignition signals from factories.
    /// </summary>
    private IReadOnlyList<IIgnitionSignal> CreateAllSignals()
    {
        var signals = new List<IIgnitionSignal>(_factories.Count);

        foreach (var factory in _factories)
        {
            var signal = factory.CreateSignal(_serviceProvider);
            signals.Add(signal);
        }

        return signals;
    }

    /// <inheritdoc/>
    public IgnitionState State => _state;

    /// <inheritdoc/>
    public event EventHandler<IgnitionSignalStartedEventArgs>? SignalStarted;

    /// <inheritdoc/>
    public event EventHandler<IgnitionSignalCompletedEventArgs>? SignalCompleted;

    /// <inheritdoc/>
    public event EventHandler<IgnitionGlobalTimeoutEventArgs>? GlobalTimeoutReached;

    /// <inheritdoc/>
    public event EventHandler<IgnitionCoordinatorCompletedEventArgs>? CoordinatorCompleted;

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
        TransitionToState(IgnitionState.Running);

        if (_handles.Value.Count == 0)
        {
            _logger.LogDebug("No startup wait handles registered; continuing immediately.");
            var emptyResult = IgnitionResult.EmptySuccess;
            TransitionToFinalState(emptyResult);
            return emptyResult;
        }

        using var activity = _options.EnableTracing ? ActivitySource.StartActivity("Ignition.WaitAll") : null;

        _logger.LogDebug("Awaiting readiness of {Count} startup handle(s) using mode {Mode}.", _handles.Value.Count, _options.ExecutionMode);
        var swGlobal = Stopwatch.StartNew();

        using var globalCts = new CancellationTokenSource();
        // Do not attach cancellation token; we want pure elapsed-time behavior independent of later cancellation.
        var globalTimeoutTask = Task.Delay(_options.GlobalTimeout);

        var (signalTasks, globalTimedOut) = _options.ExecutionMode switch
        {
            IgnitionExecutionMode.Sequential => await RunSequentialAsync(globalCts, globalTimeoutTask, swGlobal),
            IgnitionExecutionMode.DependencyAware => await RunDependencyAwareAsync(globalCts, globalTimeoutTask, swGlobal),
            IgnitionExecutionMode.Staged => (new List<Task<IgnitionSignalResult>>(), false), // Handled separately
            _ => await RunParallelAsync(globalCts, globalTimeoutTask, swGlobal)
        };

        // Staged execution has its own result construction path
        if (_options.ExecutionMode == IgnitionExecutionMode.Staged)
        {
            var stagedResult = await RunStagedAsync(globalCts, globalTimeoutTask, swGlobal);
            TransitionToFinalState(stagedResult);
            return stagedResult;
        }

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
                var handleName = _handles.Value[i].Name;
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
                var timeoutResult = IgnitionResult.FromTimeout(results, swGlobal.Elapsed);
                TransitionToFinalState(timeoutResult);
                return timeoutResult;
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
                var timeoutResult = IgnitionResult.FromTimeout(results, swGlobal.Elapsed);
                TransitionToFinalState(timeoutResult);
                return timeoutResult;
            }
            _logger.LogWarning("Global timeout elapsed (soft) with no per-signal timeouts; treating as success per Option B semantics.");
            var softTimeoutResult = IgnitionResult.FromResults(results, swGlobal.Elapsed);
            TransitionToFinalState(softTimeoutResult);
            return softTimeoutResult;
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

                // Transition to final state and raise CoordinatorCompleted before throwing.
                // This allows observers to receive the complete result even when FailFast causes an exception.
                var failedResult = IgnitionResult.FromResults(results, swGlobal.Elapsed);
                TransitionToFinalState(failedResult);
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
        var finalResult = IgnitionResult.FromResults(results, swGlobal.Elapsed);
        TransitionToFinalState(finalResult);
        return finalResult;
    }

    private async Task<(List<Task<IgnitionSignalResult>> Tasks, bool TimedOut)> RunSequentialAsync(CancellationTokenSource globalCts, Task globalTimeoutTask, Stopwatch swGlobal)
    {
        var list = new List<Task<IgnitionSignalResult>>();
        bool globalTimeoutRaised = false;

        foreach (var h in _handles.Value)
        {
            RaiseSignalStarted(h.Name);
            var t = WaitOneAsync(h, globalCts.Token, swGlobal);
            list.Add(t);
            var completed = await Task.WhenAny(t, globalTimeoutTask);
            if (completed == globalTimeoutTask)
            {
                if (!globalTimeoutRaised)
                {
                    globalTimeoutRaised = true;
                    RaiseGlobalTimeout(swGlobal.Elapsed, GetPendingSignalNames(list, _handles.Value));
                }

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

            // Raise signal completed event after the task completes
            if (t.IsCompleted)
            {
                RaiseSignalCompleted(t.Result);
            }

            if (_options.Policy == IgnitionPolicy.FailFast && t.IsCompleted && t.Result.Status == IgnitionSignalStatus.Failed)
            {
                _logger.LogError(t.Result.Exception, "Ignition signal '{Name}' failed in sequential mode; aborting.", h.Name);
                // Transition to final state and raise CoordinatorCompleted before throwing.
                // This allows observers to receive the complete result even when FailFast causes an exception.
                var failedResult = IgnitionResult.FromResults(list.Select(task => task.Result).ToList(), swGlobal.Elapsed);
                TransitionToFinalState(failedResult);
                // Fail-fast sequential semantics: throw immediately with the single failure.
                throw new AggregateException(t.Result.Exception!);
            }
        }
        return (list, false);
    }

    private async Task<(List<Task<IgnitionSignalResult>> Tasks, bool TimedOut)> RunParallelAsync(CancellationTokenSource globalCts, Task globalTimeoutTask, Stopwatch swGlobal)
    {
        var list = new List<Task<IgnitionSignalResult>>();
        SemaphoreSlim? gate = null;
        if (_options.MaxDegreeOfParallelism.HasValue && _options.MaxDegreeOfParallelism > 0)
        {
            gate = new SemaphoreSlim(_options.MaxDegreeOfParallelism.Value);
        }

        foreach (var h in _handles.Value)
        {
            if (gate is not null)
            {
                await gate.WaitAsync(globalCts.Token);
            }

            var task = Task.Run(async () =>
            {
                RaiseSignalStarted(h.Name);
                try
                {
                    var result = await WaitOneAsync(h, globalCts.Token, swGlobal);
                    RaiseSignalCompleted(result);
                    return result;
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
            RaiseGlobalTimeout(swGlobal.Elapsed, GetPendingSignalNames(list, _handles.Value));

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

    private async Task<(List<Task<IgnitionSignalResult>> Tasks, bool TimedOut)> RunDependencyAwareAsync(CancellationTokenSource globalCts, Task globalTimeoutTask, Stopwatch swGlobal)
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
        bool globalTimeoutRaised = false;

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
                        // Determine if we should use Cancelled or Skipped status
                        IgnitionSignalStatus skipStatus;
                        CancellationReason cancelReason;
                        string? cancelledBy = null;

                        if (_options.CancelDependentsOnFailure)
                        {
                            skipStatus = IgnitionSignalStatus.Cancelled;
                            cancelReason = CancellationReason.DependencyFailed;
                            // Join all failed dependency names for accurate reporting
                            cancelledBy = failedDeps.Count == 1
                                ? failedDeps[0]
                                : string.Join(", ", failedDeps);
                        }
                        else
                        {
                            skipStatus = IgnitionSignalStatus.Skipped;
                            cancelReason = CancellationReason.None;
                        }

                        // Skip/Cancel this signal due to failed dependencies
                        var skipResult = new IgnitionSignalResult(
                            signal.Name,
                            skipStatus,
                            TimeSpan.Zero,
                            Exception: null,
                            FailedDependencies: failedDeps,
                            CancellationReason: cancelReason,
                            CancelledBySignal: cancelledBy);

                        RaiseSignalCompleted(skipResult);

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

                        var action = _options.CancelDependentsOnFailure ? "Cancelling" : "Skipping";
                        _logger.LogWarning(
                            "{Action} signal '{Name}' due to failed dependencies: {Dependencies}",
                            action,
                            signal.Name,
                            string.Join(", ", failedDeps));
                        continue;
                    }

                    // Start the signal - gate released in task's finally block
                    gateAcquired = false; // Transfer ownership to task
                    var task = Task.Run(async () =>
                    {
                        RaiseSignalStarted(signal.Name);
                        try
                        {
                            var result = await WaitOneAsync(signal, globalCts.Token, swGlobal);
                            RaiseSignalCompleted(result);
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
                    if (!globalTimeoutRaised)
                    {
                        globalTimeoutRaised = true;
                        RaiseGlobalTimeout(swGlobal.Elapsed, GetPendingSignalNamesFromGraph(results, _graph));
                    }

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

    private async Task<IgnitionResult> RunStagedAsync(CancellationTokenSource globalCts, Task globalTimeoutTask, Stopwatch swGlobal)
    {
        _logger.LogDebug("Starting staged execution for {Count} signal(s).", _handles.Value.Count);

        var signalsByStage = GroupSignalsByStage();
        var sortedStages = signalsByStage.Keys.OrderBy(s => s).ToList();

        var context = new StagedExecutionContext();

        SemaphoreSlim? gate = null;
        if (_options.MaxDegreeOfParallelism.HasValue && _options.MaxDegreeOfParallelism > 0)
        {
            gate = new SemaphoreSlim(_options.MaxDegreeOfParallelism.Value);
        }

        try
        {
            foreach (var stageNumber in sortedStages)
            {
                if (context.ShouldStop)
                {
                    MarkStageAsSkipped(signalsByStage[stageNumber], stageNumber, context);
                    continue;
                }

                await ExecuteStageAsync(signalsByStage[stageNumber], stageNumber, globalCts, globalTimeoutTask, swGlobal, gate, context);
            }
        }
        finally
        {
            gate?.Dispose();
        }

        return BuildStagedResult(context, swGlobal);
    }

    private Dictionary<int, List<IIgnitionSignal>> GroupSignalsByStage()
    {
        var signalsByStage = new Dictionary<int, List<IIgnitionSignal>>();
        foreach (var signal in _handles.Value)
        {
            int stage = signal is IStagedIgnitionSignal staged ? staged.Stage : 0;
            if (!signalsByStage.TryGetValue(stage, out var list))
            {
                list = new List<IIgnitionSignal>();
                signalsByStage[stage] = list;
            }
            list.Add(signal);
        }
        return signalsByStage;
    }

    private void MarkStageAsSkipped(List<IIgnitionSignal> signals, int stageNumber, StagedExecutionContext context)
    {
        var skippedResults = new List<IgnitionSignalResult>();
        foreach (var signal in signals)
        {
            var skipResult = new IgnitionSignalResult(signal.Name, IgnitionSignalStatus.Skipped, TimeSpan.Zero);
            skippedResults.Add(skipResult);
            RaiseSignalCompleted(skipResult);
        }
        context.AllResults.AddRange(skippedResults);
        context.StageResults.Add(new IgnitionStageResult(
            stageNumber, TimeSpan.Zero, skippedResults,
            SucceededCount: 0, FailedCount: 0, TimedOutCount: 0, Completed: false));
    }

    private async Task ExecuteStageAsync(
        List<IIgnitionSignal> signalsInStage,
        int stageNumber,
        CancellationTokenSource globalCts,
        Task globalTimeoutTask,
        Stopwatch swGlobal,
        SemaphoreSlim? gate,
        StagedExecutionContext context)
    {
        _logger.LogDebug("Starting stage {Stage} with {Count} signal(s).", stageNumber, signalsInStage.Count);

        var swStage = Stopwatch.StartNew();
        var stageTasks = await StartStageSignalsAsync(signalsInStage, globalCts, gate, swGlobal);

        var stageExecution = _options.StagePolicy == IgnitionStagePolicy.EarlyPromotion
            ? await ExecuteStageWithEarlyPromotionAsync(stageTasks, signalsInStage, stageNumber, globalCts, globalTimeoutTask, swGlobal, context)
            : await ExecuteStageStandardAsync(stageTasks, signalsInStage, stageNumber, globalCts, globalTimeoutTask, swGlobal, context);

        swStage.Stop();

        var stageResult = BuildStageResult(stageNumber, swStage.Elapsed, stageExecution, signalsInStage.Count);

        if (stageResult.TimedOutCount > 0)
        {
            context.HasAnyTimeout = true;
        }

        context.StageResults.Add(stageResult);
        context.AllResults.AddRange(stageExecution.Results);

        _logger.LogDebug(
            "Stage {Stage} completed in {Duration:F0} ms (succeeded: {Succeeded}, failed: {Failed}, timed out: {TimedOut}).",
            stageNumber, swStage.Elapsed.TotalMilliseconds, stageResult.SucceededCount, stageResult.FailedCount, stageResult.TimedOutCount);

        if (!context.ShouldStop)
        {
            context.ShouldStop = ShouldStopAfterStage(stageResult, stageExecution.Promoted, signalsInStage.Count, stageNumber);
        }
    }

    private Task<List<Task<IgnitionSignalResult>>> StartStageSignalsAsync(
        List<IIgnitionSignal> signals,
        CancellationTokenSource globalCts,
        SemaphoreSlim? gate,
        Stopwatch swGlobal)
    {
        var stageTasks = new List<Task<IgnitionSignalResult>>();
        foreach (var signal in signals)
        {
            var task = Task.Run(async () =>
            {
                if (gate is not null)
                {
                    await gate.WaitAsync(globalCts.Token);
                }

                RaiseSignalStarted(signal.Name);
                try
                {
                    var result = await WaitOneAsync(signal, globalCts.Token, swGlobal);
                    RaiseSignalCompleted(result);
                    return result;
                }
                finally
                {
                    gate?.Release();
                }
            }, globalCts.Token);

            stageTasks.Add(task);
        }
        return Task.FromResult(stageTasks);
    }

    private async Task<StageExecutionResult> ExecuteStageWithEarlyPromotionAsync(
        List<Task<IgnitionSignalResult>> stageTasks,
        List<IIgnitionSignal> signalsInStage,
        int stageNumber,
        CancellationTokenSource globalCts,
        Task globalTimeoutTask,
        Stopwatch swGlobal,
        StagedExecutionContext context)
    {
        var requiredSuccesses = (int)Math.Ceiling(signalsInStage.Count * _options.EarlyPromotionThreshold);
        var completedTasks = new HashSet<Task<IgnitionSignalResult>>();
        var results = new List<IgnitionSignalResult>();
        int succeededCount = 0;
        bool promoted = false;

        while (completedTasks.Count < stageTasks.Count)
        {
            var remainingTasks = stageTasks.Where(t => !completedTasks.Contains(t)).ToList();
            var allStageTask = Task.WhenAny(remainingTasks);
            var completedTask = await Task.WhenAny(allStageTask, globalTimeoutTask);

            if (completedTask == globalTimeoutTask)
            {
                context.GlobalTimedOut = true;
                RaiseGlobalTimeout(swGlobal.Elapsed, GetPendingSignalNamesFromTasks(remainingTasks, signalsInStage));

                if (_options.CancelOnGlobalTimeout)
                {
                    _logger.LogWarning("Global timeout in staged execution during stage {Stage} (cancelling).", stageNumber);
                    globalCts.Cancel();
                    context.ShouldStop = true;
                    try { await Task.WhenAll(stageTasks); } catch { /* swallow */ }
                    break;
                }
            }
            else
            {
                var finished = await allStageTask;
                completedTasks.Add(finished);

                if (finished.IsCompletedSuccessfully)
                {
                    var result = finished.Result;
                    results.Add(result);
                    if (result.Status == IgnitionSignalStatus.Succeeded)
                    {
                        succeededCount++;
                    }
                }

                if (!promoted && succeededCount >= requiredSuccesses)
                {
                    promoted = true;
                    _logger.LogDebug(
                        "Stage {Stage} reached early promotion threshold ({Success}/{Required}).",
                        stageNumber, succeededCount, requiredSuccesses);
                }
            }
        }

        // Collect remaining results
        CollectRemainingResults(stageTasks, signalsInStage, results, ref succeededCount);

        return new StageExecutionResult(results, succeededCount, promoted);
    }

    private async Task<StageExecutionResult> ExecuteStageStandardAsync(
        List<Task<IgnitionSignalResult>> stageTasks,
        List<IIgnitionSignal> signalsInStage,
        int stageNumber,
        CancellationTokenSource globalCts,
        Task globalTimeoutTask,
        Stopwatch swGlobal,
        StagedExecutionContext context)
    {
        var allStageTask = Task.WhenAll(stageTasks);
        var completedTask = await Task.WhenAny(allStageTask, globalTimeoutTask);

        if (completedTask == globalTimeoutTask)
        {
            context.GlobalTimedOut = true;
            RaiseGlobalTimeout(swGlobal.Elapsed, GetPendingSignalNamesFromTasks(stageTasks, signalsInStage));

            if (_options.CancelOnGlobalTimeout)
            {
                _logger.LogWarning("Global timeout in staged execution during stage {Stage} (cancelling).", stageNumber);
                globalCts.Cancel();
                context.ShouldStop = true;
                try { await allStageTask; } catch { /* swallow */ }
            }
            else
            {
                _logger.LogDebug("Global timeout in staged mode but CancelOnGlobalTimeout=false; waiting for stage {Stage}.", stageNumber);
                try { await allStageTask; } catch { /* swallow */ }
            }
        }
        else
        {
            try { await allStageTask; } catch { /* swallow, results gathered later */ }
        }

        var results = new List<IgnitionSignalResult>();
        int succeededCount = 0;

        for (int i = 0; i < stageTasks.Count; i++)
        {
            var task = stageTasks[i];
            if (task.IsCompletedSuccessfully)
            {
                results.Add(task.Result);
                if (task.Result.Status == IgnitionSignalStatus.Succeeded)
                {
                    succeededCount++;
                }
            }
            else if (task.IsCanceled)
            {
                results.Add(new IgnitionSignalResult(
                    signalsInStage[i].Name, IgnitionSignalStatus.TimedOut, TimeSpan.Zero,
                    CancellationReason: CancellationReason.GlobalTimeout));
            }
            else if (task.IsFaulted)
            {
                results.Add(new IgnitionSignalResult(
                    signalsInStage[i].Name, IgnitionSignalStatus.Failed, TimeSpan.Zero, task.Exception));
            }
        }

        return new StageExecutionResult(results, succeededCount, Promoted: false);
    }

    private void CollectRemainingResults(
        List<Task<IgnitionSignalResult>> stageTasks,
        List<IIgnitionSignal> signalsInStage,
        List<IgnitionSignalResult> results,
        ref int succeededCount)
    {
        var processedSignalNames = new HashSet<string>(results.Select(r => r.Name));

        for (int i = 0; i < stageTasks.Count; i++)
        {
            var task = stageTasks[i];
            var signalName = signalsInStage[i].Name;

            if (processedSignalNames.Contains(signalName))
            {
                continue;
            }

            if (task.IsCompletedSuccessfully)
            {
                results.Add(task.Result);
                if (task.Result.Status == IgnitionSignalStatus.Succeeded)
                {
                    succeededCount++;
                }
            }
            else if (task.IsCanceled || task.IsFaulted)
            {
                results.Add(new IgnitionSignalResult(
                    signalName,
                    task.IsCanceled ? IgnitionSignalStatus.TimedOut : IgnitionSignalStatus.Failed,
                    TimeSpan.Zero,
                    task.Exception,
                    CancellationReason: task.IsCanceled ? CancellationReason.GlobalTimeout : CancellationReason.None));
            }
        }
    }

    private static IgnitionStageResult BuildStageResult(int stageNumber, TimeSpan duration, StageExecutionResult execution, int totalSignals)
    {
        int failedCount = execution.Results.Count(r => r.Status == IgnitionSignalStatus.Failed);
        int timedOutCount = execution.Results.Count(r => r.Status == IgnitionSignalStatus.TimedOut);
        bool stageCompleted = execution.Results.Count == totalSignals;

        return new IgnitionStageResult(
            stageNumber, duration, execution.Results,
            execution.SucceededCount, failedCount, timedOutCount, stageCompleted, execution.Promoted);
    }

    private bool ShouldStopAfterStage(IgnitionStageResult stageResult, bool promoted, int totalSignals, int stageNumber)
    {
        switch (_options.StagePolicy)
        {
            case IgnitionStagePolicy.AllMustSucceed:
                if (stageResult.FailedCount > 0 || stageResult.TimedOutCount > 0)
                {
                    _logger.LogWarning(
                        "Stage {Stage} had failures/timeouts; stopping execution per AllMustSucceed policy.",
                        stageNumber);
                    return true;
                }
                break;

            case IgnitionStagePolicy.FailFast:
                if (stageResult.FailedCount > 0)
                {
                    _logger.LogWarning(
                        "Stage {Stage} had {Count} failure(s); stopping execution per FailFast policy.",
                        stageNumber, stageResult.FailedCount);
                    return true;
                }
                break;

            case IgnitionStagePolicy.BestEffort:
                // Continue regardless of failures
                break;

            case IgnitionStagePolicy.EarlyPromotion:
                var requiredSuccesses = (int)Math.Ceiling(totalSignals * _options.EarlyPromotionThreshold);
                if (!promoted && stageResult.SucceededCount < requiredSuccesses)
                {
                    _logger.LogWarning(
                        "Stage {Stage} did not meet promotion threshold ({Success}/{Required}); stopping execution.",
                        stageNumber, stageResult.SucceededCount, requiredSuccesses);
                    return true;
                }
                break;
        }
        return false;
    }

    private IgnitionResult BuildStagedResult(StagedExecutionContext context, Stopwatch swGlobal)
    {
        bool hasTimedOut = context.GlobalTimedOut || context.HasAnyTimeout;
        if (_options.LogTopSlowHandles)
        {
            foreach (var s in context.AllResults.OrderByDescending(r => r.Duration).Take(_options.SlowHandleLogCount))
            {
                _logger.LogDebug("Startup handle '{Name}' took {Ms} ms.", s.Name, s.Duration.TotalMilliseconds);
            }
        }

        _logger.LogDebug(
            "Staged startup readiness finished in {Ms} ms across {StageCount} stage(s).",
            swGlobal.Elapsed.TotalMilliseconds, context.StageResults.Count);

        return IgnitionResult.FromStaged(context.AllResults, context.StageResults, swGlobal.Elapsed, hasTimedOut && _options.CancelOnGlobalTimeout);
    }

    /// <summary>
    /// Context for tracking state during staged execution.
    /// </summary>
    private sealed class StagedExecutionContext
    {
        public List<IgnitionSignalResult> AllResults { get; } = new();
        public List<IgnitionStageResult> StageResults { get; } = new();
        public bool GlobalTimedOut { get; set; }
        public bool HasAnyTimeout { get; set; }
        public bool ShouldStop { get; set; }
    }

    /// <summary>
    /// Result of executing a single stage.
    /// </summary>
    private sealed record StageExecutionResult(
        List<IgnitionSignalResult> Results,
        int SucceededCount,
        bool Promoted);

    private static IReadOnlyList<string> GetPendingSignalNamesFromTasks(
        List<Task<IgnitionSignalResult>> tasks,
        List<IIgnitionSignal> signals)
    {
        var pending = new List<string>();
        for (int i = 0; i < signals.Count && i < tasks.Count; i++)
        {
            if (!tasks[i].IsCompleted)
            {
                pending.Add(signals[i].Name);
            }
        }
        return pending;
    }

    private async Task<IgnitionSignalResult> WaitOneAsync(IIgnitionSignal h, CancellationToken globalToken, Stopwatch swGlobal)
    {
        var startedAt = swGlobal.Elapsed;
        var sw = Stopwatch.StartNew();

        // Extract scope information if the signal implements IScopedIgnitionSignal
        ICancellationScope? signalScope = null;
        bool cancelScopeOnFailure = false;
        if (h is IScopedIgnitionSignal scopedSignal)
        {
            signalScope = scopedSignal.CancellationScope;
            cancelScopeOnFailure = scopedSignal.CancelScopeOnFailure;
        }

        try
        {
            // Create linked cancellation including scope token if present
            CancellationTokenSource perHandleCts = signalScope is not null
                ? CancellationTokenSource.CreateLinkedTokenSource(globalToken, signalScope.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(globalToken);

            using (perHandleCts)
            {
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

                        // Trigger scope cancellation if configured
                        if (cancelScopeOnFailure && signalScope is not null)
                        {
                            signalScope.Cancel(CancellationReason.BundleCancelled, h.Name);
                        }

                        var completedAt = swGlobal.Elapsed;
                        return new IgnitionSignalResult(
                            h.Name,
                            IgnitionSignalStatus.TimedOut,
                            sw.Elapsed,
                            CancellationReason: CancellationReason.PerSignalTimeout,
                            StartedAt: startedAt,
                            CompletedAt: completedAt);
                    }
                }

                await work; // propagate exceptions if failed
                var successCompletedAt = swGlobal.Elapsed;
                return new IgnitionSignalResult(
                    h.Name,
                    IgnitionSignalStatus.Succeeded,
                    sw.Elapsed,
                    StartedAt: startedAt,
                    CompletedAt: successCompletedAt);
            }
        }
        catch (OperationCanceledException)
        {
            // Determine the cancellation reason based on which token was cancelled.
            // Check cancellation sources in priority order: scope > global > external.
            // Scope cancellation is more specific and takes precedence over global timeout.
            CancellationReason reason;
            string? cancelledBy = null;
            var completedAt = swGlobal.Elapsed;

            if (signalScope is not null && signalScope.IsCancelled)
            {
                reason = signalScope.CancellationReason;
                cancelledBy = signalScope.TriggeringSignalName;

                // Return Cancelled status for scope-based cancellations
                return new IgnitionSignalResult(
                    h.Name,
                    IgnitionSignalStatus.Cancelled,
                    sw.Elapsed,
                    CancellationReason: reason,
                    CancelledBySignal: cancelledBy,
                    StartedAt: startedAt,
                    CompletedAt: completedAt);
            }
            else if (globalToken.IsCancellationRequested)
            {
                reason = CancellationReason.GlobalTimeout;
            }
            else
            {
                reason = CancellationReason.ExternalCancellation;
            }

            // Treat global/external cancellations as timeouts for backward compatibility
            return new IgnitionSignalResult(
                h.Name,
                IgnitionSignalStatus.TimedOut,
                sw.Elapsed,
                CancellationReason: reason,
                CancelledBySignal: cancelledBy,
                StartedAt: startedAt,
                CompletedAt: completedAt);
        }
        catch (Exception ex)
        {
            // Trigger scope cancellation if configured
            if (cancelScopeOnFailure && signalScope is not null)
            {
                signalScope.Cancel(CancellationReason.BundleCancelled, h.Name);
            }

            var completedAt = swGlobal.Elapsed;
            return new IgnitionSignalResult(
                h.Name,
                IgnitionSignalStatus.Failed,
                sw.Elapsed,
                ex,
                StartedAt: startedAt,
                CompletedAt: completedAt);
        }
    }

    /// <summary>
    /// Transitions the state to the specified new state in a thread-safe manner.
    /// </summary>
    private void TransitionToState(IgnitionState newState)
    {
        lock (_stateLock)
        {
            _state = newState;
        }
    }

    /// <summary>
    /// Transitions to a terminal state based on the result and raises the CoordinatorCompleted event.
    /// </summary>
    private void TransitionToFinalState(IgnitionResult result)
    {
        // Record total duration metric if configured.
        var metrics = _options.Metrics;
        if (metrics is not null)
        {
            metrics.RecordTotalDuration(result.TotalDuration);
        }

        IgnitionState finalState;
        if (result.TimedOut)
        {
            finalState = IgnitionState.TimedOut;
        }
        else
        {
            bool hasFailed = result.Results.Any(r =>
                r.Status == IgnitionSignalStatus.Failed ||
                r.Status == IgnitionSignalStatus.Cancelled);
            finalState = hasFailed ? IgnitionState.Failed : IgnitionState.Completed;
        }

        lock (_stateLock)
        {
            _state = finalState;
        }

        RaiseCoordinatorCompleted(finalState, result);
    }

    /// <summary>
    /// Raises the SignalStarted event in a thread-safe manner.
    /// </summary>
    private void RaiseSignalStarted(string signalName)
    {
        var handler = SignalStarted;
        if (handler is not null)
        {
            try
            {
                var args = new IgnitionSignalStartedEventArgs(signalName, DateTimeOffset.UtcNow);
                handler(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception in SignalStarted event handler for signal '{Name}'.", signalName);
            }
        }
    }

    /// <summary>
    /// Raises the SignalCompleted event in a thread-safe manner.
    /// </summary>
    private void RaiseSignalCompleted(IgnitionSignalResult result)
    {
        // Record metrics if configured (null-check avoids overhead when metrics are disabled).
        var metrics = _options.Metrics;
        if (metrics is not null)
        {
            metrics.RecordSignalDuration(result.Name, result.Duration);
            metrics.RecordSignalStatus(result.Name, result.Status);
        }

        var handler = SignalCompleted;
        if (handler is not null)
        {
            try
            {
                var args = new IgnitionSignalCompletedEventArgs(
                    result.Name,
                    result.Status,
                    result.Duration,
                    DateTimeOffset.UtcNow,
                    result.Exception);
                handler(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception in SignalCompleted event handler for signal '{Name}'.", result.Name);
            }
        }
    }

    /// <summary>
    /// Raises the GlobalTimeoutReached event in a thread-safe manner.
    /// </summary>
    private void RaiseGlobalTimeout(TimeSpan elapsed, IReadOnlyList<string> pendingSignals)
    {
        var handler = GlobalTimeoutReached;
        if (handler is not null)
        {
            try
            {
                var args = new IgnitionGlobalTimeoutEventArgs(
                    _options.GlobalTimeout,
                    elapsed,
                    DateTimeOffset.UtcNow,
                    pendingSignals);
                handler(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception in GlobalTimeoutReached event handler.");
            }
        }
    }

    /// <summary>
    /// Raises the CoordinatorCompleted event in a thread-safe manner.
    /// </summary>
    private void RaiseCoordinatorCompleted(IgnitionState finalState, IgnitionResult result)
    {
        var handler = CoordinatorCompleted;
        if (handler is not null)
        {
            try
            {
                var args = new IgnitionCoordinatorCompletedEventArgs(
                    finalState,
                    result.TotalDuration,
                    DateTimeOffset.UtcNow,
                    result);
                handler(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception in CoordinatorCompleted event handler.");
            }
        }
    }

    /// <summary>
    /// Gets the names of signals that have not completed yet, including those not yet started.
    /// </summary>
    private static IReadOnlyList<string> GetPendingSignalNames(
        List<Task<IgnitionSignalResult>> tasks,
        IReadOnlyList<IIgnitionSignal> handles)
    {
        var pending = new List<string>();
        for (int i = 0; i < handles.Count; i++)
        {
            if (i >= tasks.Count || !tasks[i].IsCompleted)
            {
                pending.Add(handles[i].Name);
            }
        }
        return pending;
    }

    /// <summary>
    /// Gets the names of signals from a graph that have not started or completed yet.
    /// </summary>
    private static IReadOnlyList<string> GetPendingSignalNamesFromGraph(
        Dictionary<IIgnitionSignal, Task<IgnitionSignalResult>> results,
        IIgnitionGraph graph)
    {
        var pending = new List<string>();
        foreach (var signal in graph.Signals.Where(s => !results.TryGetValue(s, out var task) || !task.IsCompleted))
        {
            pending.Add(signal.Name);
        }
        return pending;
    }
}
