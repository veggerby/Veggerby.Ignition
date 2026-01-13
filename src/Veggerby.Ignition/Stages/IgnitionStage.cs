using System;
using System.Collections.Generic;

namespace Veggerby.Ignition.Stages;

/// <summary>
/// Represents a stage/phase in ignition execution with its own execution configuration.
/// Acts as a mini-coordinator for signals within the stage, enabling per-stage execution modes
/// and deep hierarchical stage structures.
/// </summary>
public sealed class IgnitionStage
{
    private readonly List<IIgnitionSignalFactory> _factories = new();
    private readonly List<IgnitionStage> _childStages = new();

    /// <summary>
    /// Creates a new ignition stage.
    /// </summary>
    /// <param name="stageNumber">The stage number (0-based index for ordering).</param>
    /// <param name="name">Optional human-readable name for the stage.</param>
    /// <param name="executionMode">Execution mode for signals within this stage. Defaults to Parallel.</param>
    public IgnitionStage(int stageNumber, string? name = null, IgnitionExecutionMode executionMode = IgnitionExecutionMode.Parallel)
    {
        if (stageNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stageNumber), "Stage number cannot be negative.");
        }

        StageNumber = stageNumber;
        Name = name ?? $"Stage {stageNumber}";
        ExecutionMode = executionMode;
    }

    /// <summary>
    /// Gets the stage number (0-based index for ordering).
    /// </summary>
    public int StageNumber { get; }

    /// <summary>
    /// Gets the human-readable name for the stage.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the execution mode for signals within this stage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This determines how signals within this specific stage are executed:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="IgnitionExecutionMode.Parallel"/>: All signals execute concurrently</item>
    ///   <item><see cref="IgnitionExecutionMode.Sequential"/>: Signals execute one after another</item>
    ///   <item><see cref="IgnitionExecutionMode.DependencyAware"/>: Signals execute based on dependency graph</item>
    ///   <item><see cref="IgnitionExecutionMode.Staged"/>: Signals are organized into nested sub-stages</item>
    /// </list>
    /// </remarks>
    public IgnitionExecutionMode ExecutionMode { get; }

    /// <summary>
    /// Gets the signal factories registered for this stage.
    /// </summary>
    public IReadOnlyList<IIgnitionSignalFactory> Factories => _factories;

    /// <summary>
    /// Gets the child stages for hierarchical stage structures.
    /// Used when ExecutionMode is Staged to enable nested stage graphs.
    /// </summary>
    public IReadOnlyList<IgnitionStage> ChildStages => _childStages;

    /// <summary>
    /// Adds a signal factory to this stage.
    /// </summary>
    /// <param name="factory">The factory to add.</param>
    public void AddFactory(IIgnitionSignalFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.Add(factory);
    }

    /// <summary>
    /// Adds a child stage for hierarchical stage structures.
    /// </summary>
    /// <param name="childStage">The child stage to add.</param>
    /// <remarks>
    /// Child stages are only executed when this stage's ExecutionMode is Staged.
    /// They enable deep hierarchical structures where each stage can have its own sub-stages
    /// with independent execution modes.
    /// </remarks>
    public void AddChildStage(IgnitionStage childStage)
    {
        ArgumentNullException.ThrowIfNull(childStage);
        _childStages.Add(childStage);
    }

    /// <summary>
    /// Gets whether this stage has any signal factories.
    /// </summary>
    public bool HasFactories => _factories.Count > 0;

    /// <summary>
    /// Gets whether this stage has child stages.
    /// </summary>
    public bool HasChildStages => _childStages.Count > 0;

    /// <summary>
    /// Gets the total number of factories including child stages recursively.
    /// </summary>
    public int TotalFactoryCount
    {
        get
        {
            int count = _factories.Count;
            foreach (var child in _childStages)
            {
                count += child.TotalFactoryCount;
            }
            return count;
        }
    }
}
