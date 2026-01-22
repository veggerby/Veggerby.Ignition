using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Marten;

/// <summary>
/// Extension methods for registering Marten document store readiness signals with dependency injection.
/// </summary>
public static class MartenIgnitionExtensions
{
    /// <summary>
    /// Registers a Marten document store readiness signal.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The signal name defaults to "marten-readiness". The document store instance is resolved
    /// from the DI container. Ensure Marten is configured before calling this method.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMarten(/* configure Marten */);
    /// 
    /// services.AddMartenReadiness(options =>
    /// {
    ///     options.VerifyDocumentStore = true;
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// 
    /// // Staged execution
    /// services.AddMartenReadiness(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.VerifyDocumentStore = true;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMartenReadiness(
        this IServiceCollection services,
        Action<MartenReadinessOptions>? configure = null)
    {
        var options = new MartenReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new MartenReadinessSignalFactory(options);
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
            var documentStore = sp.GetRequiredService<IDocumentStore>();
            var logger = sp.GetRequiredService<ILogger<MartenReadinessSignal>>();
            return new MartenReadinessSignal(documentStore, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Marten document store readiness signal using a factory-based approach for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Marten readiness signals in staged execution.
    /// The document store is resolved when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and configures Marten for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and configure Marten
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("postgres-container",
    ///     async ct => await infrastructure.StartPostgresAsync(), stage: 0);
    /// 
    /// // Register Marten
    /// services.AddMarten(options =>
    /// {
    ///     options.Connection(infrastructure.PostgresConnectionString);
    /// });
    /// 
    /// // Stage 1: Use Marten document store
    /// services.AddMartenReadinessFactory(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.VerifyDocumentStore = true;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMartenReadinessFactory(
        this IServiceCollection services,
        Action<MartenReadinessOptions>? configure = null)
    {
        var options = new MartenReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new MartenReadinessSignalFactory(options);

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
