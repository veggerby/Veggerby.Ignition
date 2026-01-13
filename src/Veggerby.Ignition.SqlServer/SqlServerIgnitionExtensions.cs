using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.SqlServer;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering SQL Server readiness signals with dependency injection.
/// </summary>
public static class SqlServerIgnitionExtensions
{
    /// <summary>
    /// Registers a SQL Server readiness signal using a connection factory from DI.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This is the recommended modern pattern. Requires <see cref="Func{SqlConnection}"/> to be registered in DI.
    /// Register the factory before calling this method:
    /// </para>
    /// <code>
    /// services.AddSingleton&lt;Func&lt;SqlConnection&gt;&gt;(() => new SqlConnection(connectionString));
    /// services.AddSqlServerReadiness(options => options.Timeout = TimeSpan.FromSeconds(5));
    /// </code>
    /// <para>
    /// For simpler scenarios without factory registration, use the overload accepting a connection string.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSqlServerReadiness(
        this IServiceCollection services,
        Action<SqlServerReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new SqlServerReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<SqlServerReadinessSignal>>();
            
            // Use nested factory to defer both factory retrieval and connection creation
            return new SqlServerReadinessSignal(
                () => sp.GetRequiredService<Func<SqlConnection>>()(),
                options,
                logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a SQL Server readiness signal using a connection string.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "sqlserver-readiness". For connection-only verification,
    /// no additional configuration is required. To execute a validation query, use the
    /// <paramref name="configure"/> delegate to specify the query.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSqlServerReadiness("Server=localhost;Database=MyDb;Trusted_Connection=True;", options =>
    /// {
    ///     options.ValidationQuery = "SELECT 1";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSqlServerReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<SqlServerReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new SqlServerReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<SqlServerReadinessSignal>>();
            return new SqlServerReadinessSignal(connectionString, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a SQL Server readiness signal using a connection string factory with a specific stage/phase number for staged execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the SQL Server connection string using the service provider.</param>
    /// <param name="stage">The stage/phase number (0 = infrastructure, 1 = services, 2 = workers, etc.).</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for SQL Server readiness signals in staged execution.
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
    /// services.AddIgnitionFromTaskWithStage("sqlserver-container",
    ///     async ct => await infrastructure.StartSqlServerAsync(), stage: 0);
    /// 
    /// // Stage 1: Use connection string from infrastructure
    /// services.AddSqlServerReadinessWithStage(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().SqlServerConnectionString,
    ///     stage: 1,
    ///     options =>
    ///     {
    ///         options.ValidationQuery = "SELECT 1";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddSqlServerReadinessWithStage(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        int stage,
        Action<SqlServerReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory);

        if (stage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stage), "Stage number cannot be negative.");
        }

        services.AddIgnitionSignalFromFactoryWithStage(
            "sqlserver-readiness",
            sp =>
            {
                var connectionString = connectionStringFactory(sp);
                var options = new SqlServerReadinessOptions();
                configure?.Invoke(options);

                var logger = sp.GetRequiredService<ILogger<SqlServerReadinessSignal>>();
                return new SqlServerReadinessSignal(connectionString, options, logger);
            },
            stage);

        return services;
    }
}
