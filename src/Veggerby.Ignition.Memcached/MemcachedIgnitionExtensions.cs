using System.Collections.Generic;
using System.Linq;

using Enyim.Caching;
using Enyim.Caching.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Memcached;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Memcached readiness signals with dependency injection.
/// </summary>
public static class MemcachedIgnitionExtensions
{
    /// <summary>
    /// Registers a Memcached readiness signal using server endpoints.
    /// Creates a new <see cref="IMemcachedClient"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="servers">Memcached server endpoints (host:port format).</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The signal name defaults to "memcached-readiness". For connection-only verification,
    /// no additional configuration is required. To execute stats or test key operations,
    /// use the <paramref name="configure"/> delegate to specify the verification strategy.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
    /// {
    ///     options.VerificationStrategy = MemcachedVerificationStrategy.TestKey;
    ///     options.TestKeyPrefix = "ignition:readiness:";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// 
    /// // Staged execution
    /// services.AddMemcachedReadiness(new[] { "localhost:11211" }, options =>
    /// {
    ///     options.Stage = 1;
    ///     options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMemcachedReadiness(
        this IServiceCollection services,
        IEnumerable<string> servers,
        Action<MemcachedReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(servers, nameof(servers));

        var serverList = servers.ToList();
        if (serverList.Count == 0)
        {
            throw new ArgumentException("At least one server endpoint is required", nameof(servers));
        }

        var options = new MemcachedReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            // Register Memcached client if not already registered
            services.AddEnyimMemcached(opts =>
            {
                foreach (var server in serverList)
                {
                    // Parse host:port format
                    var parts = server.Split(':');
                    var host = parts[0];
                    var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 11211;
                    opts.AddServer(host, port);
                }
            });

            var innerFactory = new MemcachedReadinessSignalFactory(options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        // Register Memcached client as singleton if not already registered
        services.AddEnyimMemcached(opts =>
        {
            foreach (var server in serverList)
            {
                // Parse host:port format
                var parts = server.Split(':');
                var host = parts[0];
                var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 11211;
                opts.AddServer(host, port);
            }
        });

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var client = sp.GetRequiredService<IMemcachedClient>();
            var logger = sp.GetRequiredService<ILogger<MemcachedReadinessSignal>>();
            return new MemcachedReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Memcached readiness signal using an existing <see cref="IMemcachedClient"/>
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload expects an <see cref="IMemcachedClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client across multiple components.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the Memcached client
    /// services.AddEnyimMemcached(options =>
    /// {
    ///     options.AddServer("localhost:11211");
    ///     options.Protocol = MemcachedProtocol.Binary;
    /// });
    ///
    /// // Then register readiness signal
    /// services.AddMemcachedReadiness(options =>
    /// {
    ///     options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
    ///     options.Timeout = TimeSpan.FromSeconds(3);
    /// });
    /// 
    /// // Staged execution
    /// services.AddMemcachedReadiness(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.VerificationStrategy = MemcachedVerificationStrategy.TestKey;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMemcachedReadiness(
        this IServiceCollection services,
        Action<MemcachedReadinessOptions>? configure = null)
    {
        var options = new MemcachedReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new MemcachedReadinessSignalFactory(options);
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
            var client = sp.GetRequiredService<IMemcachedClient>();
            var logger = sp.GetRequiredService<ILogger<MemcachedReadinessSignal>>();
            return new MemcachedReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a Memcached readiness signal using a factory-based approach for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Memcached readiness signals in staged execution.
    /// The Memcached client is resolved when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and configures Memcached client for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and configure Memcached client
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("memcached-container",
    ///     async ct => await infrastructure.StartMemcachedAsync(), stage: 0);
    /// 
    /// // Register Memcached client
    /// services.AddEnyimMemcached(options =>
    /// {
    ///     options.AddServer("localhost:11211");
    /// });
    /// 
    /// // Stage 1: Use Memcached client
    /// services.AddMemcachedReadinessFactory(options =>
    /// {
    ///     options.Stage = 1;
    ///     options.VerificationStrategy = MemcachedVerificationStrategy.Stats;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMemcachedReadinessFactory(
        this IServiceCollection services,
        Action<MemcachedReadinessOptions>? configure = null)
    {
        var options = new MemcachedReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new MemcachedReadinessSignalFactory(options);

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
