using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Veggerby.Ignition.Stages;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring staged ignition execution with per-stage execution modes.
/// </summary>
public static class IgnitionStageExtensions
{
    /// <summary>
    /// Configures ignition to use staged execution with explicit stage definitions.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configureStages">Configuration delegate for defining stages.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables per-stage execution modes, allowing sophisticated orchestration patterns:
    /// </para>
    /// <code>
    /// services.AddStagedIgnition(stages => stages
    ///     .AddSequentialStage("Infrastructure")  // Stage 0: Sequential container startup
    ///     .AddParallelStage("Readiness Checks")  // Stage 1: Parallel readiness validation
    ///     .AddDependencyAwareStage("Services")); // Stage 2: Dependency-aware initialization
    /// </code>
    /// <para>
    /// Signals registered with stage numbers will be assigned to the corresponding stage.
    /// Each stage executes with its own execution mode.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddStagedIgnition(
        this IServiceCollection services,
        Action<IgnitionStageBuilder> configureStages)
    {
        ArgumentNullException.ThrowIfNull(configureStages, nameof(configureStages));

        // Register the stage configuration
        var stageBuilder = new IgnitionStageBuilder();
        configureStages(stageBuilder);
        var stages = stageBuilder.Build();

        // Store stages in DI for the coordinator to use
        services.TryAddSingleton<IReadOnlyList<IgnitionStage>>(stages);

        // Ensure staged execution mode is set
        services.Configure<IgnitionOptions>(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Staged;
        });

        return services;
    }

    /// <summary>
    /// Configures signals for a specific stage using a fluent builder pattern.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="stageNumber">The stage number (0 = infrastructure, 1 = services, 2 = workers, etc.).</param>
    /// <param name="configureStage">Configuration delegate for adding signals to this stage.</param>
    /// <param name="executionMode">Execution mode for this stage (default: Parallel).</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method provides a fluent API for configuring multiple signals within a single stage,
    /// eliminating the need to specify the stage number repeatedly.
    /// </para>
    /// <para>
    /// Automatically configures staged execution mode if not already set.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Configure Stage 0 with multiple container startup signals
    /// services.AddIgnitionStage(0, stage => stage
    ///     .AddTaskSignal("postgres-container", async ct => await infra.StartPostgresAsync())
    ///     .AddTaskSignal("redis-container", async ct => await infra.StartRedisAsync())
    ///     .AddTaskSignal("rabbitmq-container", async ct => await infra.StartRabbitMqAsync()));
    /// 
    /// // Configure Stage 1 with sequential execution
    /// services.AddIgnitionStage(1, stage => stage
    ///     .AddTaskSignal("db-migration", async ct => await migrator.MigrateAsync(ct))
    ///     .AddTaskSignal("seed-data", async ct => await seeder.SeedAsync(ct)),
    ///     IgnitionExecutionMode.Sequential);
    /// </code>
    /// </example>
    public static IServiceCollection AddIgnitionStage(
        this IServiceCollection services,
        int stageNumber,
        Action<IgnitionStageSignalBuilder> configureStage,
        IgnitionExecutionMode executionMode = IgnitionExecutionMode.Parallel)
    {
        ArgumentNullException.ThrowIfNull(configureStage, nameof(configureStage));

        if (stageNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stageNumber), "Stage number cannot be negative.");
        }

        // Ensure staged execution mode is set
        services.Configure<IgnitionOptions>(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Staged;
        });

        // Configure the stage's execution mode
        services.Configure<IgnitionStageConfiguration>(config =>
        {
            config.EnsureStage(stageNumber, executionMode);
        });

        // Build the stage signals
        var builder = new IgnitionStageSignalBuilder(services, stageNumber, executionMode);
        configureStage(builder);

        return services;
    }

    /// <summary>
    /// Adds a signal factory to a specific stage with explicit execution mode configuration.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="stageNumber">The stage number to add the signal to.</param>
    /// <param name="name">Human-readable name for the signal.</param>
    /// <param name="signalFactory">Factory delegate for creating the signal.</param>
    /// <param name="executionMode">Execution mode for this stage (if not already defined).</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method allows adding signals to stages with explicit execution mode control.
    /// If the stage doesn't exist yet, it will be created with the specified execution mode.
    /// </para>
    /// <code>
    /// services.AddSignalToStage(0, "postgres-container", 
    ///     sp => new ContainerStartSignal(...),
    ///     IgnitionExecutionMode.Sequential);  // Containers start in order
    /// 
    /// services.AddSignalToStage(1, "postgres-ready",
    ///     sp => new PostgresReadinessSignal(...),
    ///     IgnitionExecutionMode.Parallel);    // Readiness checks run concurrently
    /// </code>
    /// </remarks>
    public static IServiceCollection AddSignalToStage(
        this IServiceCollection services,
        int stageNumber,
        string name,
        Func<IServiceProvider, IIgnitionSignal> signalFactory,
        IgnitionExecutionMode executionMode = IgnitionExecutionMode.Parallel,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(signalFactory, nameof(signalFactory));

        // Create factory with stage metadata
        var innerFactory = new DelegateIgnitionSignalFactory(name, signalFactory, timeout);
        var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, stageNumber);

        services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

        // Store stage configuration metadata for the coordinator to use when building stages
        services.Configure<IgnitionStageConfiguration>(config =>
        {
            config.EnsureStage(stageNumber, executionMode);
        });

        return services;
    }

    /// <summary>
    /// Adds a signal factory to a specific stage using a task factory delegate.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="stageNumber">The stage number to add the signal to.</param>
    /// <param name="name">Human-readable name for the signal.</param>
    /// <param name="taskFactory">Task factory delegate that produces the readiness task.</param>
    /// <param name="executionMode">Execution mode for this stage (if not already defined).</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddTaskToStage(
        this IServiceCollection services,
        int stageNumber,
        string name,
        Func<IServiceProvider, CancellationToken, Task> taskFactory,
        IgnitionExecutionMode executionMode = IgnitionExecutionMode.Parallel,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(taskFactory, nameof(taskFactory));

        return services.AddSignalToStage(
            stageNumber,
            name,
            sp => IgnitionSignal.FromTaskFactory(name, ct => taskFactory(sp, ct), timeout),
            executionMode,
            timeout);
    }
}

/// <summary>
/// Configuration for stage execution modes.
/// Used internally to track which execution mode each stage should use.
/// </summary>
public sealed class IgnitionStageConfiguration
{
    private readonly Dictionary<int, IgnitionExecutionMode> _stageExecutionModes = new();

    /// <summary>
    /// Ensures a stage exists with the specified execution mode.
    /// If the stage already exists with a different mode, the existing mode is preserved.
    /// </summary>
    public void EnsureStage(int stageNumber, IgnitionExecutionMode executionMode)
    {
        if (!_stageExecutionModes.ContainsKey(stageNumber))
        {
            _stageExecutionModes[stageNumber] = executionMode;
        }
    }

    /// <summary>
    /// Gets the execution mode for a specific stage number.
    /// </summary>
    public IgnitionExecutionMode? GetExecutionMode(int stageNumber)
    {
        return _stageExecutionModes.TryGetValue(stageNumber, out var mode) ? mode : null;
    }

    /// <summary>
    /// Gets all configured stages with their execution modes.
    /// </summary>
    public IReadOnlyDictionary<int, IgnitionExecutionMode> StageExecutionModes => _stageExecutionModes;
}
