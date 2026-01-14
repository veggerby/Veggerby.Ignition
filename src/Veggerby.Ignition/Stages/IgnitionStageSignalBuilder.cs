using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Fluent builder for configuring signals within a specific ignition stage.
/// </summary>
public sealed class IgnitionStageSignalBuilder
{
    private readonly IServiceCollection _services;
    private readonly int _stageNumber;
    private readonly IgnitionExecutionMode _executionMode;

    internal IgnitionStageSignalBuilder(IServiceCollection services, int stageNumber, IgnitionExecutionMode executionMode)
    {
        _services = services;
        _stageNumber = stageNumber;
        _executionMode = executionMode;
    }

    /// <summary>
    /// Adds a task-based signal to this stage.
    /// </summary>
    /// <param name="name">Logical signal name used for diagnostics and result reporting.</param>
    /// <param name="taskFactory">Factory producing the readiness task.</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IgnitionStageSignalBuilder AddTaskSignal(
        string name,
        Func<CancellationToken, Task> taskFactory,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(taskFactory, nameof(taskFactory));

        var signal = IgnitionSignal.FromTaskFactory(name, taskFactory, timeout);
        var innerFactory = new DelegateIgnitionSignalFactory(name, _ => signal, timeout);
        var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, _stageNumber);

        _services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

        return this;
    }

    /// <summary>
    /// Adds an already-created task-based signal to this stage.
    /// </summary>
    /// <param name="name">Logical signal name used for diagnostics and result reporting.</param>
    /// <param name="readyTask">Task that completes when the underlying component is ready.</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IgnitionStageSignalBuilder AddTaskSignal(
        string name,
        Task readyTask,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(readyTask, nameof(readyTask));

        var signal = IgnitionSignal.FromTask(name, readyTask, timeout);
        var innerFactory = new DelegateIgnitionSignalFactory(name, _ => signal, timeout);
        var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, _stageNumber);

        _services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

        return this;
    }

    /// <summary>
    /// Adds an existing signal to this stage.
    /// </summary>
    /// <param name="signal">The signal to add to this stage.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IgnitionStageSignalBuilder AddSignal(IIgnitionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal, nameof(signal));

        var innerFactory = new DelegateIgnitionSignalFactory(signal.Name, _ => signal, signal.Timeout);
        var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, _stageNumber);

        _services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

        return this;
    }

    /// <summary>
    /// Adds a signal factory to this stage.
    /// </summary>
    /// <param name="name">Logical signal name.</param>
    /// <param name="signalFactory">Factory that creates the signal using the service provider.</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public IgnitionStageSignalBuilder AddSignalFactory(
        string name,
        Func<IServiceProvider, IIgnitionSignal> signalFactory,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(signalFactory, nameof(signalFactory));

        var innerFactory = new DelegateIgnitionSignalFactory(name, signalFactory, timeout);
        var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, _stageNumber);

        _services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

        return this;
    }
}
