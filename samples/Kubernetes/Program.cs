using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Kubernetes;

/// <summary>
/// Simulates a PostgreSQL database connection signal.
/// </summary>
public class PostgresConnectionSignal : IIgnitionSignal
{
    private readonly ILogger<PostgresConnectionSignal> _logger;

    public string Name => "postgres-connection";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public PostgresConnectionSignal(ILogger<PostgresConnectionSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to PostgreSQL database...");
        await Task.Delay(1200, cancellationToken);
        _logger.LogInformation("PostgreSQL connection established");
    }
}

/// <summary>
/// Simulates a Redis cache connection signal.
/// </summary>
public class RedisConnectionSignal : IIgnitionSignal
{
    private readonly ILogger<RedisConnectionSignal> _logger;

    public string Name => "redis-connection";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(8);

    public RedisConnectionSignal(ILogger<RedisConnectionSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to Redis cache...");
        await Task.Delay(800, cancellationToken);
        _logger.LogInformation("Redis connection established");
    }
}

/// <summary>
/// Simulates loading Kubernetes ConfigMaps and Secrets.
/// </summary>
public class KubernetesConfigSignal : IIgnitionSignal
{
    private readonly ILogger<KubernetesConfigSignal> _logger;

    public string Name => "kubernetes-config";

    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public KubernetesConfigSignal(ILogger<KubernetesConfigSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading Kubernetes ConfigMaps and Secrets...");
        await Task.Delay(600, cancellationToken);
        _logger.LogInformation("Kubernetes configuration loaded successfully");
    }
}

/// <summary>
/// Demonstrates Kubernetes deployment with health check integration.
/// Shows liveness, readiness, and startup probe patterns.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddIgnition(options =>
        {
            options.Policy = IgnitionPolicy.BestEffort;
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
            options.GlobalTimeout = TimeSpan.FromSeconds(30);
            options.CancelOnGlobalTimeout = false;
            options.CancelIndividualOnTimeout = true;
            options.EnableTracing = true;
            options.MaxDegreeOfParallelism = 4;
        }, healthCheckTags: new[] { "ready", "startup" });

        builder.Services.AddIgnitionSignal<PostgresConnectionSignal>();
        builder.Services.AddIgnitionSignal<RedisConnectionSignal>();
        builder.Services.AddIgnitionSignal<KubernetesConfigSignal>();

        var app = builder.Build();

        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthCheckResponse
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthCheckResponse
        });

        app.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("startup"),
            ResponseWriter = WriteHealthCheckResponse
        });

        app.MapGet("/", () => new
        {
            service = "Veggerby.Ignition Kubernetes Sample",
            version = "1.0.0",
            endpoints = new[]
            {
                "/health/live",
                "/health/ready",
                "/health/startup",
                "/api/status"
            }
        });

        app.MapGet("/api/status", async (IIgnitionCoordinator coordinator) =>
        {
            var result = await coordinator.GetResultAsync();
            return Results.Ok(new
            {
                initialized = true,
                totalDuration = result.TotalDuration.TotalMilliseconds,
                timedOut = result.TimedOut,
                signals = result.Results.Select(r => new
                {
                    name = r.Name,
                    status = r.Status.ToString(),
                    duration = r.Duration.TotalMilliseconds,
                    error = r.Exception?.Message
                })
            });
        });

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();

        try
        {
            logger.LogInformation("Starting Kubernetes sample initialization...");

            await coordinator.WaitAllAsync();
            var result = await coordinator.GetResultAsync();

            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            if (overallSuccess)
            {
                logger.LogInformation("Initialization completed successfully in {Duration}ms",
                    result.TotalDuration.TotalMilliseconds);

                foreach (var signal in result.Results)
                {
                    logger.LogInformation("✓ {SignalName} completed in {Duration}ms",
                        signal.Name, signal.Duration.TotalMilliseconds);
                }
            }
            else
            {
                logger.LogWarning("Initialization completed with issues in {Duration}ms",
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

                    if (signal.Exception is not null)
                    {
                        logger.LogWarning("  Error: {Error}", signal.Exception.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize application");
            throw;
        }

        logger.LogInformation("Kubernetes sample is ready to accept requests");
        logger.LogInformation("Health check endpoints:");
        logger.LogInformation("  • GET /health/live - Liveness probe (always healthy)");
        logger.LogInformation("  • GET /health/ready - Readiness probe (checks ignition)");
        logger.LogInformation("  • GET /health/startup - Startup probe (checks ignition)");
        logger.LogInformation("  • GET /api/status - Detailed startup status");

        app.Run();
    }

    private static async Task WriteHealthCheckResponse(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
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
}
