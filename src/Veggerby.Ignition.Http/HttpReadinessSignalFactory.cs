using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Http;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating HTTP readiness signals with configurable URLs.
/// </summary>
public sealed class HttpReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _urlFactory;
    private readonly HttpReadinessOptions _options;

    /// <summary>
    /// Creates a new HTTP readiness signal factory.
    /// </summary>
    /// <param name="urlFactory">Factory that produces the URL using the service provider.</param>
    /// <param name="options">HTTP readiness options.</param>
    public HttpReadinessSignalFactory(
        Func<IServiceProvider, string> urlFactory,
        HttpReadinessOptions options)
    {
        _urlFactory = urlFactory ?? throw new ArgumentNullException(nameof(urlFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "http-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var url = _urlFactory(serviceProvider);
        var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
        var logger = serviceProvider.GetRequiredService<ILogger<HttpReadinessSignal>>();
        
        return new HttpReadinessSignal(httpClient, url, _options, logger);
    }
}
