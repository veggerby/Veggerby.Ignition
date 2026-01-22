using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Http;

/// <summary>
/// Extension methods for registering HTTP readiness signals with dependency injection.
/// </summary>
public static class HttpIgnitionExtensions
{
    /// <summary>
    /// Registers an HTTP readiness signal for the specified URL.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="url">Target URL to check for readiness.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "http-readiness". For basic connectivity checks,
    /// no additional configuration is required. To validate response content or customize
    /// expected status codes, use the <paramref name="configure"/> delegate.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddHttpReadiness("https://api.example.com/health", options =>
    /// {
    ///     options.ExpectedStatusCodes = [200, 204];
    ///     options.ValidateResponse = async (response) =>
    ///         (await response.Content.ReadAsStringAsync()).Contains("healthy");
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHttpReadiness(
        this IServiceCollection services,
        string url,
        Action<HttpReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));

        var options = new HttpReadinessOptions();
        configure?.Invoke(options);

        services.AddHttpClient();

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new HttpReadinessSignalFactory(_ => url, options);
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
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var logger = sp.GetRequiredService<ILogger<HttpReadinessSignal>>();

            return new HttpReadinessSignal(httpClient, url, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an HTTP readiness signal using a URL factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="urlFactory">Factory that produces the URL using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for HTTP readiness signals in staged execution.
    /// The URL factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes URLs available for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store URL
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("http-container",
    ///     async ct => await infrastructure.StartApiAsync(), stage: 0);
    /// 
    /// // Stage 1: Use URL from infrastructure
    /// services.AddHttpReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().ApiUrl,
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddHttpReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> urlFactory,
        Action<HttpReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(urlFactory, nameof(urlFactory));

        var options = new HttpReadinessOptions();
        configure?.Invoke(options);

        services.AddHttpClient();

        var innerFactory = new HttpReadinessSignalFactory(urlFactory, options);

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
