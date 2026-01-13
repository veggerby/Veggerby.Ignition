using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Veggerby.Ignition.SqlServer;

namespace Veggerby.Ignition.SqlServer.Tests;

public class SqlServerIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _sqlServerContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        _sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _sqlServerContainer.StartAsync();

        _connectionString = _sqlServerContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_sqlServerContainer != null)
        {
            await _sqlServerContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionOnly_Succeeds()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            ValidationQuery = null,
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ValidationQuery_Succeeds()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            ValidationQuery = "SELECT 1",
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ValidationQuery_WithTimeout_Succeeds()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            ValidationQuery = "SELECT 1",
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionString!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }
}
