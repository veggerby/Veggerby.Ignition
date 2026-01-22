using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Veggerby.Ignition.Http;

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

    /// <summary>
    /// Maximum number of retry attempts for transient connection failures.
    /// Default is 3 attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retry attempts.
    /// Subsequent delays use exponential backoff (doubled each retry).
    /// Default is 100 milliseconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Optional stage/phase number for staged execution.
    /// If <c>null</c>, the signal belongs to stage 0 (default/unstaged).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stages enable sequential execution across logical phases (e.g., infrastructure → services → workers).
    /// All signals in stage N complete before stage N+1 begins.
    /// </para>
    /// <para>
    /// Particularly useful with Testcontainers scenarios where Stage 0 starts containers
    /// and makes connection strings available for Stage 1+ to consume.
    /// </para>
    /// </remarks>
    public int? Stage { get; set; }
}
