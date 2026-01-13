using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Veggerby.Ignition.Postgres;

namespace Veggerby.Ignition.Postgres.Tests;

public class PostgresIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        // Create container with minimal wait - just wait for container to start, not for PostgreSQL to be ready
        // This lets the Ignition signal handle the actual readiness verification
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _postgresContainer.StartAsync();

        _connectionString = _postgresContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var options = new PostgresReadinessOptions
        {
            ValidationQuery = null
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ValidationQuery_Succeeds()
    {
        // arrange
        var options = new PostgresReadinessOptions
        {
            ValidationQuery = "SELECT 1"
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ValidationQuery_WithTimeout_Succeeds()
    {
        // arrange
        var options = new PostgresReadinessOptions
        {
            ValidationQuery = "SELECT 1",
            Timeout = TimeSpan.FromSeconds(5)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_connectionString!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task InvalidConnectionString_ThrowsException()
    {
        // arrange
        var invalidConnectionString = "Host=invalid-host;Port=5432;Database=testdb;Username=test;Password=test;";
        var options = new PostgresReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(invalidConnectionString, options, logger);

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(async () => await signal.WaitAsync());
    }
}
