# Veggerby.Ignition.Http

HTTP readiness signals for Veggerby.Ignition - verify HTTP endpoints and external services during application startup.

## Installation

```bash
dotnet add package Veggerby.Ignition.Http
```

## Usage

### Basic HTTP Endpoint Verification

```csharp
builder.Services.AddIgnition();

builder.Services.AddHttpReadiness("https://api.example.com/health");

var app = builder.Build();
await app.Services.GetRequiredService<IIgnitionCoordinator>().WaitAllAsync();
```

### With Custom Status Codes

```csharp
builder.Services.AddHttpReadiness(
    "https://api.example.com/health",
    options =>
    {
        options.ExpectedStatusCodes = [200, 204];
        options.Timeout = TimeSpan.FromSeconds(5);
    });
```

### With Response Validation

```csharp
builder.Services.AddHttpReadiness(
    "https://api.example.com/health",
    options =>
    {
        options.ExpectedStatusCodes = [200];
        options.ValidateResponse = async (response) =>
        {
            var content = await response.Content.ReadAsStringAsync();
            return content.Contains("healthy");
        };
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

### With Custom Headers

```csharp
builder.Services.AddHttpReadiness(
    "https://api.example.com/health",
    options =>
    {
        options.CustomHeaders = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token123",
            ["User-Agent"] = "MyApp/1.0"
        };
    });
```

### JSON Response Validation

```csharp
builder.Services.AddHttpReadiness(
    "https://api.example.com/health",
    options =>
    {
        options.ValidateResponse = async (response) =>
        {
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("status").GetString() == "healthy";
        };
    });
```

## Features

- **HTTP Endpoint Verification**: Validates connectivity to external HTTP services
- **Flexible Status Code Matching**: Support for multiple expected status codes (200, 204, etc.)
- **Custom Response Validation**: Validate response body content (JSON, text, etc.)
- **Custom Headers**: Add Authorization, User-Agent, or other headers
- **Activity Tracing**: Tags for URL, status codes, and validation results
- **Structured Logging**: Information, Debug, and Error level diagnostics
- **HttpClient Reuse**: Efficient connection reuse via IHttpClientFactory
- **Idempotent Execution**: Cached results prevent redundant requests
- **Thread-Safe**: Concurrent readiness checks execute once

## Configuration Options

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Timeout` | `TimeSpan?` | Per-signal timeout override | `null` (uses global timeout) |
| `ExpectedStatusCodes` | `int[]` | HTTP status codes indicating success | `[200]` |
| `CustomHeaders` | `Dictionary<string, string>?` | Headers to include in requests | `null` |
| `ValidateResponse` | `Func<HttpResponseMessage, Task<bool>>?` | Custom response validation | `null` |

## Logging

The signal emits structured logs at different levels:

- **Information**: Request start and successful completions
- **Debug**: Response received, validation execution
- **Error**: Status code mismatches, validation failures, connection errors

## Activity Tracing

When tracing is enabled, the signal adds these tags:

- `http.url`: Target URL
- `http.expected_status_codes`: Comma-separated expected status codes
- `http.status_code`: Actual response status code
- `http.custom_validation`: "true" if custom validation is configured

## Examples

### Health Check Integration

```csharp
builder.Services.AddIgnition();
builder.Services.AddHttpReadiness("https://api.example.com/health");

builder.Services
    .AddHealthChecks()
    .AddCheck<IgnitionHealthCheck>("ignition-readiness");
```

### Multiple External Services

```csharp
// Primary API
builder.Services.AddHttpReadiness(
    "https://api.example.com/health",
    options =>
    {
        options.Timeout = TimeSpan.FromSeconds(5);
    });

// Payment Gateway
builder.Services.AddHttpReadiness(
    "https://payment.example.com/status",
    options =>
    {
        options.ExpectedStatusCodes = [200, 204];
        options.Timeout = TimeSpan.FromSeconds(10);
    });
```

Note: Currently, multiple HTTP signals will share the same name ("http-readiness"). For distinct signals, implement custom `IIgnitionSignal` with unique names.

### Retry-After Header Handling

The signal does not automatically respect Retry-After headers. For retry logic, use Polly or similar libraries with the coordinator's timeout policies:

```csharp
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort;
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});
```

## Error Handling

Connection failures and validation errors are logged and propagated:

```csharp
try
{
    await coordinator.WaitAllAsync();
}
catch (AggregateException ex)
{
    foreach (var inner in ex.InnerExceptions)
    {
        if (inner is HttpRequestException httpEx)
        {
            // Handle HTTP-specific errors
            Console.WriteLine($"HTTP Error: {httpEx.Message}");
        }
        else if (inner is InvalidOperationException validationEx)
        {
            // Handle validation failures
            Console.WriteLine($"Validation Error: {validationEx.Message}");
        }
    }
}
```

## Performance

- Efficient HttpClient reuse via IHttpClientFactory
- Minimal allocations per signal invocation
- Async throughout (no blocking I/O)
- Idempotent execution (request made once)

## License

MIT License. See [LICENSE](../../LICENSE).
