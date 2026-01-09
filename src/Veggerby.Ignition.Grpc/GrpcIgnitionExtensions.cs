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

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new GrpcReadinessOptions();
            configure?.Invoke(options);

            var channel = GrpcChannel.ForAddress(serviceUrl);
            var logger = sp.GetRequiredService<ILogger<GrpcReadinessSignal>>();

            return new GrpcReadinessSignal(channel, serviceUrl, options, logger);
        });

        return services;
    }
}
