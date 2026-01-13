using System;
using System.Collections.Generic;
using System.Linq;

namespace Veggerby.Ignition.Stages;

/// <summary>
/// Fluent builder for constructing hierarchical stage structures.
/// Provides a simple API for configuring complex multi-stage execution graphs.
/// </summary>
public sealed class IgnitionStageBuilder
{
    private readonly List<IgnitionStage> _stages = new();
    private int _currentStageNumber = 0;

    /// <summary>
    /// Adds a new stage with the specified execution mode.
    /// </summary>
    /// <param name="executionMode">The execution mode for the stage.</param>
    /// <param name="name">Optional human-readable name for the stage.</param>
    /// <param name="configure">Optional configuration action for the stage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public IgnitionStageBuilder AddStage(
        IgnitionExecutionMode executionMode = IgnitionExecutionMode.Parallel,
        string? name = null,
        Action<IgnitionStage>? configure = null)
    {
        var stage = new IgnitionStage(_currentStageNumber++, name, executionMode);
        configure?.Invoke(stage);
        _stages.Add(stage);
        return this;
    }

    /// <summary>
    /// Adds a parallel execution stage (all signals execute concurrently).
    /// </summary>
    /// <param name="name">Optional human-readable name for the stage.</param>
    /// <param name="configure">Optional configuration action for the stage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public IgnitionStageBuilder AddParallelStage(string? name = null, Action<IgnitionStage>? configure = null)
        => AddStage(IgnitionExecutionMode.Parallel, name, configure);

    /// <summary>
    /// Adds a sequential execution stage (signals execute one after another).
    /// </summary>
    /// <param name="name">Optional human-readable name for the stage.</param>
    /// <param name="configure">Optional configuration action for the stage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public IgnitionStageBuilder AddSequentialStage(string? name = null, Action<IgnitionStage>? configure = null)
        => AddStage(IgnitionExecutionMode.Sequential, name, configure);

    /// <summary>
    /// Adds a dependency-aware execution stage (signals execute based on dependency graph).
    /// </summary>
    /// <param name="name">Optional human-readable name for the stage.</param>
    /// <param name="configure">Optional configuration action for the stage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public IgnitionStageBuilder AddDependencyAwareStage(string? name = null, Action<IgnitionStage>? configure = null)
        => AddStage(IgnitionExecutionMode.DependencyAware, name, configure);

    /// <summary>
    /// Adds a nested staged execution stage (enables hierarchical stage structures).
    /// </summary>
    /// <param name="name">Optional human-readable name for the stage.</param>
    /// <param name="configure">Optional configuration action for the stage.</param>
    /// <returns>The builder for fluent chaining.</returns>
    public IgnitionStageBuilder AddNestedStage(string? name = null, Action<IgnitionStage>? configure = null)
        => AddStage(IgnitionExecutionMode.Staged, name, configure);

    /// <summary>
    /// Builds the collection of configured stages.
    /// </summary>
    /// <returns>A read-only list of configured stages.</returns>
    public IReadOnlyList<IgnitionStage> Build() => _stages.AsReadOnly();

    /// <summary>
    /// Gets the currently configured stages.
    /// </summary>
    public IReadOnlyList<IgnitionStage> Stages => _stages;
}
