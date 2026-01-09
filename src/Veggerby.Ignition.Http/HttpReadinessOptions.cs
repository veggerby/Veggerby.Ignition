using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Http;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Configuration options for HTTP endpoint readiness verification.
/// </summary>
public sealed class HttpReadinessOptions
{
    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Expected HTTP status codes indicating readiness. Default is 200 (OK).
    /// </summary>
    /// <remarks>
    /// Multiple codes can be specified for endpoints that may return various success codes (e.g., 200, 204).
    /// </remarks>
    public int[] ExpectedStatusCodes { get; set; } = [200];

    /// <summary>
    /// Optional custom headers to include in the HTTP request.
    /// </summary>
    /// <remarks>
    /// Use for Authorization headers, User-Agent customization, or API keys.
    /// </remarks>
    public Dictionary<string, string>? CustomHeaders { get; set; }

    /// <summary>
    /// Optional response validation function. Receives the <see cref="HttpResponseMessage"/> and should return
    /// <c>true</c> if the response is valid, <c>false</c> otherwise.
    /// </summary>
    /// <remarks>
    /// Useful for validating response body content (JSON schema, specific text, etc.).
    /// Validation is only performed when the status code is one of the <see cref="ExpectedStatusCodes"/>.
    /// </remarks>
    public Func<HttpResponseMessage, Task<bool>>? ValidateResponse { get; set; }
}
