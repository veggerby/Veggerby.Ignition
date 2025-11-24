# Migration Guide

This guide helps you upgrade between versions of Veggerby.Ignition and adopt new features.

## Version History

### Current Version

Latest stable version: **1.0.0** (check [NuGet](https://www.nuget.org/packages/Veggerby.Ignition))

### Version Compatibility

- .NET 8.0+
- .NET 9.0+

## Upgrading to v1.0.0

### New in v1.0.0

✨ **New Features**:

- Dependency-aware execution (DAG mode)
- Ignition bundles
- `[SignalDependency]` attribute
- Enhanced timeout management
- Improved diagnostics

🔄 **Breaking Changes**:

None (initial release)

### Installation

```bash
dotnet add package Veggerby.Ignition --version 1.0.0
```

### Migration Steps

As this is the initial release, no migration is required. See [Getting Started](getting-started.md) for setup instructions.

## Adopting New Features

### Adopting DAG Mode

If you previously used sequential ordering, consider DAG mode for better performance:

#### Before (Sequential)

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.Sequential;
});

// Order matters
builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();
```

#### After (DAG)

```csharp
builder.Services.AddIgnition(options =>
{
    options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
});

[SignalDependency("database")]
public class CacheSignal : IIgnitionSignal { /* ... */ }

[SignalDependency("cache")]
public class WorkerSignal : IIgnitionSignal { /* ... */ }

builder.Services.AddIgnitionSignal<DatabaseSignal>();
builder.Services.AddIgnitionSignal<CacheSignal>();
builder.Services.AddIgnitionSignal<WorkerSignal>();

builder.Services.AddIgnitionGraph((builder, sp) =>
{
    var signals = sp.GetServices<IIgnitionSignal>();
    builder.AddSignals(signals);
    builder.ApplyAttributeDependencies();
});
```

**Benefits**: Explicit dependencies, automatic parallel execution of independent branches.

### Adopting Bundles

If you have repeated signal patterns, extract into bundles:

#### Before (Repeated Code)

```csharp
// Project 1
builder.Services.AddIgnitionFromTask("redis:connect", ct => ConnectAsync(ct));
builder.Services.AddIgnitionFromTask("redis:health", ct => HealthCheckAsync(ct));

// Project 2 (same pattern)
builder.Services.AddIgnitionFromTask("redis:connect", ct => ConnectAsync(ct));
builder.Services.AddIgnitionFromTask("redis:health", ct => HealthCheckAsync(ct));
```

#### After (Reusable Bundle)

```csharp
// Shared library
public class RedisBundle : IIgnitionBundle { /* ... */ }

// Project 1
builder.Services.AddIgnitionBundle(new RedisBundle("localhost:6379"));

// Project 2
builder.Services.AddIgnitionBundle(new RedisBundle("localhost:6379"));
```

**Benefits**: Reusability, consistency, maintainability.

### Adopting Enhanced Timeouts

Use the two-layer timeout system for better control:

#### Before (Single Timeout)

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
});
```

#### After (Global + Per-Signal)

```csharp
builder.Services.AddIgnition(options =>
{
    options.GlobalTimeout = TimeSpan.FromSeconds(30);
    options.CancelOnGlobalTimeout = true; // Hard deadline
    options.CancelIndividualOnTimeout = true; // Cancel slow signals
});

public class DatabaseSignal : IIgnitionSignal
{
    public TimeSpan? Timeout => TimeSpan.FromSeconds(10); // Per-signal
    // ...
}
```

**Benefits**: Fine-grained control, better diagnostics.

## Best Practices for Upgrades

### 1. Test in Non-Production First

```bash
# Staging environment
dotnet add package Veggerby.Ignition --version 1.0.0

# Run tests
dotnet test

# Verify startup
dotnet run
```

### 2. Review Release Notes

Check [CHANGELOG.md](../CHANGELOG.md) for detailed changes.

### 3. Update Documentation

Update internal docs to reflect new features and patterns.

### 4. Monitor After Upgrade

- Check startup duration metrics
- Verify health check status
- Review logs for warnings/errors

### 5. Incremental Adoption

Don't rush to adopt all new features at once:

1. Upgrade package version
2. Test existing functionality
3. Adopt new features incrementally
4. Monitor each change

## Troubleshooting Upgrades

### Build Errors

**Error**: `The type 'IIgnitionSignal' is defined in an assembly that is not referenced`

**Solution**: Ensure package is restored:

```bash
dotnet restore
```

### Runtime Errors

**Error**: `MissingMethodException` or `TypeLoadException`

**Solution**: Clean and rebuild:

```bash
dotnet clean
dotnet build
```

### Performance Regression

**Issue**: Startup slower after upgrade

**Diagnosis**:

```csharp
options.SlowHandleLogCount = 10; // Identify slow signals
```

**Solution**: Review new configuration options, adjust timeouts.

## Deprecation Policy

Veggerby.Ignition follows semantic versioning:

- **Major version** (e.g., 2.0.0): Breaking changes
- **Minor version** (e.g., 1.1.0): New features (backward compatible)
- **Patch version** (e.g., 1.0.1): Bug fixes

Deprecated features will:

1. Be marked `[Obsolete]` in minor version
2. Remain functional for at least one major version
3. Be documented in release notes

## Future Roadmap

Potential future features (subject to change):

- **Async initialization hooks**: Pre/post ignition callbacks
- **Custom policy support**: User-defined failure handling
- **Distributed coordination**: Multi-instance startup synchronization
- **Advanced metrics**: Built-in Prometheus/OTEL metrics

Follow the [GitHub repository](https://github.com/veggerby/Veggerby.Ignition) for updates.

## Getting Help with Migrations

- **GitHub Issues**: [Report issues](https://github.com/veggerby/Veggerby.Ignition/issues)
- **Discussions**: [Ask questions](https://github.com/veggerby/Veggerby.Ignition/discussions)
- **Documentation**: Review updated [docs](README.md)

## Related Topics

- [Getting Started](getting-started.md) - Setup for new projects
- [Features](features.md) - Complete feature overview
- [Changelog](../CHANGELOG.md) - Detailed version history
