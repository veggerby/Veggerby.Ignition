using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Http;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying HTTP endpoint readiness.
/// Validates HTTP connectivity and optionally response content.
/// </summary>
internal sealed class HttpReadinessSignal : IIgnitionSignal
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly HttpReadinessOptions _options;
    private readonly ILogger<HttpReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpReadinessSignal"/> class.
    /// </summary>
    /// <param name="httpClient">HttpClient instance for making requests.</param>
    /// <param name="url">Target URL to check for readiness.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public HttpReadinessSignal(
        HttpClient httpClient,
        string url,
        HttpReadinessOptions options,
        ILogger<HttpReadinessSignal> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));
        _url = url;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "http-readiness";

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

        activity?.SetTag("http.url", _url);
        activity?.SetTag("http.expected_status_codes", string.Join(",", _options.ExpectedStatusCodes));

        _logger.LogInformation("HTTP readiness check starting for {Url}", _url);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _url);

            if (_options.CustomHeaders is not null)
            {
                foreach (var header in _options.CustomHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var statusCode = (int)response.StatusCode;
            activity?.SetTag("http.status_code", statusCode);

            _logger.LogDebug("HTTP response received with status code {StatusCode}", statusCode);

            if (!_options.ExpectedStatusCodes.Contains(statusCode))
            {
                var message = $"HTTP endpoint returned unexpected status code {statusCode}. Expected: {string.Join(", ", _options.ExpectedStatusCodes)}";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            if (_options.ValidateResponse is not null)
            {
                activity?.SetTag("http.custom_validation", "true");
                _logger.LogDebug("Executing custom response validation");

                var isValid = await _options.ValidateResponse(response);

                if (!isValid)
                {
                    var message = "HTTP response validation failed";
                    _logger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                _logger.LogDebug("Custom response validation succeeded");
            }

            _logger.LogInformation("HTTP readiness check completed successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "HTTP readiness check failed");
            throw;
        }
    }
}
