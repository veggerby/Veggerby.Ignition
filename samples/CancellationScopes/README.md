# Cancellation Scopes Sample

**Complexity**: Advanced  
**Type**: Console Application  
**Focus**: Hierarchical cancellation with scoped signals

## Overview

This sample demonstrates hierarchical cancellation scopes using a database cluster scenario where primary database failure should automatically cancel replica initialization signals.

## What It Demonstrates

### Core Concepts

- **Hierarchical Cancellation**: Creating parent-child scope relationships
- **Scope-based Signal Registration**: Using `AddIgnitionSignalWithScope`
- **Cancellation Propagation**: How failure in one signal cancels related signals
- **CancelScopeOnFailure**: Opt-in cancellation trigger behavior

### Database Cluster Scenario

The sample models a database cluster with:
- **Primary Database**: Must connect first; failure is fatal
- **Replica Databases**: Depend on primary; should not initialize if primary fails

This pattern prevents wasted initialization work when critical dependencies fail.

## Prerequisites

- .NET 10.0 SDK or later
- No external services required (uses simulated delays)

## How to Run

```bash
cd samples/CancellationScopes
dotnet run
```

## Expected Output

### Scenario 1: Success Path

All signals succeed - no cancellation occurs:

```
🏗️  Scenario 1: Success - Primary and Replicas Initialize
============================================================
   📡 Connecting to primary database...
   📡 Connecting to replica 1...
   📡 Connecting to replica 2...
   ✅ Primary database connected
   ✅ Replica 1 connected
   ✅ Replica 2 connected

📊 Success Scenario Results:
   Total Duration: 1024ms
   Succeeded: 3
   Failed: 0
   Cancelled: 0

✅ Overall Status: SUCCESS
```

### Scenario 2: Primary Failure with Cancellation

Primary fails, causing replica cancellation:

```
🏗️  Scenario 2: Primary Failure - Replicas Should Cancel
============================================================
   📡 Connecting to primary database...
   📡 Connecting to replica 1...
   📡 Connecting to replica 2...
   ❌ Primary database connection failed!

📊 Primary Failure Scenario Results:
   Total Duration: 559ms
   Succeeded: 0
   Failed: 1
   Cancelled: 2

📋 Signal Details:
   ❌ primary-db:connect: Failed - Primary database unavailable
   🚫 replica-1:connect: Cancelled
   🚫 replica-2:connect: Cancelled

⚠️  Overall Status: PARTIAL SUCCESS / FAILED

📚 Cancellation Scope Behavior:
   • Primary failure triggered scope cancellation
   • Replica signals were cancelled before completion
   • This prevents wasted work on dependent resources
```

## Key Components

### 1. Creating Cancellation Scopes

```csharp
// Root scope for primary database
var primaryScope = new CancellationScope("primary-db");

// Child scope for replicas (inherits primary cancellation)
var replicaScope = primaryScope.CreateChildScope("replicas");
```

**Scope Hierarchy**:
- Parent scope cancellation automatically cancels all child scopes
- Enables structured cancellation trees

### 2. Registering Scoped Signals

```csharp
// Primary signal: Cancel scope on failure
services.AddIgnitionSignalWithScope(
    IgnitionSignal.FromTaskFactory(
        "primary-db:connect",
        async ct => { /* connection logic */ },
        TimeSpan.FromSeconds(10)),
    primaryScope,
    cancelScopeOnFailure: true);  // Key: triggers cancellation

// Replica signal: Participates in scope but doesn't trigger
services.AddIgnitionSignalWithScope(
    IgnitionSignal.FromTaskFactory(
        "replica-1:connect",
        async ct => { /* connection logic */ },
        TimeSpan.FromSeconds(10)),
    replicaScope,
    cancelScopeOnFailure: false);
```

**Parameters**:
- `signal`: The ignition signal to register
- `scope`: The cancellation scope this signal participates in
- `cancelScopeOnFailure`: If `true`, signal failure cancels the entire scope

### 3. Cancellation Status

When a signal is cancelled via scope, its status becomes `IgnitionSignalStatus.Cancelled`:

```csharp
var cancelled = result.Results.Count(r => r.Status == IgnitionSignalStatus.Cancelled);
```

## Use Cases

### 1. Database Clusters

- Primary must initialize before replicas
- Replica initialization wastes time/resources if primary fails

### 2. Multi-Stage Dependencies

- Stage 1 failure should cancel Stage 2 signals
- Example: Container startup → Service initialization

### 3. Bundle Failure Propagation

- Group related signals in a bundle scope
- Any signal failure cancels entire bundle

### 4. Resource Hierarchy

- Parent resource → Child resources
- Parent failure invalidates children

## Comparison: Cancellation Scopes vs Dependency Graph

| Feature | Cancellation Scopes | Dependency Graph |
|---------|---------------------|------------------|
| **Purpose** | Structured cancellation | Execution ordering |
| **When signals run** | Parallel (by default) | Sequential (topological) |
| **Failure behavior** | Can cancel siblings | Skips dependents |
| **Scope** | Hierarchical trees | Directed acyclic graph |
| **Best for** | Preventing waste | Ensuring order |

**Use Both Together**: Dependency graph for ordering + scopes for cancellation.

## Advanced Patterns

### Multi-Level Scopes

```csharp
var clusterScope = new CancellationScope("db-cluster");
var primaryScope = clusterScope.CreateChildScope("primary");
var replicaScope = clusterScope.CreateChildScope("replicas");

// Cancelling cluster scope cancels all children
```

### Selective Cancellation

```csharp
// Critical signal: triggers cancellation
AddIgnitionSignalWithScope(criticalSignal, scope, cancelScopeOnFailure: true);

// Best-effort signal: participates but doesn't trigger
AddIgnitionSignalWithScope(optionalSignal, scope, cancelScopeOnFailure: false);
```

### External Cancellation

```csharp
var scope = new CancellationScope("services");

// Manually cancel scope (e.g., shutdown signal)
scope.Cancel(CancellationReason.ExternalCancellation, "Shutdown requested");
```

## Troubleshooting

### Signals Not Cancelling

**Symptom**: Replica signals complete despite primary failure.

**Cause**: `cancelScopeOnFailure` is `false` on primary signal.

**Fix**: Set `cancelScopeOnFailure: true` on the signal that should trigger cancellation.

### Signals Cancel Unexpectedly

**Symptom**: All signals cancelled without clear reason.

**Cause**: Parent scope cancelled, propagating to children.

**Fix**: Review scope hierarchy; ensure scopes are structured correctly.

### Delayed Cancellation

**Symptom**: Cancelled signals take time to stop.

**Cause**: Signal logic doesn't check `CancellationToken`.

**Fix**: Ensure your signal logic respects the cancellation token:

```csharp
await SomeLongOperation(ct);  // Pass ct to async operations
ct.ThrowIfCancellationRequested();  // Check periodically
```

## Related Samples

- [DependencyGraph](../DependencyGraph/) - Execution ordering with dependencies
- [Bundles](../Bundles/) - Packaging related signals
- [Advanced](../Advanced/) - Policies and execution modes

## Further Reading

- [ICancellationScope API](../../src/Veggerby.Ignition/Core/ICancellationScope.cs)
- [IScopedIgnitionSignal](../../src/Veggerby.Ignition/Extensions/IScopedIgnitionSignal.cs)
- [Main Documentation](../../README.md)
