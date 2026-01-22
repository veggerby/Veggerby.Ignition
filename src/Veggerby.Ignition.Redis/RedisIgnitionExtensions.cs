using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Veggerby.Ignition.Redis;

/// <summary>
/// Extension methods for registering Redis readiness signals with dependency injection.
/// </summary>
public static class RedisIgnitionExtensions
{
    /// <summary>
    /// Registers a Redis readiness signal using a connection string.
    /// Creates a new <see cref="IConnectionMultiplexer"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "redis-readiness". For connection-only verification,
    /// no additional configuration is required. To execute PING or test key operations,
    /// use the <paramref name="configure"/> delegate to specify the verification strategy.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple usage
    /// services.AddRedisReadiness("localhost:6379", options =>
    /// {
    ///     options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
    ///     options.TestKeyPrefix = "ignition:readiness:";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// 
    /// // Staged execution
    /// services.AddRedisReadiness("localhost:6379", options =>
    /// {
    ///     options.Stage = 2;
    ///     options.VerificationStrategy = RedisVerificationStrategy.Ping;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<RedisReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var options = new RedisReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new RedisReadinessSignalFactory(_ => connectionString, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        // Register the connection multiplexer as singleton if not already registered
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var configOptions = ConfigurationOptions.Parse(connectionString);
            // Ensure resilient connection: retry until timeout instead of failing immediately
            configOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(configOptions);
        });

        services.AddSingleton<IIgnitionSignalFactory>(sp =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisReadinessSignal>>();
            var signal = new RedisReadinessSignal(multiplexer, options, logger);
            
            // Simple wrapper factory
            return new SimpleRedisSignalFactory(signal);
        });

        return services;
    }

    /// <summary>
    /// Registers a Redis readiness signal using an existing <see cref="IConnectionMultiplexer"/>
    /// from the DI container, or a connection string factory for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload expects an <see cref="IConnectionMultiplexer"/> to be already registered
    /// in the DI container. Use this when you have custom connection configuration or want
    /// to share a connection multiplexer across multiple components.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the connection multiplexer
    /// services.AddSingleton&lt;IConnectionMultiplexer&gt;(sp =>
    ///     ConnectionMultiplexer.Connect("localhost:6379"));
    ///
    /// // Then register readiness signal
    /// services.AddRedisReadiness(options =>
    /// {
    ///     options.VerificationStrategy = RedisVerificationStrategy.Ping;
    ///     options.Timeout = TimeSpan.FromSeconds(3);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisReadiness(
        this IServiceCollection services,
        Action<RedisReadinessOptions>? configure = null)
    {
        var options = new RedisReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need connection string factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddRedisReadiness() requires a connection string factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> connectionStringFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignalFactory>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisReadinessSignal>>();
            // Use factory pattern to defer multiplexer resolution until signal executes
            var signal = new RedisReadinessSignal(
                () => sp.GetRequiredService<IConnectionMultiplexer>(),
                options,
                logger);
            
            return new SimpleRedisSignalFactory(signal);
        });

        return services;
    }

    /// <summary>
    /// Registers a Redis readiness signal using a connection string factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the Redis connection string using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Redis readiness signals in staged execution.
    /// The connection string factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store connection string
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("redis-container",
    ///     async ct => await infrastructure.StartRedisAsync(), stage: 0);
    /// 
    /// // Stage 2: Use connection string from infrastructure
    /// services.AddRedisReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().RedisConnectionString,
    ///     options =>
    ///     {
    ///         options.Stage = 2;
    ///         options.VerificationStrategy = RedisVerificationStrategy.Ping;
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        Action<RedisReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory, nameof(connectionStringFactory));

        var options = new RedisReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new RedisReadinessSignalFactory(connectionStringFactory, options);

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

/// <summary>
/// Simple factory wrapper for pre-created Redis signals.
/// </summary>
internal sealed class SimpleRedisSignalFactory : IIgnitionSignalFactory
{
    private readonly IIgnitionSignal _signal;

    public SimpleRedisSignalFactory(IIgnitionSignal signal)
    {
        _signal = signal ?? throw new ArgumentNullException(nameof(signal));
    }

    public string Name => _signal.Name;
    public TimeSpan? Timeout => _signal.Timeout;
    public int? Stage => null;

    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider) => _signal;
}
