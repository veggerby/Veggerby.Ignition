using System;

using Amazon;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Aws;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
    /// The signal name defaults to "s3-readiness". This overload creates an S3 client using
    /// default AWS credentials (environment variables, IAM role, AWS profile, etc.).
    /// For connection-only verification without bucket checks, use the overload without bucketName.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddS3Readiness("my-bucket", options =>
    /// {
    ///     options.Region = "us-east-1";
    ///     options.VerifyBucketAccess = true;
    ///     options.Timeout = TimeSpan.FromSeconds(10);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddS3Readiness(
        this IServiceCollection services,
        string bucketName,
        Action<S3ReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucketName, nameof(bucketName));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = new S3ReadinessOptions { BucketName = bucketName };
            configure?.Invoke(options);

            if (!string.IsNullOrWhiteSpace(options.Region))
            {
                return new AmazonS3Client(RegionEndpoint.GetBySystemName(options.Region));
            }

            return new AmazonS3Client();
        });

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new S3ReadinessOptions { BucketName = bucketName };
            configure?.Invoke(options);

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
    /// This overload expects an <see cref="IAmazonS3"/> client to be already registered
    /// in the DI container. Use this when you have custom client configuration or want
    /// to share a client instance across multiple components.
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
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new S3ReadinessOptions();
            configure?.Invoke(options);

            var client = sp.GetRequiredService<IAmazonS3>();
            var logger = sp.GetRequiredService<ILogger<S3ReadinessSignal>>();
            return new S3ReadinessSignal(client, options, logger);
        });

        return services;
    }
}
