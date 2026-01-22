using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Grpc;

/// <summary>
/// Ignition signal for verifying gRPC service readiness via health check protocol.
/// </summary>
internal sealed class GrpcReadinessSignal : IIgnitionSignal, IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly bool _ownsChannel;
    private readonly string _serviceUrl;
    private readonly GrpcReadinessOptions _options;
    private readonly ILogger<GrpcReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcReadinessSignal"/> class.
    /// </summary>
    /// <param name="channel">gRPC channel for making health check requests.</param>
    /// <param name="serviceUrl">Target service URL for diagnostics.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="ownsChannel">Whether this signal owns the channel and should dispose it.</param>
    public GrpcReadinessSignal(
        GrpcChannel channel,
        string serviceUrl,
        GrpcReadinessOptions options,
        ILogger<GrpcReadinessSignal> logger,
        bool ownsChannel = false)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceUrl, nameof(serviceUrl));
        _serviceUrl = serviceUrl;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ownsChannel = ownsChannel;
    }

    /// <inheritdoc/>
    public string Name => "grpc-readiness";

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

        activity?.SetTag("grpc.service_url", _serviceUrl);
        activity?.SetTag("grpc.service_name", _options.ServiceName ?? "(server)");
        activity?.SetTag("grpc.channel_state", _channel.State.ToString());

        _logger.LogInformation(
            "gRPC readiness check starting for {ServiceUrl} (service: {ServiceName})",
            _serviceUrl,
            _options.ServiceName ?? "(overall server)");

        try
        {
            var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

            await retryPolicy.ExecuteAsync(async ct =>
            {
                var client = new Health.HealthClient(_channel);

                var request = new HealthCheckRequest
                {
                    Service = _options.ServiceName ?? string.Empty
                };

                var response = await client.CheckAsync(request, cancellationToken: ct).ConfigureAwait(false);

                activity?.SetTag("grpc.health_status", response.Status.ToString());

                if (response.Status != HealthCheckResponse.Types.ServingStatus.Serving)
                {
                    var message = $"gRPC service health check returned non-serving status: {response.Status}";
                    _logger.LogError(message);
                    throw new InvalidOperationException(message);
                }
            }, "gRPC health check", cancellationToken, _options.Timeout);

            _logger.LogInformation("gRPC readiness check completed successfully");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            _logger.LogError("gRPC health check protocol not implemented by server");
            throw new InvalidOperationException("gRPC server does not implement health check protocol", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "gRPC readiness check failed");
            throw;
        }
    }

    /// <summary>
    /// Disposes the gRPC channel if owned by this signal.
    /// </summary>
    public void Dispose()
    {
        if (_ownsChannel)
        {
            _channel?.Dispose();
        }
    }
}
