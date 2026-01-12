using System;

using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Azure Queue Storage readiness signals with dependency injection.
/// </summary>
public static class AzureQueueIgnitionExtensions
{
    /// <summary>
    /// Registers an Azure Queue Storage readiness signal using a connection string.
    /// Creates a new <see cref="QueueServiceClient"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">Azure Storage connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "azure-queue-readiness". For connection-only verification,
    /// leave <see cref="AzureQueueReadinessOptions.QueueName"/> null. To verify queue existence,
    /// use the <paramref name="configure"/> delegate to specify the queue name.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddAzureQueueReadiness(connectionString, options =>
    /// {
    ///     options.QueueName = "messages";
    ///     options.VerifyQueueExists = true;
    ///     options.CreateIfNotExists = false;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureQueueReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<AzureQueueReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        services.AddSingleton<QueueServiceClient>(sp =>
            new QueueServiceClient(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new AzureQueueReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<QueueServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureQueueReadinessSignal>>();
            return new AzureQueueReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an Azure Queue Storage readiness signal using an existing <see cref="QueueServiceClient"/>
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload expects a <see cref="QueueServiceClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the QueueServiceClient
    /// services.AddSingleton(sp =>
    ///     new QueueServiceClient(connectionString));
    ///
    /// // Then register readiness signal
    /// services.AddAzureQueueReadiness(options =>
    /// {
    ///     options.QueueName = "messages";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureQueueReadiness(
        this IServiceCollection services,
        Action<AzureQueueReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new AzureQueueReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<QueueServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureQueueReadinessSignal>>();
            return new AzureQueueReadinessSignal(client, options, logger);
        });

        return services;
    }
}
