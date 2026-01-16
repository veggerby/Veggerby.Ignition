# Multi-Environment Sample

This sample demonstrates how to configure Veggerby.Ignition for different environments (Development, Production) with environment-specific timeout strategies, signal registration, and execution policies.

## Overview

The sample showcases:
- Environment-specific configuration using `appsettings.{Environment}.json`
- Different timeout strategies for Development vs. Production
- Conditional signal registration based on environment
- Policy and execution mode configuration per environment
- Configuration-driven Ignition options
- Command-line configuration overrides

## Key Concepts

### Environment-Specific Configuration

The sample uses the standard ASP.NET Core configuration hierarchy:

1. **appsettings.json** - Base configuration for all environments
2. **appsettings.Development.json** - Development overrides
3. **appsettings.Production.json** - Production overrides
4. Environment variables
5. Command-line arguments

### Development Environment Strategy

**Optimized for fast feedback and developer productivity:**

- **Policy**: `BestEffort` - Continue even if non-critical signals fail
- **Global Timeout**: 20 seconds (faster feedback)
- **Individual Timeouts**: Shorter (5s database, 3s external API)
- **Cancellation**: No forced cancellation on individual timeouts
- **Max Parallelism**: 8 (faster startup on developer machines)
- **Signals**: Includes development diagnostics, excludes cache warmup
- **Logging**: Debug level for detailed diagnostics

### Production Environment Strategy

**Optimized for reliability and strict validation:**

- **Policy**: `FailFast` - Fail immediately if any signal fails
- **Global Timeout**: 60 seconds (allows time for cold starts)
- **Individual Timeouts**: Longer (20s database, 15s external API, 30s cache warmup)
- **Cancellation**: Force cancellation on all timeouts
- **Max Parallelism**: 4 (controlled resource usage)
- **Signals**: Includes cache warmup, excludes development diagnostics
- **Logging**: Information level (production-appropriate verbosity)

## Signals

### Common Signals (All Environments)

1. **DatabaseSignal**
   - Simulates database connection initialization
   - Timeout: 5s (Dev) / 20s (Prod)
   - Always registered

2. **ExternalApiSignal**
   - Simulates external API health check
   - Timeout: 3s (Dev) / 15s (Prod)
   - Always registered

### Environment-Specific Signals

3. **CacheWarmupSignal** (Production only)
   - Simulates cache preloading
   - Timeout: 30s
   - Registered only in Production for optimal performance
   - Skipped in Development for faster startup

4. **DevelopmentDiagnosticsSignal** (Development only)
   - Runs development-specific health checks
   - Timeout: 2s
   - Registered only in Development
   - Skipped in Production

## Prerequisites

- .NET 10.0 SDK

## Running the Sample

### Development Environment

```bash
cd samples/MultiEnvironment
dotnet run --environment Development
```

Expected behavior:
- Fast startup (< 20 seconds)
- 3 signals executed (Database, ExternalApi, DevelopmentDiagnostics)
- BestEffort policy allows graceful degradation
- Debug-level logging

### Production Environment

```bash
dotnet run --environment Production
```

Expected behavior:
- Longer initialization (< 60 seconds)
- 3 signals executed (Database, ExternalApi, CacheWarmup)
- FailFast policy ensures strict validation
- Information-level logging

### Default Environment

If no environment is specified, `appsettings.json` defaults are used:

```bash
dotnet run
```

## Configuration Structure

### Ignition Configuration Schema

```json
{
  "Ignition": {
    "Policy": "BestEffort | FailFast | ContinueOnTimeout",
    "ExecutionMode": "Parallel | Sequential",
    "GlobalTimeout": "hh:mm:ss",
    "CancelOnGlobalTimeout": true | false,
    "CancelIndividualOnTimeout": true | false,
    "EnableTracing": true | false,
    "MaxDegreeOfParallelism": 1-100,
    "Timeouts": {
      "Database": "hh:mm:ss",
      "ExternalApi": "hh:mm:ss",
      "CacheWarmup": "hh:mm:ss"
    }
  }
}
```

