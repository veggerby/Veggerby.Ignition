using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Grpc;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering gRPC readiness signals with dependency injection.
/// </summary>
public static class GrpcIgnitionExtensions
{
    /// <summary>
    /// Registers a gRPC readiness signal for the specified service URL.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="serviceUrl">Target gRPC service URL.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "grpc-readiness". Creates a gRPC channel for the specified URL
    /// and uses the gRPC health check protocol (grpc.health.v1.Health) to verify service readiness.
    /// The channel is created as a singleton and will be disposed when the application shuts down.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddGrpcReadiness("https://grpc.example.com", options =>
    /// {
    ///     options.ServiceName = "myservice";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddGrpcReadiness(
        this IServiceCollection services,
        string serviceUrl,
        Action<GrpcReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl, nameof(serviceUrl));

        var options = new GrpcReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new GrpcReadinessSignalFactory(_ => serviceUrl, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        services.AddSingleton(sp =>
        {
            var channel = GrpcChannel.ForAddress(serviceUrl);
            return channel;
        });

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var channel = sp.GetRequiredService<GrpcChannel>();
            var logger = sp.GetRequiredService<ILogger<GrpcReadinessSignal>>();

            return new GrpcReadinessSignal(channel, serviceUrl, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a gRPC readiness signal using a service URL factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="serviceUrlFactory">Factory that produces the service URL using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for gRPC readiness signals in staged execution.
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
    /// // Stage 0: Start container and store URL
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("grpc-container",
    ///     async ct => await infrastructure.StartGrpcServiceAsync(), stage: 0);
    /// 
    /// // Stage 1: Use URL from infrastructure
    /// services.AddGrpcReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().GrpcServiceUrl,
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.ServiceName = "myservice";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddGrpcReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> serviceUrlFactory,
        Action<GrpcReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(serviceUrlFactory, nameof(serviceUrlFactory));

        var options = new GrpcReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new GrpcReadinessSignalFactory(serviceUrlFactory, options);

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
