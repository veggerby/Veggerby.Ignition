using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a reusable, packaged set of ignition signals that can be registered as a unit.
/// Bundles enable modular, composable startup readiness patterns (e.g., "Redis Starter Bundle", "Kafka Consumer Bundle").
/// </summary>
/// <remarks>
/// Bundles provide a convenient way to group related signals with optional per-bundle configuration overrides
/// (timeouts, policies, dependencies). Each bundle registers its signals via an <see cref="IIgnitionRegistrar"/>
/// when <see cref="ConfigureBundle(IIgnitionRegistrar, Action{IgnitionBundleOptions}?)"/> is invoked.
/// 
/// Using <see cref="IIgnitionRegistrar"/> (rather than the raw <c>IServiceCollection</c>) prevents bundles
/// from registering arbitrary unrelated services and keeps bundle logic focused on signal registration.
///
/// Implementation guidelines:
/// - Keep bundle logic lightweight and focused on registration; avoid heavy initialization in the bundle itself.
/// - Use <see cref="IgnitionBundleOptions"/> to provide per-bundle timeout and policy overrides.
/// - Bundles should be deterministic and idempotent in their registration logic.
/// - Avoid introducing external dependencies; preserve the library's zero-dependency philosophy.
/// </remarks>
public interface IIgnitionBundle
{
    /// <summary>
    /// Human-friendly bundle name used for diagnostics and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Configure the bundle by registering its signals via the provided <see cref="IIgnitionRegistrar"/>.
    /// </summary>
    /// <param name="registrar">Narrowly-scoped registrar that only exposes ignition-signal registration methods.</param>
    /// <param name="configure">Optional configuration delegate for per-bundle options.</param>
    /// <remarks>
    /// This method is invoked once during DI container setup when the bundle is registered via
    /// <see cref="IgnitionExtensions.AddIgnitionBundle(Microsoft.Extensions.DependencyInjection.IServiceCollection, IIgnitionBundle, Action{IgnitionBundleOptions}?)"/>.
    /// Implementations register all signals via the <paramref name="registrar"/>. To enforce ordering
    /// among a bundle's signals, use <see cref="IgnitionExecutionMode.Sequential"/> on the coordinator
    /// (signals execute in registration order) or register the bundle signals with explicit stage numbers
    /// for staged execution.
    /// </remarks>
    void ConfigureBundle(IIgnitionRegistrar registrar, Action<IgnitionBundleOptions>? configure = null);
}
