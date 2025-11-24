using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Veggerby.Ignition;

/// <summary>
/// Represents a reusable, packaged set of ignition signals that can be registered as a unit.
/// Bundles enable modular, composable startup readiness patterns (e.g., "Redis Starter Bundle", "Kafka Consumer Bundle").
/// </summary>
/// <remarks>
/// Bundles provide a convenient way to group related signals with optional per-bundle configuration overrides
/// (timeouts, policies, dependencies). Each bundle registers its signals and optionally a dependency graph
/// when <see cref="ConfigureBundle(IServiceCollection, Action{IgnitionBundleOptions}?)"/> is invoked.
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
    /// Configure the bundle by registering its signals and optional dependency graph.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for per-bundle options.</param>
    /// <remarks>
    /// This method is invoked once during DI container setup when the bundle is registered via
    /// <see cref="IgnitionExtensions.AddIgnitionBundle(IServiceCollection, IIgnitionBundle, Action{IgnitionBundleOptions}?)"/>.
    /// Implementations should register all signals and optionally configure a dependency graph if signals have prerequisites.
    /// </remarks>
    void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null);
}
