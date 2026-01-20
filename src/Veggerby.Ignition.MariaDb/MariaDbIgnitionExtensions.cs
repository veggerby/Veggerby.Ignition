using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MariaDb;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering MariaDB readiness signals with dependency injection.
/// </summary>
public static class MariaDbIgnitionExtensions
{
    /// <summary>
    /// Registers a MariaDB readiness signal using a connection string.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">MariaDB connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload creates a connection using MySqlConnector for MariaDB connectivity.
    /// MariaDB is wire-compatible with MySQL, so MySqlConnector provides excellent performance
    /// and compatibility for both MariaDB and MySQL databases.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple usage
    /// services.AddMariaDbReadiness("Server=localhost;Database=mydb;User=root;Password=pass", options =>
    /// {
    ///     options.VerificationStrategy = MariaDbVerificationStrategy.SimpleQuery;
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// 
    /// // Staged execution
    /// services.AddMariaDbReadiness("Server=localhost;Database=mydb", options =>
    /// {
    ///     options.Stage = 1;
    ///     options.VerificationStrategy = MariaDbVerificationStrategy.Ping;
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMariaDbReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<MariaDbReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var options = new MariaDbReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new MariaDbReadinessSignalFactory(_ => connectionString, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MariaDbReadinessSignal>>();
            return new MariaDbReadinessSignal(connectionString, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a MariaDB readiness signal using a connection string factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the MariaDB connection string using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for MariaDB readiness signals in staged execution.
    /// The connection string factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store connection string
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("mariadb-container",
    ///     async ct => await infrastructure.StartMariaDbAsync(), stage: 0);
    /// 
    /// // Stage 1: Use connection string from infrastructure
    /// services.AddMariaDbReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().MariaDbConnectionString,
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.VerificationStrategy = MariaDbVerificationStrategy.TableExists;
    ///         options.VerifyTables.AddRange(new[] { "users", "products" });
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddMariaDbReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        Action<MariaDbReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory, nameof(connectionStringFactory));

        var options = new MariaDbReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new MariaDbReadinessSignalFactory(connectionStringFactory, options);

        // If Stage is specified, wrap with StagedIgnitionSignalFactory
        if (options.Stage.HasValue)
        {
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });
        }
        else
        {
            services.AddSingleton<IIgnitionSignalFactory>(innerFactory);
        }

        return services;
    }

    /// <summary>
    /// Registers a MariaDB readiness signal using a connection factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionFactory">Factory function that creates MariaDB/MySQL connections.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload enables advanced scenarios where connection creation needs custom logic
    /// or when integrating with existing connection pooling infrastructure.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate, but note
    /// that this overload requires the connection factory to be available at registration time
    /// and cannot properly support staged factory-based scenarios. Use the connection string
    /// factory overload instead for staged execution.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Using custom connection factory
    /// services.AddMariaDbReadiness(
    ///     () => new MySqlConnection("Server=localhost;Database=mydb;User=root;Password=pass"),
    ///     options =>
    ///     {
    ///         options.VerificationStrategy = MariaDbVerificationStrategy.ConnectionPool;
    ///         options.Timeout = TimeSpan.FromSeconds(10);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddMariaDbReadiness(
        this IServiceCollection services,
        Func<MySqlConnection> connectionFactory,
        Action<MariaDbReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory, nameof(connectionFactory));

        var options = new MariaDbReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need connection string factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddMariaDbReadiness(Func<MySqlConnection>) requires a connection string factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> connectionStringFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MariaDbReadinessSignal>>();
            return new MariaDbReadinessSignal(connectionFactory, options, logger);
        });

        return services;
    }
}