### Signal-Specific Timeouts

Signals read their timeouts from configuration:

```csharp
public DatabaseSignal(IConfiguration configuration)
{
    _timeout = configuration.GetValue<TimeSpan>("Ignition:Timeouts:Database");
}
```

This allows per-environment timeout tuning without code changes.

## Advanced Scenarios

### Override Configuration via Command Line

Override specific settings:

```bash
# Override global timeout
dotnet run --Ignition:GlobalTimeout=00:02:00

# Override policy
dotnet run --Ignition:Policy=FailFast --environment Development

# Override individual signal timeout
dotnet run --Ignition:Timeouts:Database=00:00:30
```

### Override Configuration via Environment Variables

```bash
# Linux/macOS
export Ignition__GlobalTimeout=00:02:00
export Ignition__Policy=FailFast
dotnet run

# Windows
set Ignition__GlobalTimeout=00:02:00
set Ignition__Policy=FailFast
dotnet run
```

Note: Use double underscores (`__`) for nested configuration in environment variables.

### Add Custom Environment

Create `appsettings.Staging.json`:

```json
{
  "Ignition": {
    "Policy": "BestEffort",
    "GlobalTimeout": "00:00:45",
    "Timeouts": {
      "Database": "00:00:15",
      "ExternalApi": "00:00:10",
      "CacheWarmup": "00:00:20"
    }
  }
}
```

Run with:

```bash
dotnet run --environment Staging
```

### Conditional Signal Registration

Register signals based on environment or configuration:

```csharp
services.AddIgnitionSignal<DatabaseSignal>();

if (environment.IsProduction())
{
    services.AddIgnitionSignal<CacheWarmupSignal>();
}

if (configuration.GetValue<bool>("Features:EnableDiagnostics"))
{
    services.AddIgnitionSignal<DiagnosticsSignal>();
}
```

## Best Practices Demonstrated

1. **Configuration-Driven Behavior**: All Ignition settings come from configuration, not hardcoded
2. **Environment-Specific Timeouts**: Adjust timeouts based on environment characteristics
3. **Policy Selection**: Strict validation in production, lenient in development
4. **Conditional Registration**: Register environment-appropriate signals
5. **Timeout Inheritance**: Signals read timeouts from centralized configuration
6. **Logging Levels**: Appropriate verbosity per environment
7. **Override Hierarchy**: Support environment variables and command-line overrides

## Comparison Table

| Aspect | Development | Production |
|--------|-------------|------------|
| **Policy** | BestEffort | FailFast |
| **Global Timeout** | 20s | 60s |
| **Database Timeout** | 5s | 20s |
| **External API Timeout** | 3s | 15s |
| **Max Parallelism** | 8 | 4 |
| **Cancel on Timeout** | No | Yes |
| **Tracing** | Enabled | Disabled |
| **Logging Level** | Debug | Information |
| **Cache Warmup** | ❌ | ✅ |
| **Dev Diagnostics** | ✅ | ❌ |

## Troubleshooting

### Configuration Not Loading

Ensure `appsettings.*.json` files are copied to output:

```xml
<ItemGroup>
  <None Update="appsettings*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Environment Not Detected

Verify environment variable:

```bash
# Check current environment
echo $ASPNETCORE_ENVIRONMENT

# Set explicitly
export ASPNETCORE_ENVIRONMENT=Production
dotnet run
```

### Timeout Values Not Applied

Check configuration binding:

```csharp
// Log loaded configuration
var timeout = configuration.GetValue<TimeSpan>("Ignition:GlobalTimeout");
logger.LogInformation("Global timeout: {Timeout}", timeout);
```

## Additional Resources

- [ASP.NET Core Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
- [ASP.NET Core Environments](https://learn.microsoft.com/aspnet/core/fundamentals/environments)
- [Veggerby.Ignition Options Reference](../../README.md#configuration-options)
