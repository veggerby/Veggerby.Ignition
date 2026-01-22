using System;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Kafka;

/// <summary>
/// Extension methods for registering Kafka readiness signals with dependency injection.
/// </summary>
public static class KafkaIgnitionExtensions
{
    /// <summary>
    /// Registers a Kafka readiness signal using bootstrap servers.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="bootstrapServers">Kafka bootstrap servers (e.g., "localhost:9092").</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "kafka-readiness". For connection-only verification,
    /// no additional configuration is required. To verify topics or consumer groups, use the
    /// <paramref name="configure"/> delegate to specify verification settings.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple cluster connectivity check
    /// services.AddKafkaReadiness("localhost:9092");
    /// 
    /// // Topic verification
    /// services.AddKafkaReadiness("localhost:9092", options =>
    /// {
    ///     options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
    ///     options.WithTopic("orders");
    ///     options.WithTopic("payments");
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// 
    /// // Staged execution
    /// services.AddKafkaReadiness("localhost:9092", options =>
    /// {
    ///     options.Stage = 3;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKafkaReadiness(
        this IServiceCollection services,
        string bootstrapServers,
        Action<KafkaReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bootstrapServers, nameof(bootstrapServers));

        var options = new KafkaReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new KafkaReadinessSignalFactory(_ => bootstrapServers, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        return AddKafkaReadiness(services, producerConfig, options);
    }

    /// <summary>
    /// Registers a Kafka readiness signal using a pre-configured producer config.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="producerConfig">Pre-configured Kafka producer configuration.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when you need fine-grained control over producer configuration
    /// (e.g., SSL/TLS settings, SASL authentication, custom client properties).
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate, but note
    /// that this overload requires a pre-configured producer config and cannot properly support
    /// staged factory-based scenarios. Use the bootstrap servers factory overload instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// var producerConfig = new ProducerConfig
    /// {
    ///     BootstrapServers = "kafka.example.com:9093",
    ///     SecurityProtocol = SecurityProtocol.SaslSsl,
    ///     SaslMechanism = SaslMechanism.Plain,
    ///     SaslUsername = "user",
    ///     SaslPassword = "password"
    /// };
    /// 
    /// services.AddKafkaReadiness(producerConfig, options =>
    /// {
    ///     options.VerificationStrategy = KafkaVerificationStrategy.ProducerTest;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKafkaReadiness(
        this IServiceCollection services,
        ProducerConfig producerConfig,
        Action<KafkaReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(producerConfig, nameof(producerConfig));

        var options = new KafkaReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need bootstrap servers factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddKafkaReadiness() requires a bootstrap servers factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> bootstrapServersFactory parameter.");
        }

        return AddKafkaReadiness(services, producerConfig, options);
    }

    /// <summary>
    /// Internal helper to add Kafka readiness with pre-configured producer config and options.
    /// </summary>
    private static IServiceCollection AddKafkaReadiness(
        this IServiceCollection services,
        ProducerConfig producerConfig,
        KafkaReadinessOptions options)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KafkaReadinessSignal>>();
            return new KafkaReadinessSignal(producerConfig, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Kafka readiness signal using a producer config resolved from dependency injection.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when you have an existing <see cref="ProducerConfig"/> registered in DI.
    /// The signal will resolve the producer config from the service provider.
    /// This is the recommended approach for modern .NET applications using dependency injection.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate, but note
    /// that this overload requires an existing producer config in DI and cannot properly support
    /// staged factory-based scenarios. Use the bootstrap servers factory overload instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSingleton&lt;ProducerConfig&gt;(sp =&gt;
    /// {
    ///     var config = sp.GetRequiredService&lt;IConfiguration&gt;();
    ///     return new ProducerConfig
    ///     {
    ///         BootstrapServers = config["Kafka:BootstrapServers"]
    ///     };
    /// });
    /// 
    /// services.AddKafkaReadiness(options =>
    /// {
    ///     options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
    ///     options.WithTopic("events");
    ///     options.Timeout = TimeSpan.FromSeconds(20);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKafkaReadiness(
        this IServiceCollection services,
        Action<KafkaReadinessOptions>? configure = null)
    {
        var options = new KafkaReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need bootstrap servers factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddKafkaReadiness() requires a bootstrap servers factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> bootstrapServersFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KafkaReadinessSignal>>();
            // Use factory pattern to defer producer config resolution until signal executes
            return new KafkaReadinessSignal(
                () => sp.GetRequiredService<ProducerConfig>(),
                options,
                logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Kafka readiness signal using a bootstrap servers factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="bootstrapServersFactory">Factory that produces the bootstrap servers string using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Kafka readiness signals in staged execution.
    /// The bootstrap servers factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes bootstrap servers available for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store bootstrap servers
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("kafka-container",
    ///     async ct => await infrastructure.StartKafkaAsync(), stage: 0);
    /// 
    /// // Stage 3: Use bootstrap servers from infrastructure
    /// services.AddKafkaReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().KafkaBootstrapServers,
    ///     options =>
    ///     {
    ///         options.Stage = 3;
    ///         options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;
    ///         options.WithTopic("orders");
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddKafkaReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> bootstrapServersFactory,
        Action<KafkaReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(bootstrapServersFactory, nameof(bootstrapServersFactory));

        var options = new KafkaReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new KafkaReadinessSignalFactory(bootstrapServersFactory, options);

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
