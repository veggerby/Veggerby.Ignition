using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Orleans;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Orleans readiness signals with dependency injection.
/// </summary>
public static class OrleansIgnitionExtensions
{
    /// <summary>
    /// Registers an Orleans readiness signal using the registered <see cref="IClusterClient"/>.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "orleans-readiness". Requires an <see cref="IClusterClient"/>
    /// to be registered in the service collection. Verifies that the cluster client is available
    /// from the DI container.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddOrleansReadiness(options =>
    /// {
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddOrleansReadiness(
        this IServiceCollection services,
        Action<OrleansReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new OrleansReadinessOptions();
            configure?.Invoke(options);

            var clusterClient = sp.GetRequiredService<IClusterClient>();
            var logger = sp.GetRequiredService<ILogger<OrleansReadinessSignal>>();

            return new OrleansReadinessSignal(clusterClient, options, logger);
        });

        return services;
    }
}
