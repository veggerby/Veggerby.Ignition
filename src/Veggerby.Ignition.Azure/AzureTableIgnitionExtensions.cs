using System;

using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Azure Table Storage readiness signals with dependency injection.
/// </summary>
public static class AzureTableIgnitionExtensions
{
    /// <summary>
    /// Registers an Azure Table Storage readiness signal using a connection string.
    /// Creates a new <see cref="TableServiceClient"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">Azure Storage connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "azure-table-readiness". For connection-only verification,
    /// leave <see cref="AzureTableReadinessOptions.TableName"/> null. To verify table existence,
    /// use the <paramref name="configure"/> delegate to specify the table name.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddAzureTableReadiness(connectionString, options =>
    /// {
    ///     options.TableName = "entities";
    ///     options.VerifyTableExists = true;
    ///     options.CreateIfNotExists = false;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureTableReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<AzureTableReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        services.AddSingleton<TableServiceClient>(sp =>
            new TableServiceClient(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new AzureTableReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<TableServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureTableReadinessSignal>>();
            return new AzureTableReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an Azure Table Storage readiness signal using an existing <see cref="TableServiceClient"/>
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload expects a <see cref="TableServiceClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the TableServiceClient
    /// services.AddSingleton(sp =>
    ///     new TableServiceClient(connectionString));
    ///
    /// // Then register readiness signal
    /// services.AddAzureTableReadiness(options =>
    /// {
    ///     options.TableName = "entities";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureTableReadiness(
        this IServiceCollection services,
        Action<AzureTableReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new AzureTableReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<TableServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureTableReadinessSignal>>();
            return new AzureTableReadinessSignal(client, options, logger);
        });

        return services;
    }
}
