# GitHub Actions Workflows

This directory contains the CI/CD workflows for Veggerby.Ignition.

## Workflows

### 1. Fast Build & Test (`ci-fast.yml`)

**Triggers:**
- All pull requests (draft and ready)
- Manual workflow dispatch

**Purpose:**
- Quick feedback for developers
- Runs on every PR update regardless of draft status
- Excludes integration tests for speed

**What it does:**
- Restores dependencies
- Builds solution with FastBuild mode (analyzers disabled)
- Runs **unit tests only** (`--filter "Category!=Integration"`)

**Duration:** ~1-2 minutes

---

### 2. Integration Tests (`ci-integration.yml`)

**Triggers:**
- Pull requests when marked ready for review (not draft)
- Push to main branch
- Manual workflow dispatch

**Purpose:**
- Verify integration with real infrastructure (Docker containers)
- Required check before merge
- Skipped for draft PRs to save CI resources

**What it does:**
- Restores dependencies
- Builds solution in Release mode
- Runs **integration tests only** (`--filter "Category=Integration"`)
- Uses Testcontainers to spin up real Redis, PostgreSQL, RabbitMQ, MongoDB, SQL Server containers
- Uploads test results as artifacts

**Duration:** ~5-10 minutes (depends on container startup times)

**Environment:**
- `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` set for Docker networking

**Required for Merge:** ✅ Yes - this workflow must pass before PRs can be merged

---

### 3. Build, Test & Package (`ci-release.yml`)

**Triggers:**
- Push to main branch
- Tags matching `v*.*.*`
- Pull requests with `full-pack` label
- Manual workflow dispatch

**Purpose:**
- Full release build with code coverage
- Package creation for NuGet
- Deployment to package registries

**What it does:**
- Builds solution in Release mode
- Runs **unit tests with code coverage** (excludes integration tests)
- Uploads coverage to Codecov
- Packages NuGet packages using GitVersion
- Optionally publishes to NuGet.org and GitHub Packages

**Duration:** ~3-5 minutes

---

## CI Strategy

### For Pull Requests

1. **All PRs (including drafts):**
   - ✅ Fast build & unit tests run immediately
   - ⏭️ Integration tests skipped
   - Goal: Quick feedback for rapid iteration

2. **Ready for Review (non-draft):**
   - ✅ Fast build & unit tests continue to run
   - ✅ Integration tests now run (required check)
   - Goal: Ensure production readiness before merge

3. **With `full-pack` label:**
   - ✅ Fast build & unit tests
   - ✅ Integration tests
   - ✅ Release build with packaging
   - Goal: Test package creation before merge (optional)

### For Main Branch

- All workflows run on push to main
- Full release build with packaging
- Code coverage uploaded

### For Tags

- Release workflow creates and publishes NuGet packages
- Packages pushed to NuGet.org and GitHub Packages (if configured)

---

## Local Testing

### Run Unit Tests Only (Fast)

```bash
dotnet test Veggerby.Ignition.sln --filter "Category!=Integration"
```

### Run Integration Tests Only

```bash
# Requires Docker running
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal
dotnet test Veggerby.Ignition.sln --filter "Category=Integration"
```

### Run All Tests

```bash
# Requires Docker running
export TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal
dotnet test Veggerby.Ignition.sln
```

---

## Workflow Maintenance

When modifying workflows:

1. Keep integration tests separate from unit tests (different execution times)
2. Ensure draft PRs skip expensive integration tests
3. Integration tests must be required for merge protection
4. Unit tests should run on all PRs for immediate feedback
5. Use `TESTCONTAINERS_HOST_OVERRIDE` for Docker-in-Docker compatibility
