using Microsoft.Extensions.DependencyInjection;
using Veggerby.Ignition.Metrics;

namespace Veggerby.Ignition.Metrics.OpenTelemetry;

/// <summary>
/// Extension methods for registering OpenTelemetry metrics with Ignition.
/// </summary>
public static class OpenTelemetryIgnitionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry metrics for Ignition.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method registers <see cref="OpenTelemetryIgnitionMetrics"/> as the singleton
    /// implementation of <see cref="IIgnitionMetrics"/> so that Ignition can emit metrics
    /// via OpenTelemetry.
    /// </para>
    /// <para>
    /// After calling this method, ensure you configure OpenTelemetry to collect metrics
    /// from the "Veggerby.Ignition" meter by calling <c>.AddMeter("Veggerby.Ignition")</c>
    /// in your OpenTelemetry metrics configuration.
    /// </para>
    /// <example>
    /// <code>
    /// builder.Services.AddIgnition();
    /// builder.Services.AddOpenTelemetryIgnitionMetrics();
    /// 
    /// builder.Services.AddOpenTelemetry()
    ///     .WithMetrics(metrics => metrics
    ///         .AddMeter("Veggerby.Ignition")
    ///         .AddPrometheusExporter());
    /// </code>
    /// </example>
    /// </remarks>
    public static IServiceCollection AddOpenTelemetryIgnitionMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        services.AddSingleton<IIgnitionMetrics, OpenTelemetryIgnitionMetrics>();
        services.AddOptions<IgnitionOptions>()
            .Configure<IIgnitionMetrics>((options, metrics) => options.Metrics = metrics);

        return services;
    }
}
