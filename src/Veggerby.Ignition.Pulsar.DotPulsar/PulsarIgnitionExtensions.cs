using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Pulsar.DotPulsar;

/// <summary>
/// Extension methods for registering Apache Pulsar readiness signals with dependency injection.
/// </summary>
public static class PulsarIgnitionExtensions
{
    /// <summary>
    /// Registers a Pulsar readiness signal using a service URL.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="serviceUrl">Pulsar service URL (e.g., "pulsar://localhost:6650").</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "pulsar-readiness". For connection-only verification,
    /// no additional configuration is required. To verify topics or subscriptions, use the
    /// <paramref name="configure"/> delegate to specify verification settings.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple cluster connectivity check
    /// services.AddPulsarReadiness("pulsar://localhost:6650");
    /// 
    /// // Topic verification
    /// services.AddPulsarReadiness("pulsar://localhost:6650", options =>
    /// {
    ///     options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
    ///     options.WithTopic("persistent://public/default/orders");
    ///     options.WithTopic("persistent://public/default/payments");
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// 
    /// // Staged execution
    /// services.AddPulsarReadiness("pulsar://localhost:6650", options =>
    /// {
    ///     options.Stage = 3;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddPulsarReadiness(
        this IServiceCollection services,
        string serviceUrl,
        Action<PulsarReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl, nameof(serviceUrl));

        var options = new PulsarReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new PulsarReadinessSignalFactory(_ => serviceUrl, options);
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
            var logger = sp.GetRequiredService<ILogger<PulsarReadinessSignal>>();
            return new PulsarReadinessSignal(serviceUrl, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Pulsar readiness signal using a service URL factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="serviceUrlFactory">Factory that produces the service URL using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Pulsar readiness signals in staged execution.
    /// The service URL factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes service URLs available for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store service URL
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("pulsar-container",
    ///     async ct => await infrastructure.StartPulsarAsync(), stage: 0);
    /// 
    /// // Stage 3: Use service URL from infrastructure
    /// services.AddPulsarReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().PulsarServiceUrl,
    ///     options =>
    ///     {
    ///         options.Stage = 3;
    ///         options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;
    ///         options.WithTopic("persistent://public/default/orders");
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddPulsarReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> serviceUrlFactory,
        Action<PulsarReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(serviceUrlFactory, nameof(serviceUrlFactory));

        var options = new PulsarReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new PulsarReadinessSignalFactory(serviceUrlFactory, options);

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
