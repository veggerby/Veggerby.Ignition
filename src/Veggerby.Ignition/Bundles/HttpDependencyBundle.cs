using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace Veggerby.Ignition.Bundles;

/// <summary>
/// Pre-built ignition bundle that registers signals for verifying HTTP endpoint readiness.
/// </summary>
/// <remarks>
/// This bundle creates signals that perform HTTP GET requests to specified endpoints,
/// succeeding when the endpoint returns a successful status code (2xx).
/// Useful for verifying that dependent HTTP services are reachable before startup completes.
/// </remarks>
public sealed class HttpDependencyBundle : IIgnitionBundle
{
    private readonly string[] _endpoints;
    private readonly TimeSpan? _defaultTimeout;

    /// <summary>
    /// Creates an HTTP dependency bundle for the specified endpoints.
    /// </summary>
    /// <param name="endpoints">Array of HTTP endpoint URLs to verify (e.g., "https://api.example.com/health").</param>
    /// <param name="defaultTimeout">Optional default timeout per endpoint check.</param>
    public HttpDependencyBundle(string[] endpoints, TimeSpan? defaultTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (endpoints.Length == 0)
        {
            throw new ArgumentException("At least one endpoint must be specified.", nameof(endpoints));
        }

        foreach (var endpoint in endpoints)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint, nameof(endpoints));
        }

        _endpoints = endpoints;
        _defaultTimeout = defaultTimeout;
    }

    /// <summary>
    /// Creates an HTTP dependency bundle for a single endpoint.
    /// </summary>
    /// <param name="endpoint">HTTP endpoint URL to verify.</param>
    /// <param name="defaultTimeout">Optional default timeout for the endpoint check.</param>
    public HttpDependencyBundle(string endpoint, TimeSpan? defaultTimeout = null)
        : this(new[] { endpoint }, defaultTimeout)
    {
    }

    /// <inheritdoc/>
    public string Name => "HttpDependency";

    /// <inheritdoc/>
    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions { DefaultTimeout = _defaultTimeout };
        configure?.Invoke(options);

        foreach (var endpoint in _endpoints)
        {
            var signal = new HttpEndpointSignal(endpoint, options.DefaultTimeout);
            services.AddIgnitionSignal(signal);
        }
    }

    private sealed class HttpEndpointSignal : IIgnitionSignal
    {
        private readonly string _endpoint;
        private static readonly HttpClient _sharedClient = new();

        public HttpEndpointSignal(string endpoint, TimeSpan? timeout)
        {
            _endpoint = endpoint;
            Timeout = timeout;
        }

        public string Name => $"http:{_endpoint}";
        public TimeSpan? Timeout { get; }

        public async Task WaitAsync(CancellationToken cancellationToken = default)
        {
            var response = await _sharedClient.GetAsync(_endpoint, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
    }
}
