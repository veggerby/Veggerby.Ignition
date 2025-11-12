using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Veggerby.Ignition;

using WebApi.Signals;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HTTP client for external dependency checks
builder.Services.AddHttpClient<ExternalDependencyCheckSignal>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Add("User-Agent", "WebApi-Sample/1.0");
});

// Configure Ignition with custom options
builder.Services.AddIgnition(options =>
{
    options.Policy = IgnitionPolicy.BestEffort; // Continue even if some signals fail
    options.ExecutionMode = IgnitionExecutionMode.Parallel; // Run signals in parallel
    options.GlobalTimeout = TimeSpan.FromSeconds(30); // Overall timeout
    options.CancelOnGlobalTimeout = false; // Don't force cancellation on global timeout
    options.CancelIndividualOnTimeout = true; // Cancel individual signals that timeout
    options.EnableTracing = true; // Enable Activity tracing
    options.MaxDegreeOfParallelism = 4; // Limit concurrent signals
}, healthCheckTags: new[] { "ready" });

// Register startup signals
builder.Services.AddTransient<IIgnitionSignal, DatabaseConnectionPoolSignal>();
builder.Services.AddTransient<IIgnitionSignal, ConfigurationValidationSignal>();
builder.Services.AddTransient<IIgnitionSignal, ExternalDependencyCheckSignal>();
builder.Services.AddTransient<IIgnitionSignal, BackgroundServicesSignal>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Map health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                duration = x.Value.Duration.TotalMilliseconds,
                description = x.Value.Description,
                data = x.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Perform startup initialization
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();

try
{
    logger.LogInformation("Starting application initialization...");

    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();

    var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
    if (overallSuccess)
    {
        logger.LogInformation("Application initialization completed successfully in {Duration}ms",
            result.TotalDuration.TotalMilliseconds);

        foreach (var signal in result.Results.Where(s => s.Status == IgnitionSignalStatus.Succeeded))
        {
            logger.LogInformation("✓ {SignalName} completed in {Duration}ms",
                signal.Name, signal.Duration.TotalMilliseconds);
        }
    }
    else
    {
        logger.LogWarning("Application initialization completed with issues in {Duration}ms",
            result.TotalDuration.TotalMilliseconds);

        foreach (var signal in result.Results)
        {
            var icon = signal.Status switch
            {
                IgnitionSignalStatus.Succeeded => "✓",
                IgnitionSignalStatus.Failed => "✗",
                IgnitionSignalStatus.TimedOut => "⏰",
                _ => "?"
            };

            logger.LogInformation("{Icon} {SignalName}: {Status} ({Duration}ms)",
                icon, signal.Name, signal.Status, signal.Duration.TotalMilliseconds);

            if (signal.Exception != null)
            {
                logger.LogWarning("  Error: {Error}", signal.Exception.Message);
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize application");
    throw; // This will prevent the application from starting
}

logger.LogInformation("Web API is ready to accept requests");
logger.LogInformation("Available endpoints:");
logger.LogInformation("  • GET /api/health/ready - Startup readiness status");
logger.LogInformation("  • GET /api/health/startup - Detailed startup information");
logger.LogInformation("  • GET /health - Health check status");
logger.LogInformation("  • GET /health/ready - Readiness health check");
logger.LogInformation("  • GET /api/weather/forecast - Sample weather forecast");
logger.LogInformation("  • GET /api/weather/current - Current weather");
if (app.Environment.IsDevelopment())
{
    logger.LogInformation("  • /swagger - API documentation");
}

app.Run();
