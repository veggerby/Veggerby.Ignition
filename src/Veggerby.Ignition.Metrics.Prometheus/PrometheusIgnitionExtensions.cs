using Microsoft.Extensions.DependencyInjection;

using Veggerby.Ignition.Metrics;

namespace Veggerby.Ignition.Metrics.Prometheus;

/// <summary>
/// Extension methods for registering Prometheus metrics with Ignition.
/// </summary>
public static class PrometheusIgnitionExtensions
{
    /// <summary>
    /// Registers Prometheus metrics for Ignition.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers <see cref="PrometheusIgnitionMetrics"/> as the singleton
    /// implementation of <see cref="IIgnitionMetrics"/> and configures it in the
    /// <see cref="IgnitionOptions"/>.
    /// </para>
    /// <para>
    /// After calling this method, ensure you expose the Prometheus metrics endpoint
    /// by calling <c>app.MapMetrics()</c> in your application startup.
    /// </para>
    /// <example>
    /// <code>
    /// builder.Services.AddIgnition();
    /// builder.Services.AddPrometheusIgnitionMetrics();
    /// 
    /// var app = builder.Build();
    /// app.MapMetrics(); // Expose /metrics endpoint
    /// </code>
    /// </example>
    /// </remarks>
    public static IServiceCollection AddPrometheusIgnitionMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.AddSingleton<IIgnitionMetrics, PrometheusIgnitionMetrics>();
        return services;
    }
}
