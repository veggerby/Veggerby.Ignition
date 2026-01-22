using System;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Aws;

/// <summary>
/// Factory for creating AWS S3 readiness signals with configurable connection details.
/// </summary>
public sealed class S3ReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, IAmazonS3> _s3ClientFactory;
    private readonly S3ReadinessOptions _options;

    /// <summary>
    /// Creates a new AWS S3 readiness signal factory.
    /// </summary>
    /// <param name="s3ClientFactory">Factory that produces the S3 client using the service provider.</param>
    /// <param name="options">AWS S3 readiness options.</param>
    public S3ReadinessSignalFactory(
        Func<IServiceProvider, IAmazonS3> s3ClientFactory,
        S3ReadinessOptions options)
    {
        _s3ClientFactory = s3ClientFactory ?? throw new ArgumentNullException(nameof(s3ClientFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "s3-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var s3Client = _s3ClientFactory(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<S3ReadinessSignal>>();
        
        return new S3ReadinessSignal(s3Client, _options, logger);
    }
}
