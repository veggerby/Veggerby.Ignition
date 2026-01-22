using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using global::MassTransit;

namespace Veggerby.Ignition.MassTransit;

/// <summary>
/// Extension methods for registering MassTransit bus readiness signals with dependency injection.
/// </summary>
public static class MassTransitIgnitionExtensions
{
    /// <summary>
    /// Registers a MassTransit bus readiness signal.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension method registers an ignition signal that verifies the MassTransit bus is ready
    /// by leveraging MassTransit's built-in health checks. The signal works with any MassTransit transport
    /// (RabbitMQ, Azure Service Bus, in-memory, etc.) as long as an <see cref="IBus"/> instance is registered
    /// in the DI container.
    /// </para>
    /// <para>
    /// The signal name defaults to "masstransit-readiness". The check verifies that the bus status
    /// reaches <see cref="BusHealthStatus.Healthy"/> within the configured timeout.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMassTransit(x =>
    /// {
    ///     x.UsingRabbitMq((context, cfg) =>
    ///     {
    ///         cfg.Host("localhost", "/");
    ///         cfg.ConfigureEndpoints(context);
    ///     });
    /// });
    /// 
    /// // Suppress expected MassTransit connection warnings during startup
    /// services.AddLogging(builder => builder.AddFilter("MassTransit", LogLevel.Error));
    /// 
    /// services.AddMassTransitReadiness(options =>
    /// {
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    ///     options.BusReadyTimeout = TimeSpan.FromSeconds(45);
    /// });
    /// 
    /// // Staged execution
    /// services.AddMassTransitReadiness(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    ///     options.BusReadyTimeout = TimeSpan.FromSeconds(60);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMassTransitReadiness(
        this IServiceCollection services,
        Action<MassTransitReadinessOptions>? configure = null)
    {
        var options = new MassTransitReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new MassTransitReadinessSignalFactory(options);
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
            var bus = sp.GetRequiredService<IBus>();
            var logger = sp.GetRequiredService<ILogger<MassTransitReadinessSignal>>();

            return new MassTransitReadinessSignal(bus, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a MassTransit bus readiness signal using a factory-based approach for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for MassTransit readiness signals in staged execution.
    /// The bus is resolved when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and configures MassTransit for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and configure MassTransit
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("rabbitmq-container",
    ///     async ct => await infrastructure.StartRabbitMqAsync(), stage: 0);
    /// 
    /// // Register MassTransit
    /// services.AddMassTransit(x =>
    /// {
    ///     x.UsingRabbitMq((context, cfg) =>
    ///     {
    ///         cfg.Host("localhost", "/");
    ///         cfg.ConfigureEndpoints(context);
    ///     });
    /// });
    /// 
    /// // Stage 1: Use MassTransit bus
    /// services.AddMassTransitReadinessFactory(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    ///     options.BusReadyTimeout = TimeSpan.FromSeconds(60);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMassTransitReadinessFactory(
        this IServiceCollection services,
        Action<MassTransitReadinessOptions>? configure = null)
    {
        var options = new MassTransitReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new MassTransitReadinessSignalFactory(options);

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
