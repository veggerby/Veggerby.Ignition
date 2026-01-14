using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Aws;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying AWS S3 bucket readiness.
/// Validates connection and optionally verifies bucket existence and access permissions.
/// </summary>
public sealed class S3ReadinessSignal : IIgnitionSignal
{
    private readonly IAmazonS3 _s3Client;
    private readonly S3ReadinessOptions _options;
    private readonly ILogger<S3ReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3ReadinessSignal"/> class
    /// using an existing <see cref="IAmazonS3"/> client.
    /// </summary>
    /// <param name="s3Client">AWS S3 client.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public S3ReadinessSignal(
        IAmazonS3 s3Client,
        S3ReadinessOptions options,
        ILogger<S3ReadinessSignal> logger)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "s3-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTask is null)
        {
            lock (_sync)
            {
                _cachedTask ??= ExecuteAsync(cancellationToken);
            }
        }

        return cancellationToken.CanBeCanceled && !_cachedTask.IsCompleted
            ? _cachedTask.WaitAsync(cancellationToken)
            : _cachedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var activity = Activity.Current;

        activity?.SetTag("aws.service", "s3");
        activity?.SetTag("aws.s3.bucket", _options.BucketName);
        activity?.SetTag("aws.s3.region", _options.Region);
        activity?.SetTag("aws.s3.verify_access", _options.VerifyBucketAccess);

        _logger.LogInformation(
            "AWS S3 readiness check starting for bucket {BucketName} in region {Region}",
            _options.BucketName ?? "(none)",
            _options.Region ?? "(default)");

        try
        {
            if (_options.VerifyBucketAccess && !string.IsNullOrWhiteSpace(_options.BucketName))
            {
                await VerifyBucketAccessAsync(cancellationToken);
            }
            else
            {
                _logger.LogDebug("Verifying AWS S3 service connection");
                await _s3Client.ListBucketsAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("AWS S3 readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AWS S3 readiness check failed");
            throw;
        }
    }

    private async Task VerifyBucketAccessAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Verifying AWS S3 bucket access: {BucketName}", _options.BucketName);

        try
        {
            // Use GetBucketLocation as a lightweight check for bucket existence and access
            var response = await _s3Client.GetBucketLocationAsync(
                new GetBucketLocationRequest
                {
                    BucketName = _options.BucketName
                },
                cancellationToken);

            if (response.HttpStatusCode == HttpStatusCode.OK)
            {
                _logger.LogDebug(
                    "AWS S3 bucket access verified: {BucketName} (location: {Location})",
                    _options.BucketName,
                    response.Location?.Value ?? "us-east-1");
            }
            else
            {
                throw new InvalidOperationException(
                    $"AWS S3 bucket access check returned unexpected status: {response.HttpStatusCode}");
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"AWS S3 bucket '{_options.BucketName}' does not exist or is not accessible",
                ex);
        }
    }
}
