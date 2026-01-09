using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Http;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for registering HTTP readiness signals with dependency injection.
/// </summary>
public static class HttpIgnitionExtensions
{
    /// <summary>
    /// Registers an HTTP readiness signal for the specified URL.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="url">Target URL to check for readiness.</param>
    /// <param name="configure">Optional configuration delegate for readiness options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The signal name defaults to "http-readiness". For basic connectivity checks,
    /// no additional configuration is required. To validate response content or customize
    /// expected status codes, use the <paramref name="configure"/> delegate.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddHttpReadiness("https://api.example.com/health", options =>
    /// {
    ///     options.ExpectedStatusCodes = [200, 204];
    ///     options.ValidateResponse = async (response) =>
    ///         (await response.Content.ReadAsStringAsync()).Contains("healthy");
    ///     options.Timeout = TimeSpan.FromSeconds(5);
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHttpReadiness(
        this IServiceCollection services,
        string url,
        Action<HttpReadinessOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url, nameof(url));

        services.AddHttpClient();

        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var options = new HttpReadinessOptions();
            configure?.Invoke(options);

            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var logger = sp.GetRequiredService<ILogger<HttpReadinessSignal>>();

            return new HttpReadinessSignal(httpClient, url, options, logger);
        });

        return services;
    }
}
