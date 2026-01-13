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
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddMongoDbReadiness("mongodb://localhost:27017", options =>
    /// {
    ///     options.DatabaseName = "mydb";
    ///     options.VerifyCollection = "users";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMongoDbReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<MongoDbReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var client = new MongoClient(connectionString);

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new MongoDbReadinessOptions();
            configure?.Invoke(options);

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
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new MongoDbReadinessOptions();
            configure?.Invoke(options);

            var logger = sp.GetRequiredService<ILogger<MongoDbReadinessSignal>>();
            // Use factory pattern to defer client resolution until signal executes
            return new MongoDbReadinessSignal(
                () => sp.GetRequiredService<IMongoClient>(),
                options,
                logger);
        });

        return services;
    }
}
