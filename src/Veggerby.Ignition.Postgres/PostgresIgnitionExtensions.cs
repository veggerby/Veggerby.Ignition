using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Postgres;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering PostgreSQL readiness signals with dependency injection.
/// </summary>
public static class PostgresIgnitionExtensions
{
    /// <summary>
    /// Registers a PostgreSQL readiness signal using a connection string.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "postgres-readiness". For connection-only verification,
    /// no additional configuration is required. To execute a validation query, use the
    /// <paramref name="configure"/> delegate to specify the query.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddPostgresReadiness("Host=localhost;Database=mydb;Username=user;Password=pass", options =>
    /// {
    ///     options.ValidationQuery = "SELECT 1";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddPostgresReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<PostgresReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new PostgresReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<PostgresReadinessSignal>>();
            return new PostgresReadinessSignal(connectionString, options, logger);
        });

        return services;
    }
}
