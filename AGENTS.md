# AGENTS.md

## Project Overview

Veggerby.Ignition is a lightweight .NET library for coordinating application startup readiness. Applications register asynchronous readiness operations (`IIgnitionSignal`), and the ignition coordinator (`IIgnitionCoordinator`) awaits them collectively with configurable timeouts, execution policies, Activity tracing, health check reporting, and structured diagnostics.

**Key Technologies:**

- .NET 10+ (C#)
- Microsoft.Extensions.* (Logging, Options, DependencyInjection, Diagnostics.HealthChecks)
- xUnit, AwesomeAssertions, NSubstitute for testing
- BenchmarkDotNet for performance benchmarks

**Architecture:**

- Core library: `src/Veggerby.Ignition`
- Integration packages: `src/Veggerby.Ignition.*` (Marten, MassTransit, MongoDb, Postgres, RabbitMq, SqlServer, Grpc, Http, Aws, Azure, Orleans, Redis, Memcached)
- Tests: `test/` (unit and integration tests for each package)
- Documentation: `docs/` with comprehensive guides
- Samples: `samples/` with runnable examples (Simple, Advanced, DependencyGraph, Bundles, WebApi, Worker, Messaging, Cloud, TimeoutStrategies, TimelineExport, Replay, SimpleMode)
- Benchmarks: `benchmarks/` for performance profiling

## Setup Commands

**Prerequisites:**

- .NET 10 SDK or later

**Initial Setup:**

```bash
# Clone and navigate to repository
cd /workspaces/Veggerby.Ignition

# Restore all dependencies
dotnet restore Veggerby.Ignition.sln

# Build entire solution
dotnet build Veggerby.Ignition.sln
```

**Fast Development Build:**

```bash
# Build with analyzers disabled for faster iteration
dotnet build Veggerby.Ignition.sln -p:FastBuild=true --configuration Debug --no-restore
```

**Run Samples:**

```bash
# Simple example
dotnet run --project samples/Simple/Simple.csproj

# Advanced features
dotnet run --project samples/Advanced/Advanced.csproj

# Dependency graph coordination (DAG)
dotnet run --project samples/DependencyGraph/DependencyGraph.csproj

# Bundles (reusable signal packages)
dotnet run --project samples/Bundles/Bundles.csproj

# Web API with health checks
dotnet run --project samples/WebApi/WebApi.csproj

# Worker service integration
dotnet run --project samples/Worker/Worker.csproj

# Message bus integration
dotnet run --project samples/Messaging/Messaging.csproj

# Cloud provider integration
dotnet run --project samples/Cloud/Cloud.csproj

# Timeout strategies
dotnet run --project samples/TimeoutStrategies/TimeoutStrategies.csproj

# Timeline export/replay
dotnet run --project samples/TimelineExport/TimelineExport.csproj
dotnet run --project samples/Replay/Replay.csproj
```

**Run Benchmarks:**

```bash
dotnet run --project benchmarks/Veggerby.Ignition.Benchmarks/Veggerby.Ignition.Benchmarks.csproj -c Release
```

## Development Workflow

**Monorepo Structure:**

- Use `dotnet sln list` to see all projects in the solution
- Core library changes affect all integration packages
- Each integration package has corresponding test project
- Samples demonstrate real-world usage patterns

**Working on a Specific Package:**

```bash
# Build only core library
dotnet build src/Veggerby.Ignition/Veggerby.Ignition.csproj

# Build a specific integration package
dotnet build src/Veggerby.Ignition.MassTransit/Veggerby.Ignition.MassTransit.csproj

# Test a specific package
dotnet test test/Veggerby.Ignition.MassTransit.Tests/Veggerby.Ignition.MassTransit.Tests.csproj
```

**Build Configuration:**

- `Debug` for development (default)
- `Release` for production builds and benchmarks
- `FastBuild=true` property disables analyzers for faster iteration

## Testing Instructions

**Run All Tests:**

```bash
# Full test suite across all test projects
dotnet test Veggerby.Ignition.sln

# With coverage (if configured)
dotnet test Veggerby.Ignition.sln --collect:"XPlat Code Coverage"
```

**Run Specific Test Projects:**

```bash
# Core library tests only
dotnet test test/Veggerby.Ignition.Tests/Veggerby.Ignition.Tests.csproj

# Integration package tests
dotnet test test/Veggerby.Ignition.MassTransit.Tests/Veggerby.Ignition.MassTransit.Tests.csproj
dotnet test test/Veggerby.Ignition.RabbitMq.Tests/Veggerby.Ignition.RabbitMq.Tests.csproj
```

**Run Tests Without Rebuild:**

```bash
dotnet test Veggerby.Ignition.sln --no-build
```

**Run Specific Test:**

```bash
# Filter by test name
dotnet test --filter "DisplayName~FailFast_Sequential_StopsOnFirstFailure"

# Filter by category or trait (if using traits)
dotnet test --filter "Category=Integration"
```

**Testing Framework Details:**

- **xUnit** for test execution
- **AwesomeAssertions** for fluent assertions (`.Should()` syntax)
- **NSubstitute** for mocking dependencies
- **Testcontainers** for integration tests requiring real infrastructure (Redis, PostgreSQL, RabbitMQ, etc.)
- Test files use `// arrange`, `// act`, `// assert` comment structure
- Tests must be deterministic (no flaky timing-dependent tests)
- Use `TaskCompletionSource` for controlled async test scenarios

**Integration Tests with Testcontainers:**

Integration tests for packages requiring infrastructure (Redis, PostgreSQL, RabbitMQ, MongoDB, etc.) use **Testcontainers** to spin up real Docker containers during test execution.

**Prerequisites:**

```bash
# Docker must be running
docker --version

# Docker daemon must be accessible
docker ps
```

**Running Integration Tests:**

Integration tests are tagged with `[Trait("Category", "Integration")]` and are **excluded by default**.

**Dev Container Environment:**

When running integration tests in a dev container environment (like VS Code dev containers), you must set the `TESTCONTAINERS_HOST_OVERRIDE` environment variable so that Testcontainers can properly connect to containers started within Docker-in-Docker:

```bash
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal
```

**Running Tests:**

```bash
# Run ONLY integration tests (requires Docker)
# In dev container:
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal && dotnet test Veggerby.Ignition.sln --filter "Category=Integration"

# On host machine (no environment variable needed):
dotnet test Veggerby.Ignition.sln --filter "Category=Integration"

# Run integration tests for specific package (dev container):
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal && dotnet test test/Veggerby.Ignition.Redis.Tests/Veggerby.Ignition.Redis.Tests.csproj --filter "Category=Integration"
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal && dotnet test test/Veggerby.Ignition.Postgres.Tests/Veggerby.Ignition.Postgres.Tests.csproj --filter "Category=Integration"
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal && dotnet test test/Veggerby.Ignition.RabbitMq.Tests/Veggerby.Ignition.RabbitMq.Tests.csproj --filter "Category=Integration"

# Run unit tests only (default - excludes integration tests)
dotnet test Veggerby.Ignition.sln --filter "Category!=Integration"

# Run ALL tests (both unit and integration tests - requires Docker)
# In dev container:
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal && dotnet test Veggerby.Ignition.sln

# On host machine:
dotnet test Veggerby.Ignition.sln
```

**Testcontainers Pattern:**

```csharp
public class RedisIntegrationTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task RedisSignal_ConnectsToRealRedis()
    {
        // arrange
        var connectionString = _redis.GetConnectionString();
        var signal = new RedisReadinessSignal(connectionString, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        // Verify connection is established
    }
}
```

**Supported Testcontainers:**

- **Redis**: `Testcontainers.Redis` - Redis cache/data store tests
- **PostgreSQL**: `Testcontainers.PostgreSql` - PostgreSQL database tests
- **RabbitMQ**: `Testcontainers.RabbitMq` - RabbitMQ message broker tests
- **MongoDB**: `Testcontainers.MongoDb` - MongoDB document store tests
- **SQL Server**: `Testcontainers.MsSql` - SQL Server database tests

**Container Lifecycle:**

- Containers start in `InitializeAsync()` (before each test class)
- Containers stop in `DisposeAsync()` (after each test class)
- Each test class gets a fresh container instance
- Containers are automatically cleaned up after tests complete

**Performance Considerations:**

- Container startup adds ~2-10 seconds per test class
- Use `[Collection]` attribute to share containers across multiple test classes when appropriate
- Consider running integration tests separately from unit tests in CI/CD
- Local development: keep Docker Desktop running for faster test execution

**Testing Requirements:**

- Every bug fix must include a failing test first
- Every feature change must include tests covering success, failure, timeout, and cancellation paths
- Cover edge cases: zero signals, single signal, mixed success/failure/timeout states
- Test idempotency (multiple coordinator waits return cached results)
- Preserve existing test coverage when refactoring
- **Integration tests**: Use Testcontainers for real infrastructure; avoid mocking external services when testing integration packages

## Code Style Guidelines

**File Organization:**

- File-scoped namespaces preferred (C# 10+ feature)
- Block namespaces with Allman braces allowed (legacy style in existing code)
- One type per file, file name matches type name

**Formatting (Enforced by .editorconfig):**

- **Indentation:** 4 spaces, never tabs
- **Braces:** Allman style for types, methods, control blocks
- **Always use braces** for if/else/for/while even if single statement
- **No trailing whitespace**
- **Final newline** at end of file
- **Single blank line** between logically distinct sections

**Using Directives:**

- `using System.*` namespaces first
- Blank line
- Other framework/third-party namespaces
- Keep stable ordering, avoid unnecessary reordering

**Naming Conventions:**

- Types/Namespaces: `PascalCase`
- Interfaces: `I` + `PascalCase` (e.g., `IIgnitionCoordinator`)
- Public members: `PascalCase`
- Private fields: `_camelCase`
- Constants: `PascalCase`
- Methods: `PascalCase` with async methods ending in `Async`

**Language Guidelines:**

- Expression-bodied members allowed for simple, immutable factory helpers
- Avoid LINQ in hot coordinator paths; prefer explicit loops for performance
- No blocking waits (`.Result`, `.Wait()`) — async/await throughout
- Use `Stopwatch` for timing, never `DateTime.Now` for elapsed measurement
- Early-return guard clauses with blank line after guard block

**XML Documentation:**

- **Required** on all public types and members
- Document semantics, especially timeout/cancellation behavior
- Explain policy interactions and execution mode effects
- Include `<remarks>` for complex invariants

**Performance Considerations:**

- Minimize allocations in coordinator hot paths
- Pre-size collections when final size is known
- Avoid excessive task wrappers
- Justify any LINQ in tight loops (prefer explicit loops when uncertain)
- Use `ValueTask<T>` for frequently-called sync-path methods when appropriate

**Determinism & Invariants:**

- Library must not introduce randomness
- Same signal tasks + same options => identical classification outcomes
- Timing only influences measured durations, not classification logic
- No hidden global mutable state

## Build and Deployment

**Build Commands:**

```bash
# Debug build
dotnet build Veggerby.Ignition.sln --configuration Debug

# Release build
dotnet build Veggerby.Ignition.sln --configuration Release

# Restore, build, test in sequence
dotnet restore && dotnet build && dotnet test
```

**Build Properties:**

- `FastBuild=true`: Disables analyzers for faster iteration
- `Configuration`: Debug or Release
- Build settings centralized in `build/global.props` and `Directory.*.props`

**Package Creation:**

```bash
# Create NuGet packages (Release configuration required)
dotnet pack Veggerby.Ignition.sln --configuration Release
```

**CI/CD Pipeline:**

- `.github/workflows/ci-fast.yml`: Fast build and unit tests on all pull requests (draft or ready)
- `.github/workflows/ci-integration.yml`: Integration tests on non-draft pull requests (required for merge)
- `.github/workflows/ci-release.yml`: Full release pipeline with packaging

**CI Integration Test Requirements:**

- Integration tests run automatically when PR is marked ready for review
- Integration tests are **required** to pass before merge
- Draft PRs skip integration tests for faster iteration
- Integration tests use Docker containers via Testcontainers
- Environment variable `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` set in CI

**Environment Variables:**

- `DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1`: Skip first-run experience
- `DOTNET_NOLOGO=true`: Suppress .NET logo output
- `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`: Required for Testcontainers in CI/dev containers

## Pull Request Guidelines

**Before Submitting:**

```bash
# 1. Ensure code builds without warnings
dotnet build Veggerby.Ignition.sln

# 2. Run full test suite
dotnet test Veggerby.Ignition.sln

# 3. Verify formatting compliance (enforced by .editorconfig)
# Visual Studio / VS Code will auto-format on save
```

**PR Requirements:**

- Keep PRs small and focused on a single change
- Open an issue before large changes for discussion
- Include tests for all bug fixes and features
- Update XML docs for public API changes
- Update `docs/` and `README.md` for user-visible changes
- Explain "why" in PR description, not just "what"

**Commit Message Format:**

- Imperative mood: "Add feature" not "Added feature"
- Optional prefixes: `feat:`, `fix:`, `chore:`, `docs:`
- Include issue reference when applicable: `(#43)`
- Examples:
  - `feat: Add staged execution mode (#52)`
  - `fix: Resolve timeout classification edge case (#48)`
  - `docs: Update advanced patterns guide`

**Required Checks:**

- All unit tests pass (fast build workflow)
- All integration tests pass when PR is ready for review (not draft)
- No build warnings
- Code follows style guidelines
- Public APIs documented

**CI Workflow:**

1. **Draft PR**: Only fast build and unit tests run (quick feedback)
2. **Ready for Review**: Integration tests automatically run (required for merge)
3. **All checks must pass** before merge is allowed

## Project-Specific Context

### Core Concepts

**Signals (`IIgnitionSignal`):**

- Asynchronous readiness gates representing component initialization
- Executed at most once (idempotent)
- Have optional per-signal timeout
- Return success/failure/timeout outcome

**Coordinator (`IIgnitionCoordinator`):**

- Awaits all registered signals collectively
- Applies global and per-signal timeouts
- Enforces execution policies
- Caches results (subsequent calls return same result)
- Produces `IgnitionResult` with per-signal outcomes and timing

**Execution Policies:**

- `FailFast`: Stop on first failure (sequential mode) or aggregate all failures (parallel)
- `BestEffort`: Continue despite failures, report all outcomes
- `ContinueOnTimeout`: Continue despite timeouts, but fail on exceptions

**Execution Modes:**

- `Parallel`: Execute all signals concurrently (respects `MaxDegreeOfParallelism`)
- `Sequential`: Execute in registration order
- `DependencyAware`: Execute signals based on dependency graph (DAG with topological sort)
- `Staged`: Execute signals in sequential stages/phases with parallel execution within each stage

**Timeout Semantics:**

- **Global timeout:** Soft deadline unless `CancelOnGlobalTimeout = true`
- **Per-signal timeout:** Only affects that signal; optionally cancels via `CancelIndividualOnTimeout`
- Timeout classification deterministic: same tasks + options => same outcomes

### Forbidden Patterns (Will Fail Review)

- Re-running signal initialization multiple times (breaks idempotency)
- Blocking waits (`.Result`, `.Wait()`)
- Hidden static mutable state
- Silently swallowing exceptions
- Random/jitter logic in coordinator
- Unbounded task fan-out ignoring `MaxDegreeOfParallelism`
- Heavy LINQ in hot path loops
- Health check triggering fresh evaluation (must use cached result)
- Reflection-based dynamic signal invocation

### Suitable Tasks for This Repository

✅ **Suitable:**

- Add new `IgnitionPolicy` with tests and docs
- Introduce new execution strategy (e.g., staged batches)
- Add signal adapter factory (e.g., for `ValueTask`, channels)
- Enhance health check output with structured failure details
- Add configuration options for slow signal threshold logging
- Optimize scheduling to reduce allocations
- Create integration packages for new data stores/message buses

❌ **Unsuitable:**

- Generic workflow orchestration
- Built-in retry/backoff frameworks (user implements in signals)
- Adding external reactive/task orchestration libraries
- Embedding telemetry exporters (use external OTEL integration)
- UI/dashboard or runtime management tooling

### Key Files and Their Roles

- `src/Veggerby.Ignition/Core/IIgnitionSignal.cs`: Core signal abstraction
- `src/Veggerby.Ignition/Core/IIgnitionCoordinator.cs`: Coordinator interface
- `src/Veggerby.Ignition/Core/IgnitionCoordinator.cs`: Main coordination logic
- `src/Veggerby.Ignition/Core/IgnitionOptions.cs`: Configuration options
- `src/Veggerby.Ignition/HealthChecks/IgnitionHealthCheck.cs`: Health check integration
- `src/Veggerby.Ignition/Graph/IIgnitionGraph.cs`: Dependency graph abstraction for DAG execution
- `src/Veggerby.Ignition/Graph/IgnitionGraphBuilder.cs`: Builder for dependency graphs
- `src/Veggerby.Ignition/Extensions/IIgnitionBundle.cs`: Bundle abstraction for reusable signal packages
- `src/Veggerby.Ignition/Extensions/IIgnitionTimeoutStrategy.cs`: Pluggable timeout strategy interface
- `src/Veggerby.Ignition/Diagnostics/IgnitionTimeline.cs`: Timeline recording and replay
- `.editorconfig`: Code formatting rules
- `.github/copilot-instructions.md`: Detailed agent instructions (see attachments)

### Testing Patterns

**Example Test Structure:**

```csharp
[Fact]
public async Task FailFast_Sequential_StopsOnFirstFailure()
{
    // arrange
    var failing = new FaultingSignal("db", new InvalidOperationException("boom"));
    var never = new CountingSignal("never-run");
    var coord = CreateCoordinator(new[] { failing, never }, o =>
    {
        o.ExecutionMode = IgnitionExecutionMode.Sequential;
        o.Policy = IgnitionPolicy.FailFast;
        o.GlobalTimeout = TimeSpan.FromSeconds(1);
    });

    // act
    AggregateException? ex = null;
    try { await coord.WaitAllAsync(); } catch (AggregateException a) { ex = a; }

    // assert
    ex!.InnerExceptions.Should().Contain(e => e is InvalidOperationException);
    never.InvocationCount.Should().Be(0);
}
```

**Test Coverage Requirements:**

- Success paths
- Failure paths (exceptions)
- Timeout paths (global and per-signal)
- Cancellation paths
- Idempotency (multiple `WaitAllAsync` calls)
- Edge cases (zero signals, single signal, mixed statuses)
- Policy variations (FailFast, BestEffort, ContinueOnTimeout)
- Execution mode variations (Parallel, Sequential, DependencyAware, Staged)
- Concurrency limiting
- Dependency graph ordering (for DependencyAware mode)
- Bundle registration and configuration

### Documentation Standards

**When to Update Docs:**

- New public API added → Update XML docs + API reference
- New policy/execution mode → Update `docs/policies.md` or `docs/features.md`
- Behavioral change → Update relevant guide in `docs/`
- New integration package → Add integration recipe to `docs/integration-recipes.md`
- Performance improvement → Update `docs/performance.md`

**Documentation Files:**

- `README.md`: Overview and quick start
- `docs/getting-started.md`: Installation and basic usage
- `docs/features.md`: Feature overview
- `docs/policies.md`: Policy semantics
- `docs/timeout-management.md`: Timeout behavior
- `docs/observability.md`: Tracing and diagnostics
- `docs/performance.md`: Performance characteristics
- `docs/cookbook.md`: Common recipes
- `docs/advanced-patterns.md`: Advanced scenarios
- `docs/api-reference.md`: API documentation
- `docs/dependency-aware-execution.md`: DAG execution guide
- `docs/bundles.md`: Bundles guide
- `docs/integration-recipes.md`: Integration package recipes
- `docs/new-features.md`: Feature epics and roadmap

## Debugging and Troubleshooting

**Common Issues:**

1. **Tests failing with timing issues:**
   - Ensure tests use deterministic `TaskCompletionSource` control
   - Avoid tight timeout values that depend on execution speed
   - Use generous global timeouts in tests (1-5 seconds)

2. **Build warnings:**
   - Check `.editorconfig` compliance
   - Ensure XML docs on public APIs
   - Verify no unused usings or variables

3. **Integration test failures:**
   - **Docker not running**: Ensure Docker Desktop/daemon is running (`docker ps` should succeed)
   - **Testcontainers connection errors**: Check Docker socket permissions and network connectivity
   - **Container startup timeout**: Increase timeout or check Docker resource allocation (CPU/memory)
   - **Port conflicts**: Ensure no other services are using the same ports
   - **Image pull failures**: Check network connectivity and Docker Hub access
   - To skip integration tests when Docker unavailable: `dotnet test --filter "Category!=Integration"`

4. **Redis integration tests failing:**
   - Verify Docker is running: `docker ps`
   - Check Redis container starts: Container logs available in test output on failure
   - Ensure sufficient Docker resources allocated

5. **RabbitMQ/PostgreSQL/MongoDB tests failing:**
   - Same Docker requirements as Redis
   - Check container-specific environment variables are set correctly
   - Verify test project has correct Testcontainers package references
   - Review test setup/teardown for proper isolation

**Logging:**

- Library uses `ILogger<T>` for diagnostics
- Slow signal logging (top N durations) configurable
- Activity tracing toggled via `EnableTracing` option
- Health check reports include failure details

**Performance Profiling:**

```bash
# Run benchmarks to measure performance
dotnet run --project benchmarks/Veggerby.Ignition.Benchmarks/Veggerby.Ignition.Benchmarks.csproj -c Release

# Specific benchmark
dotnet run --project benchmarks/Veggerby.Ignition.Benchmarks/Veggerby.Ignition.Benchmarks.csproj -c Release --filter "*CoordinatorOverhead*"
```

## Additional Notes

**Definition of Done:**

A change is complete when ALL of the following hold:

1. `dotnet build` passes with no new warnings
2. `dotnet test` passes including new tests
3. Tests cover success, failure, timeout, cancellation, and edge cases
4. Public APIs have XML docs; README/docs updated if semantics changed
5. Idempotency & classification semantics preserved
6. No forbidden patterns introduced
7. Performance not regressed (with reasoning or benchmark for significant changes)
8. Formatting & coding standards applied to all modified files
9. Health check remains non-blocking and uses cached result

**Dependencies:**

- **Core library**: Zero external dependencies beyond BCL + Microsoft.Extensions.*
- **Unit tests**: xUnit, AwesomeAssertions, NSubstitute only
- **Integration tests**: Add Testcontainers.* packages as needed (Redis, PostgreSql, RabbitMq, MongoDb, MsSql)
- Keep minimal dependency surface in production code

**Version Information:**

- GitVersion.yml controls semantic versioning
- CHANGELOG.md tracks user-visible changes

**License:**

- MIT License (see LICENSE file)
