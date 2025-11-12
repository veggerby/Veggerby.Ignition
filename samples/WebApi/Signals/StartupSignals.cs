using Veggerby.Ignition;

namespace WebApi.Signals;

/// <summary>
/// Simulates establishing a connection pool to a database.
/// </summary>
public class DatabaseConnectionPoolSignal : IIgnitionSignal
{
    private readonly ILogger<DatabaseConnectionPoolSignal> _logger;

    public string Name => "database-connection-pool";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public DatabaseConnectionPoolSignal(ILogger<DatabaseConnectionPoolSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing database connection pool...");

        // Simulate connection pool initialization
        await Task.Delay(2000, cancellationToken);

        _logger.LogInformation("Database connection pool initialized with 10 connections");
    }
}

/// <summary>
/// Simulates loading and validating configuration from various sources.
/// </summary>
public class ConfigurationValidationSignal : IIgnitionSignal
{
    private readonly ILogger<ConfigurationValidationSignal> _logger;
    private readonly IConfiguration _configuration;

    public string Name => "configuration-validation";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

    public ConfigurationValidationSignal(
        ILogger<ConfigurationValidationSignal> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating application configuration...");

        // Simulate configuration validation
        await Task.Delay(800, cancellationToken);

        // Example validation logic
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection connection string is required");
        }

        var apiKey = _configuration["ApiSettings:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("ApiKey is not configured - some features may be limited");
        }

        _logger.LogInformation("Configuration validation completed successfully");
    }
}

/// <summary>
/// Simulates checking connectivity to external dependencies.
/// </summary>
public class ExternalDependencyCheckSignal : IIgnitionSignal
{
    private readonly ILogger<ExternalDependencyCheckSignal> _logger;
    private readonly HttpClient _httpClient;

    public string Name => "external-dependency-check";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(15);

    public ExternalDependencyCheckSignal(
        ILogger<ExternalDependencyCheckSignal> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking external dependencies...");

        var dependencies = new[]
        {
            "https://api.github.com",
            "https://httpbin.org/status/200"
        };

        foreach (var dependency in dependencies)
        {
            _logger.LogInformation("Checking connectivity to {Dependency}...", dependency);

            try
            {
                using var response = await _httpClient.GetAsync(dependency, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✓ {Dependency} is accessible", dependency);
                }
                else
                {
                    _logger.LogWarning("⚠ {Dependency} returned {StatusCode}", dependency, response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("⚠ Failed to connect to {Dependency}: {Error}", dependency, ex.Message);
            }

            // Small delay between checks
            await Task.Delay(200, cancellationToken);
        }

        _logger.LogInformation("External dependency checks completed");
    }
}

/// <summary>
/// Simulates initializing background services and scheduled tasks.
/// </summary>
public class BackgroundServicesSignal : IIgnitionSignal
{
    private readonly ILogger<BackgroundServicesSignal> _logger;

    public string Name => "background-services";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(8);

    public BackgroundServicesSignal(ILogger<BackgroundServicesSignal> logger)
    {
        _logger = logger;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting background services...");

        var services = new[]
        {
            "Message Queue Consumer",
            "Metrics Collector",
            "Cache Refresh Service",
            "Log Cleanup Service"
        };

        foreach (var service in services)
        {
            _logger.LogInformation("Starting {Service}...", service);
            await Task.Delay(400, cancellationToken);
            _logger.LogInformation("✓ {Service} started", service);
        }

        _logger.LogInformation("All background services started successfully");
    }
}
