# Custom Integration Sample

**Complexity**: Intermediate  
**Type**: Console Application  
**Focus**: Building custom integration packages from scratch

## Overview

This sample demonstrates how to create a custom integration package for the Veggerby.Ignition library. It shows best practices for building integrations that others can reuse.

## What It Demonstrates

### Core Patterns

- **Custom Signal Implementation**: Implementing `IIgnitionSignal` interface
- **Factory Pattern**: Clean DI registration through extension methods
- **Error Handling**: Proper exception handling and logging patterns
- **Documentation**: XML documentation for public APIs
- **Testing**: How to structure integration tests (see notes below)

### Fictional "Acme Cache" Integration

The sample builds a complete integration for a fictional caching service called "Acme Cache". This pattern can be adapted for any service:

- Connection establishment
- Health checking
- Timeout configuration
- Dependency injection registration

## Prerequisites

- .NET 10.0 SDK
- No external services required (uses simulated delays)

## How to Run

```bash
cd samples/CustomIntegration
dotnet run
```

## Expected Output

```
=== Custom Integration Sample ===

This sample demonstrates building a custom integration package
for the fictional 'Acme Cache' service.

🏗️  Building Custom Integration for Acme Cache

Starting Acme Cache initialization...

info: CustomIntegration.AcmeCacheSignal[0]
      Connecting to Acme Cache at acme-cache://localhost:9999...
info: CustomIntegration.AcmeCacheSignal[0]
      Performing Acme Cache health check...
info: CustomIntegration.AcmeCacheSignal[0]
      Acme Cache connection established and healthy

📊 Initialization Results:
   Total Duration: 800ms
   Timed Out: NO
   ✅ acme-cache: Succeeded (800ms)

✅ Overall Status: SUCCESS

🎉 Custom integration completed successfully!

📚 Key Concepts Demonstrated:
   • Custom IIgnitionSignal implementation
   • Factory pattern for DI registration
   • Proper exception handling and logging
   • XML documentation best practices
   • Extension method pattern for clean API
```

## Key Components

### 1. Signal Implementation (`AcmeCacheSignal`)

```csharp
public class AcmeCacheSignal : IIgnitionSignal
{
    public string Name => "acme-cache";
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10);

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        // Connection logic here
    }
}
```

**Key Points**:
- Implement all three members: `Name`, `Timeout`, `WaitAsync`
- Use cancellation tokens properly
- Log at appropriate stages
- Handle exceptions gracefully

### 2. Factory Pattern (`AcmeCacheSignalFactory`)

```csharp
public static IServiceCollection AddAcmeCacheSignal(
    this IServiceCollection services,
    string connectionString,
    TimeSpan? timeout = null)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));
    
    services.AddIgnitionSignal(sp => new AcmeCacheSignal(
        sp.GetRequiredService<ILogger<AcmeCacheSignal>>(),
        connectionString));
    
    return services;
}
```

**Key Points**:
- Extension methods for clean API
- Guard clauses for validation
- Return IServiceCollection for chaining
- Support optional configuration overrides

## Building a Real Integration

To create your own integration package:

### 1. Structure

```
YourCompany.Ignition.YourService/
├── src/
│   ├── YourServiceSignal.cs          # Core signal implementation
│   ├── YourServiceExtensions.cs      # DI registration helpers
│   └── YourCompany.Ignition.YourService.csproj
├── tests/
│   ├── YourServiceSignalTests.cs     # Unit tests
│   └── YourCompany.Ignition.YourService.Tests.csproj
└── README.md                          # Usage documentation
```

### 2. Dependencies

Minimal dependencies (core library only):

```xml
<ItemGroup>
  <PackageReference Include="Veggerby.Ignition" Version="x.y.z" />
</ItemGroup>
```

Add service-specific SDK if needed (e.g., Redis client, database driver).

### 3. Testing with Testcontainers

For real service integrations, use Testcontainers:

```csharp
[Fact]
public async Task AcmeCacheSignal_Succeeds_WhenServiceAvailable()
{
    // arrange
    await using var container = new ContainerBuilder()
        .WithImage("acme/cache:latest")
        .WithPortBinding(9999, 9999)
        .Build();
    
    await container.StartAsync();
    
    var host = Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddIgnition();
            services.AddAcmeCacheSignal("acme-cache://localhost:9999");
        })
        .Build();
    
    var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();
    
    // act
    await coordinator.WaitAllAsync();
    var result = await coordinator.GetResultAsync();
    
    // assert
    result.Results.Should().ContainSingle(r => 
        r.Name == "acme-cache" && 
        r.Status == IgnitionSignalStatus.Succeeded);
}
```

### 4. XML Documentation

Document all public APIs:

```csharp
/// <summary>
/// Signal for initializing Acme Cache connections.
/// </summary>
/// <remarks>
/// This signal performs:
/// <list type="bullet">
///   <item>Connection establishment</item>
///   <item>Health check validation</item>
///   <item>Authentication handshake</item>
/// </list>
/// </remarks>
public class AcmeCacheSignal : IIgnitionSignal
{
    // Implementation...
}
```

### 5. Package Metadata

For NuGet publishing:

```xml
<PropertyGroup>
  <PackageId>YourCompany.Ignition.YourService</PackageId>
  <Description>Veggerby.Ignition integration for YourService</Description>
  <PackageTags>ignition;startup;readiness;yourservice</PackageTags>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
</PropertyGroup>
```

## Adapting This Template

1. **Replace "Acme Cache"** with your service name
2. **Add service-specific logic** in `WaitAsync`:
   - Connection establishment
   - Authentication
   - Health checks
   - Schema validation
3. **Add configuration options** (connection strings, timeouts, retries)
4. **Write tests** with real or simulated services
5. **Document** prerequisites and usage patterns

## Related Samples

- [Simple](../Simple/) - Basic signal usage
- [Bundles](../Bundles/) - Packaging multiple signals
- [TestcontainersDemo](../TestcontainersDemo/) - Real infrastructure testing

## Further Reading

- [Main Documentation](../../README.md)
- [Contributing Guide](../../CONTRIBUTING.md)
- [Existing Integrations](../../src/) - Redis, PostgreSQL, RabbitMQ, etc.
