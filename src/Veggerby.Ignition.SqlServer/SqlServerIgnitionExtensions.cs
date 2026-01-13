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
            var connectionFactory = sp.GetService<Func<SqlConnection>>()
                ?? throw new InvalidOperationException(
                    "Func<SqlConnection> is not registered in DI. " +
                    "Register a connection factory or use AddSqlServerReadiness(connectionString) overload.");

            var options = new SqlServerReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<SqlServerReadinessSignal>>();
            return new SqlServerReadinessSignal(connectionFactory, options, logger);
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
}
