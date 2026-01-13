using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Redis;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddRedisReadiness("localhost:6379", options =>
    /// {
    ///     options.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
    ///     options.TestKeyPrefix = "ignition:readiness:";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<RedisReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        // Register the connection multiplexer as singleton if not already registered
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new RedisReadinessOptions();
            configure?.Invoke(options);

            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<RedisReadinessSignal>>();
            return new RedisReadinessSignal(multiplexer, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Redis readiness signal using an existing <see cref="IConnectionMultiplexer"/>
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload expects an <see cref="IConnectionMultiplexer"/> to be already registered
    /// in the DI container. Use this when you have custom connection configuration or want
    /// to share a connection multiplexer across multiple components.
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
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new RedisReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<RedisReadinessSignal>>();
            // Use factory pattern to defer multiplexer resolution until signal executes
            return new RedisReadinessSignal(
                () => sp.GetRequiredService<IConnectionMultiplexer>(),
                options,
                logger);
        });

        return services;
    }
}
