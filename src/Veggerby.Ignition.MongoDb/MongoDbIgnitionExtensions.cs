using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.MongoDb;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering MongoDB readiness signals with dependency injection.
/// </summary>
public static class MongoDbIgnitionExtensions
{
    /// <summary>
    /// Registers a MongoDB readiness signal using a connection string.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">MongoDB connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "mongodb-readiness". For cluster connectivity verification only,
    /// no additional configuration is required. To verify a specific collection, use the
    /// <paramref name="configure"/> delegate to specify database and collection names.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple usage
    /// services.AddMongoDbReadiness("mongodb://localhost:27017", options =>
    /// {
    ///     options.DatabaseName = "mydb";
    ///     options.VerifyCollection = "users";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// 
    /// // Staged execution
    /// services.AddMongoDbReadiness("mongodb://localhost:27017", options =>
    /// {
    ///     options.Stage = 1;
    ///     options.DatabaseName = "testdb";
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMongoDbReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<MongoDbReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var options = new MongoDbReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new MongoDbReadinessSignalFactory(_ => connectionString, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        var client = new MongoClient(connectionString);

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MongoDbReadinessSignal>>();
            return new MongoDbReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a MongoDB readiness signal using an existing MongoDB client.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when you have an existing <see cref="IMongoClient"/> registered in DI.
    /// The signal will resolve the client from the service provider.
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate, but note
    /// that this overload requires an existing client in DI and cannot properly support
    /// staged factory-based scenarios. Use the connection string factory overload instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddSingleton&lt;IMongoClient&gt;(sp =&gt; new MongoClient("mongodb://localhost:27017"));
    /// 
    /// services.AddMongoDbReadiness(options =>
    /// {
    ///     options.DatabaseName = "mydb";
    ///     options.VerifyCollection = "users";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMongoDbReadiness(
        this IServiceCollection services,
        Action<MongoDbReadinessOptions>? configure = null)
    {
        var options = new MongoDbReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need connection string factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddMongoDbReadiness() requires a connection string factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> connectionStringFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MongoDbReadinessSignal>>();
            // Use factory pattern to defer client resolution until signal executes
            return new MongoDbReadinessSignal(
                () => sp.GetRequiredService<IMongoClient>(),
                options,
                logger);
        });

        return services;
    }

    /// <summary>
    /// Registers a MongoDB readiness signal using a connection string factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the MongoDB connection string using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for MongoDB readiness signals in staged execution.
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
    /// services.AddIgnitionFromTaskWithStage("mongodb-container",
    ///     async ct => await infrastructure.StartMongoDbAsync(), stage: 0);
    /// 
    /// // Stage 1: Use connection string from infrastructure
    /// services.AddMongoDbReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().MongoDbConnectionString,
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.DatabaseName = "testdb";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddMongoDbReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        Action<MongoDbReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory, nameof(connectionStringFactory));

        var options = new MongoDbReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new MongoDbReadinessSignalFactory(connectionStringFactory, options);

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
}
