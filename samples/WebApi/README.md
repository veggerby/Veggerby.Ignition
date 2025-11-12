# Web API Ignition Sample

This sample demonstrates integration of the Veggerby.Ignition library with an ASP.NET Core Web API application.

## What it demonstrates

- ASP.NET Core integration with dependency injection
- Health check integration with Ignition readiness
- Startup initialization before accepting requests
- RESTful endpoints for monitoring readiness status
- Best-effort policy allowing partial failures
- Real-world signals (database, configuration, external services)
- Swagger/OpenAPI documentation integration

## Signals included

1. **DatabaseConnectionPoolSignal** - Connection pool initialization (2s, 10s timeout)
2. **ConfigurationValidationSignal** - Configuration validation (0.8s, 5s timeout)
3. **ExternalDependencyCheckSignal** - External API connectivity checks (variable, 15s timeout)
4. **BackgroundServicesSignal** - Background service startup (1.6s, 8s timeout)

## Configuration

The application uses a **BestEffort** policy with parallel execution, allowing the web API to start even if some non-critical services fail (like external dependency checks).

Key configuration in `Program.cs`:

- **Policy**: BestEffort (tolerates failures)
- **ExecutionMode**: Parallel (max 4 concurrent)
- **GlobalTimeout**: 30 seconds
- **Individual timeouts**: Respected per signal

## Endpoints

### Health & Readiness

- `GET /api/health/ready` - Startup readiness status (JSON)
- `GET /api/health/startup` - Detailed startup information
- `POST /api/health/refresh` - Force readiness refresh (returns cached)
- `GET /health` - ASP.NET Core health checks (includes Ignition)
- `GET /health/ready` - Readiness-only health check

### Sample API

- `GET /api/weather/forecast` - Sample weather forecast
- `GET /api/weather/current` - Current weather information

### Documentation

- `/swagger` - Swagger UI (development only)

## Running the sample

```bash
cd samples/WebApi
dotnet run
```

The application will be available at:

- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

## Expected startup output

```text
info: Program[0]
      Starting application initialization...
info: WebApi.Signals.ConfigurationValidationSignal[0]
      Validating application configuration...
info: WebApi.Signals.DatabaseConnectionPoolSignal[0]
      Initializing database connection pool...
info: WebApi.Signals.ExternalDependencyCheckSignal[0]
      Checking external dependencies...
info: WebApi.Signals.BackgroundServicesSignal[0]
      Starting background services...
info: WebApi.Signals.ConfigurationValidationSignal[0]
      Configuration validation completed successfully
info: WebApi.Signals.ExternalDependencyCheckSignal[0]
      Checking connectivity to https://api.github.com...
info: WebApi.Signals.ExternalDependencyCheckSignal[0]
      ✓ https://api.github.com is accessible
info: WebApi.Signals.BackgroundServicesSignal[0]
      Starting Message Queue Consumer...
info: WebApi.Signals.BackgroundServicesSignal[0]
      ✓ Message Queue Consumer started
info: WebApi.Signals.DatabaseConnectionPoolSignal[0]
      Database connection pool initialized with 10 connections
info: WebApi.Signals.ExternalDependencyCheckSignal[0]
      External dependency checks completed
info: WebApi.Signals.BackgroundServicesSignal[0]
      All background services started successfully
info: Program[0]
      Application initialization completed successfully in 2150ms
info: Program[0]
      ✓ background-services completed in 1600ms
info: Program[0]
      ✓ configuration-validation completed in 800ms
info: Program[0]
      ✓ database-connection-pool completed in 2000ms
info: Program[0]
      ✓ external-dependency-check completed in 1400ms
info: Program[0]
      Web API is ready to accept requests
```

## Example API responses

### GET /api/health/ready

```json
{
  "ready": true,
  "duration": 2150.0,
  "globalTimedOut": false,
  "policyApplied": "BestEffort",
  "signals": [
    {
      "name": "background-services",
      "status": "Succeeded",
      "duration": 1600.0,
      "error": null
    },
    {
      "name": "configuration-validation",
      "status": "Succeeded",
      "duration": 800.0,
      "error": null
    }
  ],
  "timestamp": "2024-01-15T10:30:45.123Z"
}
```

### GET /health

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "ignition-readiness",
      "status": "Healthy",
      "duration": 0.1,
      "description": "Ignition readiness check",
      "data": {}
    }
  ],
  "totalDuration": 0.5
}
```

## Key concepts demonstrated

- **Web API integration**: Seamless startup coordination
- **Health check integration**: Standard ASP.NET Core health checks
- **Configuration validation**: Real-world configuration checking
- **External dependency resilience**: Graceful handling of external service issues
- **Monitoring endpoints**: Comprehensive readiness and status reporting
- **Best-effort policy**: Continue serving requests despite some failures
