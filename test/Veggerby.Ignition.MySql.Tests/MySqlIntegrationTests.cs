using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Testcontainers.MySql;
using Veggerby.Ignition.MySql;

namespace Veggerby.Ignition.MySql.Tests;

public class MySqlIntegrationTests : IAsyncLifetime
{
    private MySqlContainer? _mySqlContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        // Using MySqlBuilder with default wait strategy
        _mySqlContainer = new MySqlBuilder()
            .WithImage("mysql:9.1")
            .Build();

        await _mySqlContainer.StartAsync();

        _connectionString = _mySqlContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_mySqlContainer != null)
        {
            await _mySqlContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Ping_Succeeds()
    {
        // arrange
        var options = new MySqlReadinessOptions
        {
            VerificationStrategy = MySqlVerificationStrategy.Ping,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SimpleQuery_Succeeds()
    {
        // arrange
        var options = new MySqlReadinessOptions
        {
            VerificationStrategy = MySqlVerificationStrategy.SimpleQuery,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionPool_Succeeds()
    {
        // arrange
        var options = new MySqlReadinessOptions
        {
            VerificationStrategy = MySqlVerificationStrategy.ConnectionPool,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TableExists_WhenTableExists_Succeeds()
    {
        // arrange
        // Create a test table first
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var command = new MySqlCommand("CREATE TABLE test_table (id INT PRIMARY KEY)", connection);
        await command.ExecuteNonQueryAsync();

        var options = new MySqlReadinessOptions
        {
            VerificationStrategy = MySqlVerificationStrategy.TableExists,
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyTables.Add("test_table");

        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TableExists_WhenTableMissing_Fails()
    {
        // arrange
        var options = new MySqlReadinessOptions
        {
            VerificationStrategy = MySqlVerificationStrategy.TableExists,
            FailOnMissingTables = true,
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyTables.Add("nonexistent_table");

        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CustomQuery_Succeeds()
    {
        // arrange
        var options = new MySqlReadinessOptions
        {
            TestQuery = "SELECT 42",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CustomQuery_WithExpectedRows_Succeeds()
    {
        // arrange
        // Create a test table with data
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var createCommand = new MySqlCommand("CREATE TABLE row_test (id INT PRIMARY KEY)", connection);
        await createCommand.ExecuteNonQueryAsync();
        using var insertCommand = new MySqlCommand("INSERT INTO row_test VALUES (1), (2), (3)", connection);
        await insertCommand.ExecuteNonQueryAsync();

        var options = new MySqlReadinessOptions
        {
            TestQuery = "SELECT * FROM row_test",
            ExpectedMinimumRows = 2,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CustomQuery_WithInsufficientRows_Fails()
    {
        // arrange
        // Create a test table with data
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        using var createCommand = new MySqlCommand("CREATE TABLE insufficient_rows (id INT PRIMARY KEY)", connection);
        await createCommand.ExecuteNonQueryAsync();
        using var insertCommand = new MySqlCommand("INSERT INTO insufficient_rows VALUES (1)", connection);
        await insertCommand.ExecuteNonQueryAsync();

        var options = new MySqlReadinessOptions
        {
            TestQuery = "SELECT * FROM insufficient_rows",
            ExpectedMinimumRows = 5,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Idempotency_MultipleCalls_ReturnSameCachedTask()
    {
        // arrange
        var options = new MySqlReadinessOptions
        {
            VerificationStrategy = MySqlVerificationStrategy.Ping,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal(_connectionString!, options, logger);

        // act
        var task1 = signal.WaitAsync();
        var task2 = signal.WaitAsync();
        var task3 = signal.WaitAsync();

        await task1;
        await task2;
        await task3;

        // assert
        // All tasks should be the same reference (idempotent)
        object.ReferenceEquals(task1, task2).Should().BeTrue();
        object.ReferenceEquals(task2, task3).Should().BeTrue();
    }
}
