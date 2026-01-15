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
    /// <para>
    /// The signal name defaults to "azure-queue-readiness". For connection-only verification,
    /// leave <see cref="AzureQueueReadinessOptions.QueueName"/> null. To verify queue existence,
    /// use the <paramref name="configure"/> delegate to specify the queue name.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
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
    /// 
    /// // Staged execution
    /// services.AddAzureQueueReadiness("UseDevelopmentStorage=true", options =>
    /// {
    ///     options.Stage = 1;
    ///     options.QueueName = "tasks";
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureQueueReadiness(
        this IServiceCollection services,
        string connectionString,
        Action<AzureQueueReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        var options = new AzureQueueReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new AzureQueueReadinessSignalFactory(_ => connectionString, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        services.AddSingleton<QueueServiceClient>(sp =>
            new QueueServiceClient(connectionString));

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
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
    /// <para>
    /// This overload expects a <see cref="QueueServiceClient"/> to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate, but note
    /// that this overload requires an existing <see cref="QueueServiceClient"/> in DI and cannot
    /// properly support staged factory-based scenarios. Use the connection string factory overload instead.
    /// </para>
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
        var options = new AzureQueueReadinessOptions();
        configure?.Invoke(options);

        // If Stage is specified, this method cannot be used (need connection string factory for proper DI)
        if (options.Stage.HasValue)
        {
            throw new InvalidOperationException(
                "Staged execution with AddAzureQueueReadiness() requires a connection string factory. " +
                "Use the overload that accepts Func<IServiceProvider, string> connectionStringFactory parameter.");
        }

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var client = sp.GetRequiredService<QueueServiceClient>();
            var logger = sp.GetRequiredService<ILogger<AzureQueueReadinessSignal>>();
            return new AzureQueueReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an Azure Queue Storage readiness signal using a connection string factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="connectionStringFactory">Factory that produces the Azure Storage connection string using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for Azure Queue Storage readiness signals in staged execution.
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
    /// services.AddAzureQueueReadiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().AzureQueueConnectionString,
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.QueueName = "tasks";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddAzureQueueReadiness(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        Action<AzureQueueReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(connectionStringFactory, nameof(connectionStringFactory));

        var options = new AzureQueueReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new AzureQueueReadinessSignalFactory(connectionStringFactory, options);

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
