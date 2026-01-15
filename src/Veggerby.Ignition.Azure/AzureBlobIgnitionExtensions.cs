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
    /// <para>
    /// The signal name defaults to "azure-blob-readiness". For connection-only verification,
    /// leave <see cref="AzureBlobReadinessOptions.ContainerName"/> null. To verify container existence,
    /// use the <paramref name="configure"/> delegate to specify the container name.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
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
    /// 
    /// // Staged execution
    /// services.AddAzureBlobReadiness("UseDevelopmentStorage=true", options =>
    /// {
    ///     options.Stage = 1;
    ///     options.ContainerName = "data";
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureBlobReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<AzureBlobReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var options = new AzureBlobReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new AzureBlobReadinessSignalFactory(_ => connectionString, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        services.AddSingleton<BlobServiceClient>(sp =>
            new BlobServiceClient(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
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
    /// <para>
    /// This overload expects a <see cref="BlobServiceClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate, but note
    /// that this overload requires an existing <see cref="BlobServiceClient"/> in DI and cannot
    /// properly support staged factory-based scenarios. Use the connection string factory overload instead.
    /// </para>
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
        var options = new AzureBlobReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need connection string factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddAzureBlobReadiness() requires a connection string factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> connectionStringFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var client = sp.GetRequiredService<BlobServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureBlobReadinessSignal>>();
            return new AzureBlobReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an Azure Blob Storage readiness signal using a connection string factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the Azure Storage connection string using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Azure Blob Storage readiness signals in staged execution.
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
    /// services.AddIgnitionFromTaskWithStage("azurite-container",
    ///     async ct => await infrastructure.StartAzuriteAsync(), stage: 0);
    /// 
    /// // Stage 1: Use connection string from infrastructure
    /// services.AddAzureBlobReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().AzureBlobConnectionString,
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.ContainerName = "data";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureBlobReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        Action<AzureBlobReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory, nameof(connectionStringFactory));

        var options = new AzureBlobReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new AzureBlobReadinessSignalFactory(connectionStringFactory, options);

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
