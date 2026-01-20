using System;

using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Elasticsearch;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Elasticsearch readiness signals with dependency injection.
/// </summary>
public static class ElasticsearchIgnitionExtensions
{
    /// <summary>
    /// Registers an Elasticsearch readiness signal using a URI.
    /// Creates a new <see cref="ElasticsearchClient"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="uri">Elasticsearch cluster URI (e.g., "http://localhost:9200").</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "elasticsearch-readiness". For cluster health verification,
    /// no additional configuration is required. To verify indices, templates, or execute test queries,
    /// use the <paramref name="configure"/> delegate to specify the verification strategy and related options.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple cluster health check
    /// services.AddElasticsearchReadiness("http://localhost:9200");
    /// 
    /// // Index verification
    /// services.AddElasticsearchReadiness("http://localhost:9200", options =>
    /// {
    ///     options.VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists;
    ///     options.VerifyIndices.Add("my-index");
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// 
    /// // Staged execution
    /// services.AddElasticsearchReadiness("http://localhost:9200", options =>
    /// {
    ///     options.Stage = 2;
    ///     options.VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddElasticsearchReadiness(
        this IServiceCollection services,
        string uri,
        Action<ElasticsearchReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri, nameof(uri));

        var elasticsearchUri = new Uri(uri);
        return services.AddElasticsearchReadiness(_ => new ElasticsearchClientSettings(elasticsearchUri), configure);
    }

    /// <summary>
    /// Registers an Elasticsearch readiness signal using client settings.
    /// Creates a new <see cref="ElasticsearchClient"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="settings">Elasticsearch client settings.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when you need custom client configuration (authentication, certificates, etc.).
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"))
    ///     .Authentication(new BasicAuthentication("user", "password"));
    /// 
    /// services.AddElasticsearchReadiness(settings, options =>
    /// {
    ///     options.VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddElasticsearchReadiness(
        this IServiceCollection services,
        ElasticsearchClientSettings settings,
        Action<ElasticsearchReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(settings, nameof(settings));

        return services.AddElasticsearchReadiness(_ => settings, configure);
    }

    /// <summary>
    /// Registers an Elasticsearch readiness signal using a settings factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="settingsFactory">Factory that produces the Elasticsearch client settings using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Elasticsearch readiness signals in staged execution.
    /// The settings factory is invoked when the signal is created (when its stage is reached),
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
    /// // Stage 0: Start container and store connection info
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("elasticsearch-container",
    ///     async ct => await infrastructure.StartElasticsearchAsync(), stage: 0);
    /// 
    /// // Stage 2: Use connection info from infrastructure
    /// services.AddElasticsearchReadiness(
    ///     sp =>
    ///     {
    ///         var infra = sp.GetRequiredService&lt;InfrastructureManager&gt;();
    ///         return new ElasticsearchClientSettings(new Uri(infra.ElasticsearchUrl));
    ///     },
    ///     options =>
    ///     {
    ///         options.Stage = 2;
    ///         options.VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists;
    ///         options.VerifyIndices.Add("logs");
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddElasticsearchReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory,
        Action<ElasticsearchReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(settingsFactory, nameof(settingsFactory));

        var options = new ElasticsearchReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new ElasticsearchReadinessSignalFactory(settingsFactory, options);

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

    /// <summary>
    /// Registers an Elasticsearch readiness signal using an existing <see cref="ElasticsearchClient"/>
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload expects an <see cref="ElasticsearchClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// Note: This overload does not support staged execution. Use the factory-based overload for staged scenarios.
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the Elasticsearch client
    /// services.AddSingleton(new ElasticsearchClient(
    ///     new ElasticsearchClientSettings(new Uri("http://localhost:9200"))));
    ///
    /// // Then register readiness signal
    /// services.AddElasticsearchReadiness(options =>
    /// {
    ///     options.VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth;
    ///     options.Timeout = TimeSpan.FromSeconds(3);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddElasticsearchReadiness(
        this IServiceCollection services,
        Action<ElasticsearchReadinessOptions>? configure = null)
    {
        var options = new ElasticsearchReadinessOptions();
        configure?.Invoke(options);

        // Staged execution not supported with this overload
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddElasticsearchReadiness() requires a settings factory. " +
                "Use the overload that accepts Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignalFactory>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ElasticsearchReadinessSignal>>();
            // Use factory pattern to defer client resolution until signal executes
            var signal = new ElasticsearchReadinessSignal(
                () => sp.GetRequiredService<ElasticsearchClient>(),
                options,
                logger);

            return new SimpleElasticsearchSignalFactory(signal);
        });

        return services;
    }
}

/// <summary>
/// Simple factory wrapper for pre-created Elasticsearch signals.
/// </summary>
internal sealed class SimpleElasticsearchSignalFactory : IIgnitionSignalFactory
{
    private readonly IIgnitionSignal _signal;

    public SimpleElasticsearchSignalFactory(IIgnitionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal, nameof(signal));
        _signal = signal;
    }

    public string Name => _signal.Name;
    public TimeSpan? Timeout => _signal.Timeout;
    public int? Stage => null;

    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider) => _signal;
}
