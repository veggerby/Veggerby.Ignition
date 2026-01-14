using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RabbitMQ.Client;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.RabbitMq;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering RabbitMQ readiness signals with dependency injection.
/// </summary>
public static class RabbitMqIgnitionExtensions
{
    /// <summary>
    /// Registers a RabbitMQ readiness signal using a connection string.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">RabbitMQ connection string (e.g., "amqp://guest:guest@localhost:5672/").</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "rabbitmq-readiness". For connection-only verification,
    /// no additional configuration is required. To verify queues or exchanges, use the
    /// <paramref name="configure"/> delegate to specify topology elements.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRabbitMqReadiness("amqp://localhost", options =>
    /// {
    ///     options.WithQueue("orders");
    ///     options.WithExchange("events");
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRabbitMqReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<RabbitMqReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString)
        };

        return AddRabbitMqReadiness(services, factory, configure);
    }

    /// <summary>
    /// Registers a RabbitMQ readiness signal using a pre-configured connection factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionFactory">Pre-configured RabbitMQ connection factory.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when you need fine-grained control over connection factory settings
    /// (e.g., SSL/TLS configuration, custom endpoints, credential management).
    /// </remarks>
    /// <example>
    /// <code>
    /// var factory = new ConnectionFactory
    /// {
    ///     HostName = "rabbitmq.example.com",
    ///     Port = 5671,
    ///     Ssl = new SslOption { Enabled = true }
    /// };
    /// 
    /// services.AddRabbitMqReadiness(factory, options =>
    /// {
    ///     options.PerformRoundTripTest = true;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRabbitMqReadiness(
        this IServiceCollection services,
        IConnectionFactory connectionFactory,
        Action<RabbitMqReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory, nameof(connectionFactory));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new RabbitMqReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<RabbitMqReadinessSignal>>();
            return new RabbitMqReadinessSignal(connectionFactory, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a RabbitMQ readiness signal using a connection factory resolved from dependency injection.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when you have an existing <see cref="IConnectionFactory"/> registered in DI.
    /// The signal will resolve the factory from the service provider.
    /// This is the recommended approach for modern .NET applications using dependency injection.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSingleton&lt;IConnectionFactory&gt;(sp =&gt;
    /// {
    ///     var factory = new ConnectionFactory
    ///     {
    ///         Uri = new Uri("amqp://localhost:5672")
    ///     };
    ///     return factory;
    /// });
    /// 
    /// services.AddRabbitMqReadiness(options =>
    /// {
    ///     options.PerformRoundTripTest = true;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRabbitMqReadiness(
        this IServiceCollection services,
        Action<RabbitMqReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new RabbitMqReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<RabbitMqReadinessSignal>>();
            // Use factory pattern to defer connection factory resolution until signal executes
            return new RabbitMqReadinessSignal(
                () => sp.GetRequiredService<IConnectionFactory>(),
                options,
                logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a RabbitMQ readiness signal using a connection string factory with a specific stage/phase number for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the RabbitMQ connection string using the service provider.</param>
    /// <param name="stage">The stage/phase number (0 = infrastructure, 1 = services, 2 = workers, etc.).</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for RabbitMQ readiness signals in staged execution.
    /// The connection string factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store connection string
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("rabbitmq-container",
    ///     async ct => await infrastructure.StartRabbitMqAsync(), stage: 0);
    /// 
    /// // Stage 3: Use connection string from infrastructure
    /// services.AddRabbitMqReadinessWithStage(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().RabbitMqConnectionString,
    ///     stage: 3,
    ///     options =>
    ///     {
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddRabbitMqReadinessWithStage(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        int stage,
        Action<RabbitMqReadinessOptions>? configure = null)
    {
        return AddRabbitMqReadinessWithStage(services, connectionStringFactory, stage, IgnitionExecutionMode.Parallel, configure);
    }

    /// <summary>
    /// Registers a RabbitMQ readiness signal using a connection string factory with a specific stage/phase number and execution mode.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the RabbitMQ connection string using the service provider.</param>
    /// <param name="stage">The stage/phase number (0 = infrastructure, 1 = services, 2 = workers, etc.).</param>
    /// <param name="executionMode">Execution mode for this stage (Sequential, Parallel, DependencyAware).</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddRabbitMqReadinessWithStage(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        int stage,
        IgnitionExecutionMode executionMode,
        Action<RabbitMqReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory, nameof(connectionStringFactory));

        var options = new RabbitMqReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new RabbitMqReadinessSignalFactory(connectionStringFactory, options);
        var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, stage);

        services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

        // Configure the stage's execution mode
        services.Configure<IgnitionStageConfiguration>(config =>
        {
            config.EnsureStage(stage, executionMode);
        });

        return services;
    }
}
