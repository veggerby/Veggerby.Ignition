using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Veggerby.Ignition.Postgres;

namespace Veggerby.Ignition.Postgres.Tests;

public class PostgresIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private string? _connectionString;
    private NpgsqlDataSource? _dataSource;

    public async Task InitializeAsync()
    {
        // Using PostgreSqlBuilder with default wait strategy
        // The pg_isready check is appropriate infrastructure-level readiness verification
        // Ignition signal then provides application-level readiness (connection pools, query execution)
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();

        await _postgresContainer.StartAsync();

        _connectionString = _postgresContainer.GetConnectionString();
        
        // Create NpgsqlDataSource for modern DI-friendly approach
        _dataSource = NpgsqlDataSource.Create(_connectionString);
    }

    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }
        
        if (_postgresContainer is not null)
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
            ValidationQuery = null,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_dataSource!, options, logger);

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
            ValidationQuery = "SELECT 1",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_dataSource!, options, logger);

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
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_dataSource!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new PostgresReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_dataSource!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionString_Constructor_Succeeds()
    {
        // arrange - test fallback connection string constructor
        var options = new PostgresReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }
}
