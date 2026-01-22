# Creating Integration Packages

This guide covers best practices for authoring Veggerby.Ignition integration packages that extend the library to support specific technologies (databases, messaging systems, cloud services, etc.).

## Table of Contents

- [Overview](#overview)
- [Package Structure](#package-structure)
- [Naming Conventions](#naming-conventions)
- [Implementation Patterns](#implementation-patterns)
- [Retry Policy Integration](#retry-policy-integration)
- [Factory Pattern](#factory-pattern)
- [Testing with Testcontainers](#testing-with-testcontainers)
- [XML Documentation Standards](#xml-documentation-standards)
- [Directory.Build.props Integration](#directorybuildprops-integration)
- [README Template](#readme-template)
- [Publishing](#publishing)

## Overview

Integration packages provide ready-to-use ignition signals for popular technologies, eliminating boilerplate and ensuring consistency across projects. Well-designed integration packages:

- ✅ Follow consistent naming conventions
- ✅ Include retry logic for transient failures
- ✅ Provide factory abstractions for flexibility
- ✅ Include comprehensive tests using Testcontainers
- ✅ Document all public APIs with XML comments
- ✅ Integrate with Directory.Build.props for versioning

## Package Structure

A typical integration package has this structure:

```text
src/Veggerby.Ignition.Redis/
├── RedisReadinessSignal.cs          # Core signal implementation
├── RedisReadinessSignalFactory.cs   # Factory for creating signals
├── RedisIgnitionExtensions.cs       # DI extension methods
├── RedisReadinessOptions.cs         # Configuration options
├── README.md                         # Package documentation
└── Veggerby.Ignition.Redis.csproj   # Project file

test/Veggerby.Ignition.Redis.Tests/
├── RedisReadinessSignalTests.cs     # Unit tests
├── RedisIntegrationTests.cs         # Integration tests with Testcontainers
└── Veggerby.Ignition.Redis.Tests.csproj
```

## Naming Conventions

### Package Name

Format: `Veggerby.Ignition.<Technology>`

**Examples:**

- `Veggerby.Ignition.Redis`
- `Veggerby.Ignition.SqlServer`
- `Veggerby.Ignition.RabbitMq`
- `Veggerby.Ignition.Aws` (for AWS services)
- `Veggerby.Ignition.Azure` (for Azure services)

### File Naming

| Component | Format | Example |
|-----------|--------|---------|
| Signal | `<Technology>ReadinessSignal.cs` | `RedisReadinessSignal.cs` |
| Factory | `<Technology>ReadinessSignalFactory.cs` | `RedisReadinessSignalFactory.cs` |
| Extensions | `<Technology>IgnitionExtensions.cs` | `RedisIgnitionExtensions.cs` |
| Options | `<Technology>ReadinessOptions.cs` | `RedisReadinessOptions.cs` |

### Namespace

Format: `Veggerby.Ignition.<Technology>`

```csharp
namespace Veggerby.Ignition.Redis;
```

**Note:** The `#pragma warning disable IDE0130` directive is **NOT** needed for integration packages because the namespace matches the folder structure (`src/Veggerby.Ignition.Redis/` → `namespace Veggerby.Ignition.Redis`). Only use the pragma when the namespace intentionally does not match the folder structure (e.g., core library files in subfolders that use the root `Veggerby.Ignition` namespace).

### Signal Name

Use lowercase with hyphens: `<technology>-readiness`

```csharp
public string Name => "redis-readiness";
```

## Implementation Patterns

### Core Signal Implementation

```csharp
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Redis;

/// <summary>
/// Ignition signal for verifying Redis cache readiness.
/// Validates connection and optionally executes PING or test key operations.
/// </summary>
internal sealed class RedisReadinessSignal : IIgnitionSignal
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly RedisReadinessOptions _options;
    private readonly ILogger<RedisReadinessSignal> _logger;
    private readonly object _sync = new();
    private Task? _cachedTask;

    public RedisReadinessSignal(
        IConnectionMultiplexer connectionMultiplexer,
        RedisReadinessOptions options,
        ILogger<RedisReadinessSignal> logger)
    {
        _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "redis-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTask is null)
        {
            lock (_sync)
            {
                _cachedTask ??= ExecuteAsync(cancellationToken);
            }
        }

        return cancellationToken.CanBeCanceled && !_cachedTask.IsCompleted
            ? _cachedTask.WaitAsync(cancellationToken)
            : _cachedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var activity = Activity.Current;
        activity?.SetTag("redis.connection", _connectionMultiplexer.Configuration);

        _logger.LogInformation("Redis readiness check starting");

        var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

        await retryPolicy.ExecuteAsync(
            async ct =>
            {
                if (!_connectionMultiplexer.IsConnected)
                {
                    throw new InvalidOperationException("Redis connection multiplexer is not connected");
                }

                await PerformReadinessCheckAsync(ct);
            },
            "Redis readiness check",
            cancellationToken);

        _logger.LogInformation("Redis readiness check completed successfully");
    }

    private async Task PerformReadinessCheckAsync(CancellationToken cancellationToken)
    {
        // Technology-specific readiness logic
        var db = _connectionMultiplexer.GetDatabase();
        await db.PingAsync();
    }
}
```

### Configuration Options

```csharp
/// <summary>
/// Configuration options for Redis readiness verification.
/// </summary>
public sealed class RedisReadinessOptions
{
    /// <summary>
    /// Gets or sets the timeout for the readiness check.
    /// Default is 10 seconds.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay between retry attempts.
    /// Default is 100ms. Delay doubles with each retry (exponential backoff).
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the verification strategy for Redis readiness.
    /// Default is <see cref="RedisVerificationStrategy.Ping"/>.
    /// </summary>
    public RedisVerificationStrategy VerificationStrategy { get; set; } = RedisVerificationStrategy.Ping;
}

/// <summary>
/// Verification strategies for Redis readiness checks.
/// </summary>
public enum RedisVerificationStrategy
{
    /// <summary>
    /// Only verifies connection status (no network call).
    /// </summary>
    ConnectionOnly,

    /// <summary>
    /// Executes PING command to verify server responsiveness.
    /// </summary>
    Ping,

    /// <summary>
    /// Executes PING and test key round-trip for full validation.
    /// </summary>
    PingAndTestKey
}
```

### Extension Methods

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Veggerby.Ignition.Redis;

/// <summary>
/// Extension methods for registering Redis ignition signals.
/// </summary>
public static class RedisIgnitionExtensions
{
    /// <summary>
    /// Adds a Redis readiness signal to the ignition pipeline.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for readiness options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisIgnition(
        this IServiceCollection services,
        Action<RedisReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        var options = new RedisReadinessOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IIgnitionSignal, RedisReadinessSignal>();

        return services;
    }

    /// <summary>
    /// Adds a Redis readiness signal using a factory function.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionMultiplexerFactory">Factory function to create connection multiplexer.</param>
    /// <param name="configure">Optional configuration action for readiness options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRedisIgnition(
        this IServiceCollection services,
        Func<IServiceProvider, IConnectionMultiplexer> connectionMultiplexerFactory,
        Action<RedisReadinessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(connectionMultiplexerFactory, nameof(connectionMultiplexerFactory));

        var options = new RedisReadinessOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IIgnitionSignal>(sp =>
        {
            var multiplexer = connectionMultiplexerFactory(sp);
            var logger = sp.GetRequiredService<ILogger<RedisReadinessSignal>>();
            return new RedisReadinessSignal(multiplexer, options, logger);
        });

        return services;
    }
}
```

## Retry Policy Integration

Integration packages should use the shared `RetryPolicy` class from `Veggerby.Ignition` for consistent retry behavior.

### Basic Usage

```csharp
private async Task ExecuteAsync(CancellationToken cancellationToken)
{
    var retryPolicy = new RetryPolicy(
        maxRetries: _options.MaxRetries,
        initialDelay: _options.RetryDelay,
        logger: _logger);

    await retryPolicy.ExecuteAsync(
        async ct =>
        {
            // Perform technology-specific readiness check
            await PerformCheckAsync(ct);
        },
        operationName: "Database readiness check",
        cancellationToken);
}
```

### Configuration Options

Expose retry configuration in your options class:

```csharp
public sealed class MyTechnologyReadinessOptions
{
    /// <summary>
    /// Maximum number of retry attempts for transient failures.
    /// Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retry attempts. Doubles with each retry.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
```

### Retry with Custom Condition

For conditional retry logic:

```csharp
await retryPolicy.ExecuteAsync(
    operation: async ct => await PerformCheckAsync(ct),
    shouldRetry: attempt => _connection.State == ConnectionState.Connecting,
    operationName: "Database connection",
    cancellationToken);
```

## Factory Pattern

Implement `IIgnitionSignalFactory` for dynamic signal creation scenarios.

### Factory Interface

```csharp
/// <summary>
/// Factory for creating Redis readiness signals.
/// </summary>
public sealed class RedisReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IConnectionMultiplexer> _connectionMultiplexerFactory;
    private readonly RedisReadinessOptions _options;
    private readonly ILogger<RedisReadinessSignal> _logger;

    public RedisReadinessSignalFactory(
        Func<IConnectionMultiplexer> connectionMultiplexerFactory,
        RedisReadinessOptions options,
        ILogger<RedisReadinessSignal> logger)
    {
        _connectionMultiplexerFactory = connectionMultiplexerFactory ?? throw new ArgumentNullException(nameof(connectionMultiplexerFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal()
    {
        var multiplexer = _connectionMultiplexerFactory();
        return new RedisReadinessSignal(multiplexer, _options, _logger);
    }
}
```

### Factory Registration

```csharp
public static IServiceCollection AddRedisIgnitionFactory(
    this IServiceCollection services,
    Func<IConnectionMultiplexer> connectionMultiplexerFactory,
    Action<RedisReadinessOptions>? configure = null)
{
    ArgumentNullException.ThrowIfNull(services, nameof(services));
    ArgumentNullException.ThrowIfNull(connectionMultiplexerFactory, nameof(connectionMultiplexerFactory));

    var options = new RedisReadinessOptions();
    configure?.Invoke(options);

    services.AddSingleton(options);
    services.AddSingleton<IIgnitionSignalFactory>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<RedisReadinessSignal>>();
        return new RedisReadinessSignalFactory(connectionMultiplexerFactory, options, logger);
    });

    return services;
}
```

## Testing with Testcontainers

Use [Testcontainers](https://dotnet.testcontainers.org/) for integration tests against real instances.

### Setup

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Testcontainers" />
    <PackageReference Include="Testcontainers.Redis" />
    <PackageReference Include="xunit" />
  </ItemGroup>
</Project>
```

### Integration Test Pattern

```csharp
using Testcontainers.Redis;
using Xunit;

public sealed class RedisReadinessIntegrationTests : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private IConnectionMultiplexer? _connectionMultiplexer;

    public async Task InitializeAsync()
    {
        // Start Redis container
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7.2-alpine")
            .Build();

        await _redisContainer.StartAsync();

        // Create connection multiplexer
        _connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync(
            _redisContainer.GetConnectionString());
    }

    [Fact]
    public async Task RedisReadinessSignal_WithRunningContainer_Succeeds()
    {
        // arrange
        var options = new RedisReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(10),
            VerificationStrategy = RedisVerificationStrategy.PingAndTestKey
        };

        var logger = NSubstitute.Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(_connectionMultiplexer!, options, logger);

        // act
        await signal.WaitAsync(CancellationToken.None);

        // assert
        // No exception = success
    }

    [Fact]
    public async Task RedisReadinessSignal_WithStoppedContainer_FailsWithRetry()
    {
        // arrange
        await _redisContainer!.StopAsync();

        var options = new RedisReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(5),
            MaxRetries = 2,
            RetryDelay = TimeSpan.FromMilliseconds(50)
        };

        var logger = NSubstitute.Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(_connectionMultiplexer!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => signal.WaitAsync(CancellationToken.None));
    }

    public async Task DisposeAsync()
    {
        _connectionMultiplexer?.Dispose();

        if (_redisContainer is not null)
        {
            await _redisContainer.DisposeAsync();
        }
    }
}
```

### Unit Test Pattern

For unit tests without containers, use mocks:

```csharp
using NSubstitute;
using Xunit;

public sealed class RedisReadinessSignalTests
{
    [Fact]
    public async Task WaitAsync_CallsExecuteAsync()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.IsConnected.Returns(true);

        var db = Substitute.For<IDatabase>();
        db.PingAsync().Returns(Task.FromResult(TimeSpan.FromMilliseconds(10)));
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        var options = new RedisReadinessOptions();
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();

        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act
        await signal.WaitAsync(CancellationToken.None);

        // assert
        await db.Received(1).PingAsync();
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var options = new RedisReadinessOptions { Timeout = TimeSpan.FromSeconds(15) };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();

        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act
        var timeout = signal.Timeout;

        // assert
        Assert.Equal(TimeSpan.FromSeconds(15), timeout);
    }
}
```

## XML Documentation Standards

All public types and members must have XML documentation.

### Signal Documentation

```csharp
/// <summary>
/// Ignition signal for verifying Redis cache readiness.
/// Validates connection and optionally executes PING or test key operations.
/// </summary>
/// <remarks>
/// <para>
/// This signal uses <see cref="RetryPolicy"/> with exponential backoff for transient failure handling.
/// Configure retry behavior via <see cref="RedisReadinessOptions.MaxRetries"/> and <see cref="RedisReadinessOptions.RetryDelay"/>.
/// </para>
/// <para>
/// Verification strategies:
/// <list type="bullet">
///   <item><see cref="RedisVerificationStrategy.ConnectionOnly"/>: Fast connection check</item>
///   <item><see cref="RedisVerificationStrategy.Ping"/>: PING command for server responsiveness</item>
///   <item><see cref="RedisVerificationStrategy.PingAndTestKey"/>: Full round-trip validation</item>
/// </list>
/// </para>
/// </remarks>
internal sealed class RedisReadinessSignal : IIgnitionSignal
{
    // Implementation
}
```

### Extension Method Documentation

```csharp
/// <summary>
/// Adds a Redis readiness signal to the ignition pipeline.
/// </summary>
/// <param name="services">The service collection.</param>
/// <param name="configure">Optional configuration action for readiness options.</param>
/// <returns>The service collection for chaining.</returns>
/// <example>
/// <code>
/// builder.Services.AddRedisIgnition(opts =>
/// {
///     opts.Timeout = TimeSpan.FromSeconds(15);
///     opts.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
/// });
/// </code>
/// </example>
public static IServiceCollection AddRedisIgnition(
    this IServiceCollection services,
    Action<RedisReadinessOptions>? configure = null)
{
    // Implementation
}
```

### Options Documentation

```csharp
/// <summary>
/// Configuration options for Redis readiness verification.
/// </summary>
public sealed class RedisReadinessOptions
{
    /// <summary>
    /// Gets or sets the timeout for the readiness check.
    /// Default is 10 seconds.
    /// </summary>
    /// <remarks>
    /// This timeout applies to the entire readiness check including all retry attempts.
    /// Consider setting this higher than <c>MaxRetries × RetryDelay</c> to allow retries to complete.
    /// </remarks>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
```

## Directory.Build.props Integration

Integration packages should inherit shared build properties from the repository root.

### Package Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Veggerby.Ignition.Redis</RootNamespace>
    <AssemblyName>Veggerby.Ignition.Redis</AssemblyName>
    <PackageId>Veggerby.Ignition.Redis</PackageId>
    <Description>Redis integration for Veggerby.Ignition startup readiness coordination.</Description>
    <PackageTags>ignition;redis;startup;readiness;health-check</PackageTags>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Veggerby.Ignition\Veggerby.Ignition.csproj" />
    <PackageReference Include="StackExchange.Redis" />
  </ItemGroup>
</Project>
```

### Test Project File

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Veggerby.Ignition.Redis\Veggerby.Ignition.Redis.csproj" />
    <PackageReference Include="xunit" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="Testcontainers.Redis" />
  </ItemGroup>
</Project>
```

## README Template

Each integration package should include a README.md:

```markdown
# Veggerby.Ignition.Redis

Redis integration for [Veggerby.Ignition](https://github.com/veggerby/Veggerby.Ignition) startup readiness coordination.

## Installation

```bash
dotnet add package Veggerby.Ignition.Redis
```

## Quick Start

```csharp
using Veggerby.Ignition.Redis;

var builder = WebApplication.CreateBuilder(args);

// Register Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379"));

// Add Redis readiness signal
builder.Services.AddRedisIgnition(opts =>
{
    opts.Timeout = TimeSpan.FromSeconds(15);
    opts.VerificationStrategy = RedisVerificationStrategy.PingAndTestKey;
    opts.MaxRetries = 3;
});

// Configure ignition
builder.Services.AddIgnition(opts =>
{
    opts.GlobalTimeout = TimeSpan.FromSeconds(30);
    opts.Policy = IgnitionPolicy.FailFast;
});

var app = builder.Build();

// Wait for Redis readiness
var coordinator = app.Services.GetRequiredService<IIgnitionCoordinator>();
await coordinator.WaitAllAsync();

app.Run();
```

## Configuration

### RedisReadinessOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Timeout` | `TimeSpan?` | 10 seconds | Timeout for the entire readiness check |
| `MaxRetries` | `int` | 3 | Maximum retry attempts for transient failures |
| `RetryDelay` | `TimeSpan` | 100ms | Initial delay between retries (exponential backoff) |
| `VerificationStrategy` | `RedisVerificationStrategy` | `Ping` | Verification depth |

### Verification Strategies

- **ConnectionOnly**: Fast connection check (no network call)
- **Ping**: Executes PING command
- **PingAndTestKey**: Full round-trip validation with test key

## License

MIT License - see [LICENSE](../../LICENSE) for details.
```

## Publishing

### NuGet Package Metadata

Ensure your `.csproj` includes complete metadata:

```xml
<PropertyGroup>
  <PackageId>Veggerby.Ignition.Redis</PackageId>
  <Description>Redis integration for Veggerby.Ignition startup readiness coordination.</Description>
  <Authors>Your Name</Authors>
  <PackageTags>ignition;redis;startup;readiness;health-check;distributed-cache</PackageTags>
  <PackageProjectUrl>https://github.com/veggerby/Veggerby.Ignition</PackageProjectUrl>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <RepositoryUrl>https://github.com/veggerby/Veggerby.Ignition</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
</PropertyGroup>
```

### Publishing Workflow

1. **Version**: Use GitVersion or manual versioning
2. **Pack**: `dotnet pack -c Release`
3. **Test**: Run all tests including integration tests
4. **Publish**: `dotnet nuget push bin/Release/Veggerby.Ignition.Redis.*.nupkg`

## Checklist for New Integration Packages

Before publishing, ensure:

- [ ] Package follows naming convention (`Veggerby.Ignition.<Technology>`)
- [ ] All public APIs have XML documentation
- [ ] Retry policy integrated for transient failures
- [ ] Factory pattern implemented (if applicable)
- [ ] Unit tests cover core logic
- [ ] Integration tests use Testcontainers (when possible)
- [ ] README.md includes installation and quick start
- [ ] Configuration options documented
- [ ] Examples compile and run
- [ ] Package metadata complete (tags, description, license)
- [ ] Cross-references to main Veggerby.Ignition documentation

## Related Documentation

- [Bundles Guide](bundles.md) - Creating reusable signal bundles
- [Advanced Patterns](advanced-patterns.md) - Signal composition and factories
- [Getting Started](getting-started.md) - Basic signal creation
- [API Reference](api-reference.md) - Complete API documentation
