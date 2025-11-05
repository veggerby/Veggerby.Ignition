using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Sample API controller demonstrating application functionality.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly ILogger<WeatherController> _logger;

    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public WeatherController(ILogger<WeatherController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets a sample weather forecast.
    /// </summary>
    [HttpGet("forecast")]
    public IEnumerable<WeatherForecast> GetForecast()
    {
        _logger.LogInformation("Weather forecast requested");
        
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            Summaries[Random.Shared.Next(Summaries.Length)]
        ))
        .ToArray();
    }

    /// <summary>
    /// Gets current weather information.
    /// </summary>
    [HttpGet("current")]
    public ActionResult<WeatherInfo> GetCurrent()
    {
        _logger.LogInformation("Current weather requested");
        
        var weather = new WeatherInfo
        {
            Location = "Sample City",
            Temperature = Random.Shared.Next(-10, 35),
            Humidity = Random.Shared.Next(30, 90),
            Conditions = Summaries[Random.Shared.Next(Summaries.Length)],
            LastUpdated = DateTimeOffset.UtcNow
        };

        return Ok(weather);
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public record WeatherInfo
{
    public string Location { get; init; } = string.Empty;
    public int Temperature { get; init; }
    public int Humidity { get; init; }
    public string Conditions { get; init; } = string.Empty;
    public DateTimeOffset LastUpdated { get; init; }
}