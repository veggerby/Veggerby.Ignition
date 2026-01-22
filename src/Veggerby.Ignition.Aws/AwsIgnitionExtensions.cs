using System;

using Amazon;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Aws;

/// <summary>
/// Extension methods for registering AWS S3 readiness signals with dependency injection.
/// </summary>
public static class AwsIgnitionExtensions
{
    /// <summary>
    /// Registers an AWS S3 readiness signal for a specific bucket.
    /// Creates a new <see cref="AmazonS3Client"/> internally using default credentials.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="bucketName">S3 bucket name to verify.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The signal name defaults to "s3-readiness". This overload creates an S3 client using
    /// default AWS credentials (environment variables, IAM role, AWS profile, etc.).
    /// For connection-only verification without bucket checks, use the overload without bucketName.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddS3Readiness("my-bucket", options =>
    /// {
    ///     options.Region = "us-east-1";
    ///     options.VerifyBucketAccess = true;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// 
    /// // Staged execution
    /// services.AddS3Readiness("test-bucket", options =>
    /// {
    ///     options.Stage = 1;
    ///     options.Region = "us-west-2";
    ///     options.Timeout = TimeSpan.FromSeconds(30);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddS3Readiness(
        this IServiceCollection services,
        string bucketName,
        Action<S3ReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName, nameof(bucketName));

        var options = new S3ReadinessOptions { BucketName = bucketName };
        configure?.Invoke(options);

        // If Stage is specified, use factory-based registration
        if (options.Stage.HasValue)
        {
            var innerFactory = new S3ReadinessSignalFactory(sp =>
            {
                if (!string.IsNullOrWhiteSpace(options.Region))
                {
                    return new AmazonS3Client(RegionEndpoint.GetBySystemName(options.Region));
                }
                return new AmazonS3Client();
            }, options);
            var stagedFactory = new StagedIgnitionSignalFactory(innerFactory, options.Stage.Value);
            services.AddSingleton<IIgnitionSignalFactory>(stagedFactory);

            // Configure the stage's execution mode
            services.Configure<IgnitionStageConfiguration>(config =>
            {
                config.EnsureStage(options.Stage.Value, IgnitionExecutionMode.Parallel);
            });

            return services;
        }

        services.AddSingleton<IAmazonS3>(sp =>
        {
            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                return new AmazonS3Client(RegionEndpoint.GetBySystemName(options.Region));
            }

            return new AmazonS3Client();
        });

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonS3>();
            var logger = sp.GetRequiredService<ILogger<S3ReadinessSignal>>();
            return new S3ReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an AWS S3 readiness signal using an existing <see cref="IAmazonS3"/> client
    /// from the DI container.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This overload expects an <see cref="IAmazonS3"/> client to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> This overload does not support staged execution. For staged execution
    /// scenarios (e.g., with Testcontainers), use the overload that accepts a client factory.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // First register the S3 client
    /// services.AddSingleton&lt;IAmazonS3&gt;(sp =>
    ///     new AmazonS3Client(RegionEndpoint.USEast1));
    ///
    /// // Then register readiness signal
    /// services.AddS3Readiness(options =>
    /// {
    ///     options.BucketName = "my-bucket";
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddS3Readiness(
        this IServiceCollection services,
        Action<S3ReadinessOptions>? configure = null)
    {
        var options = new S3ReadinessOptions();
        configure?.Invoke(options);

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var client = sp.GetRequiredService<IAmazonS3>();
            var logger = sp.GetRequiredService<ILogger<S3ReadinessSignal>>();
            return new S3ReadinessSignal(client, options, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers an AWS S3 readiness signal using a client factory.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="clientFactory">Factory that produces the S3 client using the service provider.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// This method enables proper dependency injection for AWS S3 readiness signals in staged execution.
    /// The client factory is invoked when the signal is created (when its stage is reached),
    /// allowing it to access resources that were created or modified by earlier stages.
    /// </para>
    /// <para>
    /// This is particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes S3 clients or configurations available for Stage 1+ to consume.
    /// </para>
    /// <para>
    /// For staged execution, set <c>options.Stage</c> in the configuration delegate.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Stage 0: Start container and store configuration
    /// var infrastructure = new InfrastructureManager();
    /// services.AddSingleton(infrastructure);
    /// services.AddIgnitionFromTaskWithStage("localstack-container",
    ///     async ct => await infrastructure.StartLocalStackAsync(), stage: 0);
    /// 
    /// // Stage 1: Use S3 client from infrastructure
    /// services.AddS3Readiness(
    ///     sp => sp.GetRequiredService&lt;InfrastructureManager&gt;().CreateS3Client(),
    ///     options =>
    ///     {
    ///         options.Stage = 1;
    ///         options.BucketName = "test-bucket";
    ///         options.Timeout = TimeSpan.FromSeconds(30);
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddS3Readiness(
        this IServiceCollection services,
        Func<IServiceProvider, IAmazonS3> clientFactory,
        Action<S3ReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory, nameof(clientFactory));

        var options = new S3ReadinessOptions();
        configure?.Invoke(options);

        var innerFactory = new S3ReadinessSignalFactory(clientFactory, options);

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
