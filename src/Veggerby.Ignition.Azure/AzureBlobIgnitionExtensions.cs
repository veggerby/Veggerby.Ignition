using System;

using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Azure;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering Azure Blob Storage readiness signals with dependency injection.
/// </summary>
public static class AzureBlobIgnitionExtensions
{
    /// <summary>
    /// Registers an Azure Blob Storage readiness signal using a connection string.
    /// Creates a new <see cref="BlobServiceClient"/> internally.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionString">Azure Storage connection string.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "azure-blob-readiness". For connection-only verification,
    /// leave <see cref="AzureBlobReadinessOptions.ContainerName"/> null. To verify container existence,
    /// use the <paramref name="configure"/> delegate to specify the container name.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddAzureBlobReadiness(connectionString, options =>
    /// {
    ///     options.ContainerName = "config";
    ///     options.VerifyContainerExists = true;
    ///     options.CreateIfNotExists = false;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureBlobReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<AzureBlobReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        services.AddSingleton<BlobServiceClient>(sp =>
            new BlobServiceClient(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new AzureBlobReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<BlobServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureBlobReadinessSignal>>();
            return new AzureBlobReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an Azure Blob Storage readiness signal using an existing <see cref="BlobServiceClient"/>
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// This overload expects a <see cref="BlobServiceClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the BlobServiceClient
    /// services.AddSingleton(sp =>
    ///     new BlobServiceClient(connectionString));
    ///
    /// // Then register readiness signal
    /// services.AddAzureBlobReadiness(options =>
    /// {
    ///     options.ContainerName = "config";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureBlobReadiness(
        this IServiceCollection services,
        Action<AzureBlobReadinessOptions>? configure = null)
    {
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new AzureBlobReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<BlobServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureBlobReadinessSignal>>();
            return new AzureBlobReadinessSignal(client, options, logger);
        });

        return services;
    }
}
