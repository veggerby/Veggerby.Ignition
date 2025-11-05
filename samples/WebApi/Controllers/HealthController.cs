using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Veggerby.Ignition;

namespace WebApi.Controllers;

/// <summary>
/// Health controller that provides startup readiness and health information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IIgnitionCoordinator _ignitionCoordinator;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        IIgnitionCoordinator ignitionCoordinator,
        ILogger<HealthController> logger)
    {
        _ignitionCoordinator = ignitionCoordinator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current startup readiness status.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness()
    {
        try
        {
            await _ignitionCoordinator.WaitAllAsync();
            var result = await _ignitionCoordinator.GetResultAsync();
            
            var response = new
            {
                ready = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded),
                duration = result.TotalDuration.TotalMilliseconds,
                globalTimedOut = result.TimedOut,
                signals = result.Results.Select(s => new
                {
                    name = s.Name,
                    status = s.Status.ToString(),
                    duration = s.Duration.TotalMilliseconds,
                    error = s.Exception?.Message
                }).ToArray(),
                timestamp = DateTimeOffset.UtcNow
            };

            var overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded);
            return overallSuccess 
                ? Ok(response) 
                : StatusCode(503, response); // Service Unavailable
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check readiness status");
            
            return StatusCode(500, new
            {
                ready = false,
                error = "Failed to check readiness status",
                timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Gets detailed startup information including individual signal results.
    /// </summary>
    [HttpGet("startup")]
    public async Task<IActionResult> GetStartupInfo()
    {
        try
        {
            await _ignitionCoordinator.WaitAllAsync();
            var result = await _ignitionCoordinator.GetResultAsync();
            
            var response = new
            {
                overallSuccess = result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded),
                totalDuration = result.TotalDuration.TotalMilliseconds,
                globalTimedOut = result.TimedOut,
                signalCount = result.Results.Count,
                successfulSignals = result.Results.Count(s => s.Status == IgnitionSignalStatus.Succeeded),
                failedSignals = result.Results.Count(s => s.Status == IgnitionSignalStatus.Failed),
                timedOutSignals = result.Results.Count(s => s.Status == IgnitionSignalStatus.TimedOut),
                signals = result.Results
                    .OrderBy(s => s.Name)
                    .Select(s => new
                    {
                        name = s.Name,
                        status = s.Status.ToString(),
                        duration = s.Duration.TotalMilliseconds,
                        error = s.Exception?.Message,
                        stackTrace = s.Exception?.StackTrace
                    }).ToArray(),
                checkedAt = DateTimeOffset.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get startup information");
            
            return StatusCode(500, new
            {
                error = "Failed to get startup information",
                message = ex.Message,
                timestamp = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Forces a re-check of startup readiness (note: Ignition is idempotent, so this returns cached results).
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshReadiness()
    {
        _logger.LogInformation("Readiness refresh requested (returns cached results due to idempotency)");
        
        // This will return the cached result due to Ignition's idempotent behavior
        return await GetReadiness();
    }
}