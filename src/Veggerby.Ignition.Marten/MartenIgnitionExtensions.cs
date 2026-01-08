using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Marten;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Marten document store readiness signals with dependency injection.
/// </summary>
public static class MartenIgnitionExtensions
{
    /// <summary>
    /// Registers a Marten document store readiness signal.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "marten-readiness". The document store instance is resolved
    /// from the DI container. Ensure Marten is configured before calling this method.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMarten(/* configure Marten */);
    /// 
    /// services.AddMartenReadiness(options =>
    /// {
    ///     options.VerifyDocumentStore = true;
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMartenReadiness(
        this IServiceCollection services,
        Action<MartenReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new MartenReadinessOptions();
            configure?.Invoke(options);

            var documentStore = sp.GetRequiredService<IDocumentStore>();
            var logger = sp.GetRequiredService<ILogger<MartenReadinessSignal>>();
            return new MartenReadinessSignal(documentStore, options, logger);
        });

        return services;
    }
}
