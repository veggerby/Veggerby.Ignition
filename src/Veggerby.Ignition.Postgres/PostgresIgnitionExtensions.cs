using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Postgres;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering PostgreSQL readiness signals with dependency injection.
/// </summary>
public static class PostgresIgnitionExtensions
{
    /// <summary>
    /// Registers a PostgreSQL readiness signal.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method automatically resolves <see cref="NpgsqlDataSource"/> from the DI container
    /// if registered, otherwise falls back to connection string resolution.
    /// </para>
    /// <para>
    /// The signal name defaults to "postgres-readiness". For connection-only verification,
    /// no additional configuration is required. To execute a validation query, use the
    /// <paramref name="configure"/> delegate to specify the query.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Using NpgsqlDataSource (recommended):</para>
    /// <code>
    /// // Register data source
    /// var dataSourceBuilder = new NpgsqlDataSourceBuilder("Host=localhost;Database=mydb;Username=user;Password=pass");
    /// services.AddSingleton(dataSourceBuilder.Build());
    /// 
    /// // Register readiness signal
    /// services.AddPostgresReadiness(options =>
    /// {
    ///     options.ValidationQuery = "SELECT 1";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddPostgresReadiness(
        this IServiceCollection services,
        Action<PostgresReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new PostgresReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<PostgresReadinessSignal>>();
            
            // Prefer NpgsqlDataSource from DI (recommended approach)
            var dataSource = sp.GetService<NpgsqlDataSource>();
            if (dataSource != null)
            {
                return new PostgresReadinessSignal(dataSource, options, logger);
            }

            // Fallback: try to get connection string from configuration
            throw new InvalidOperationException(
                "No NpgsqlDataSource registered in DI container. " +
                "Register NpgsqlDataSource using services.AddSingleton<NpgsqlDataSource>() or use the " +
                "AddPostgresReadiness(connectionString, configure) overload.");
        });

        return services;
    }

    /// <summary>
    /// Registers a PostgreSQL readiness signal using a connection string.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload creates an internal <see cref="NpgsqlDataSource"/> that will be disposed
    /// after readiness verification. For production scenarios with connection pooling,
    /// prefer using <see cref="AddPostgresReadiness(IServiceCollection, Action{PostgresReadinessOptions}?)"/>
    /// with <see cref="NpgsqlDataSource"/> registered in DI.
    /// </para>
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
