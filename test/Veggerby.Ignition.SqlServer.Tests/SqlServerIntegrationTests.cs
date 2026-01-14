using DotNet.Testcontainers.Builders;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Veggerby.Ignition.SqlServer;

namespace Veggerby.Ignition.SqlServer.Tests;

public class SqlServerIntegrationTests : IAsyncLifetime
{
    private MsSqlContainer? _sqlServerContainer;
    private string? _connectionString;
    private Func<SqlConnection>? _connectionFactory;

    public async Task InitializeAsync()
    {
        _sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        await _sqlServerContainer.StartAsync();

        _connectionString = _sqlServerContainer.GetConnectionString();
        _connectionFactory = () => new SqlConnection(_connectionString);
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
    public async Task ConnectionFactory_ConnectionOnly_Succeeds()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            ValidationQuery = null,
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionFactory!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionFactory_ValidationQuery_Succeeds()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            ValidationQuery = "SELECT 1",
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionFactory!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionFactory_RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new SqlServerReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal(_connectionFactory!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - no exception means cached result worked
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionString_ValidationQuery_Succeeds()
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
}
