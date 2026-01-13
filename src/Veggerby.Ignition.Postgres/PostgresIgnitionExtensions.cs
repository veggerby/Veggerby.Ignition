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
            
            // Use factory pattern to defer data source resolution until signal executes
            return new PostgresReadinessSignal(
                () => sp.GetRequiredService<NpgsqlDataSource>(),
                options,
                logger);
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

    /// <summary>
    /// Registers a PostgreSQL readiness signal using a connection string factory with a specific stage/phase number for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the PostgreSQL connection string using the service provider.</param>
    /// <param name="stage">The stage/phase number (0 = infrastructure, 1 = services, 2 = workers, etc.).</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for PostgreSQL readiness signals in staged execution.
    /// The connection string factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store connection string
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("postgres-container",
    ///     async ct => await infrastructure.StartPostgresAsync(), stage: 0);
    /// 
    /// // Stage 1: Use connection string from infrastructure
    /// services.AddPostgresReadinessWithStage(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().PostgresConnectionString,
    ///     stage: 1,
    ///     options =>
    ///     {
    ///         options.ValidationQuery = "SELECT 1";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddPostgresReadinessWithStage(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        int stage,
        Action<PostgresReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory);

        if (stage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stage), "Stage number cannot be negative.");
        }

        services.AddIgnitionSignalFromFactoryWithStage(
            "postgres-readiness",
            sp =>
            {
                var connectionString = connectionStringFactory(sp);
                var options = new PostgresReadinessOptions();
                configure?.Invoke(options);

                var logger = sp.GetRequiredService<ILogger<PostgresReadinessSignal>>();
                return new PostgresReadinessSignal(connectionString, options, logger);
            },
            stage);

        return services;
    }
}
