using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;

namespace Veggerby.Ignition.Orleans;

/// <summary>
/// Extension methods for registering Orleans readiness signals with dependency injection.
/// </summary>
public static class OrleansIgnitionExtensions
{
    /// <summary>
    /// Registers an Orleans readiness signal using the registered <see cref="IClusterClient"/>.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The signal name defaults to "orleans-readiness". Requires an <see cref="IClusterClient"/>
    /// to be registered in the service collection. Verifies that the cluster client is available
    /// from the DI container.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddOrleansReadiness(options =>
    /// {
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// 
    /// // Staged execution
    /// services.AddOrleansReadiness(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOrleansReadiness(
        this IServiceCollection services,
        Action<OrleansReadinessOptions>? configure = null)
    {
        var options = new OrleansReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new OrleansReadinessSignalFactory(options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var clusterClient = sp.GetRequiredService<IClusterClient>();
            var logger = sp.GetRequiredService<ILogger<OrleansReadinessSignal>>();

            return new OrleansReadinessSignal(clusterClient, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an Orleans readiness signal using a factory-based approach for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Orleans readiness signals in staged execution.
    /// The cluster client is resolved when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and configures Orleans client for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and configure Orleans client
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("orleans-silo",
    ///     async ct => await infrastructure.StartOrleansSiloAsync(), stage: 0);
    /// 
    /// // Register Orleans client
    /// services.AddOrleansClient(builder =>
    /// {
    ///     builder.UseLocalhostClustering();
    /// });
    /// 
    /// // Stage 1: Use Orleans client
    /// services.AddOrleansReadinessFactory(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOrleansReadinessFactory(
        this IServiceCollection services,
        Action<OrleansReadinessOptions>? configure = null)
    {
        var options = new OrleansReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new OrleansReadinessSignalFactory(options);

        // If Stage is specified, wrap with StagedIgnitionSignalFactory
        if (options.Stage.HasValue)
        {
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });
        }
        else
        {
            services.AddSingleton<IIgnitionSignalFactory>(innerFactory);
        }

        return services;
    }
}
