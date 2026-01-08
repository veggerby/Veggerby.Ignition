using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using global::MassTransit;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MassTransit;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering MassTransit bus readiness signals with dependency injection.
/// </summary>
public static class MassTransitIgnitionExtensions
{
    /// <summary>
    /// Registers a MassTransit bus readiness signal.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This extension method registers an ignition signal that verifies the MassTransit bus is ready
    /// by leveraging MassTransit's built-in health checks. The signal works with any MassTransit transport
    /// (RabbitMQ, Azure Service Bus, in-memory, etc.) as long as an <see cref="IBus"/> instance is registered
    /// in the DI container.
    /// </para>
    /// <para>
    /// The signal name defaults to "masstransit-readiness". The check verifies that the bus status
    /// reaches <see cref="BusHealthStatus.Healthy"/> within the configured timeout.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMassTransit(x =>
    /// {
    ///     x.UsingRabbitMq((context, cfg) =>
    ///     {
    ///         cfg.Host("localhost", "/");
    ///         cfg.ConfigureEndpoints(context);
    ///     });
    /// });
    /// 
    /// services.AddMassTransitReadiness(options =>
    /// {
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    ///     options.BusReadyTimeout = TimeSpan.FromSeconds(45);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMassTransitReadiness(
        this IServiceCollection services,
        Action<MassTransitReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new MassTransitReadinessOptions();
            configure?.Invoke(options);

            var bus = sp.GetRequiredService<IBus>();
            var logger = sp.GetRequiredService<ILogger<MassTransitReadinessSignal>>();

            return new MassTransitReadinessSignal(bus, options, logger);
        });

        return services;
    }
}
