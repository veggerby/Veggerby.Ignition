using Microsoft.Extensions.Logging;
using MySqlConnector;
using Testcontainers.MariaDb;
using Veggerby.Ignition.MariaDb;

namespace Veggerby.Ignition.MariaDb.Tests;

public class MariaDbIntegrationTests : IAsyncLifetime
{
    private MariaDbContainer? _mariaDbContainer;
    private string? _connectionString;

    public async Task InitializeAsync()
    {
        // Using MariaDbBuilder with default wait strategy
        // The MariaDB health check verifies infrastructure-level readiness
        // Ignition signal then provides application-level readiness (connection pools, query execution)
        _mariaDbContainer = new MariaDbBuilder()
            .WithImage("mariadb:11")
            .Build();

        await _mariaDbContainer.StartAsync();

        _connectionString = _mariaDbContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_mariaDbContainer is not null)
        {
            await _mariaDbContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PingVerification_Succeeds()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.Ping,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SimpleQueryVerification_Succeeds()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.SimpleQuery,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionPoolVerification_Succeeds()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.ConnectionPool,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TableExistsVerification_WithExistingTable_Succeeds()
    {
        // arrange - create a test table
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var command = new MySqlCommand("CREATE TABLE IF NOT EXISTS test_table (id INT PRIMARY KEY)", connection);
            await command.ExecuteNonQueryAsync();
        }

        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.TableExists,
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyTables.Add("test_table");

        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TableExistsVerification_WithMissingTable_FailsWhenConfigured()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.TableExists,
            FailOnMissingTables = true,
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyTables.Add("non_existent_table");

        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TableExistsVerification_WithMissingTable_SucceedsWhenNotRequired()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.TableExists,
            FailOnMissingTables = false,
            Timeout = TimeSpan.FromSeconds(30)
        };
        options.VerifyTables.Add("non_existent_table");

        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CustomQueryVerification_Succeeds()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            TestQuery = "SELECT @@version",
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CustomQueryVerification_WithMinimumRows_Succeeds()
    {
        // arrange - create a test table with data
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var createCommand = new MySqlCommand("CREATE TABLE IF NOT EXISTS test_data (id INT PRIMARY KEY, value VARCHAR(50))", connection);
            await createCommand.ExecuteNonQueryAsync();

            using var insertCommand = new MySqlCommand("INSERT IGNORE INTO test_data (id, value) VALUES (1, 'test1'), (2, 'test2'), (3, 'test3')", connection);
            await insertCommand.ExecuteNonQueryAsync();
        }

        var options = new MariaDbReadinessOptions
        {
            TestQuery = "SELECT * FROM test_data",
            ExpectedMinimumRows = 2,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CustomQueryVerification_WithInsufficientRows_Fails()
    {
        // arrange - create a test table with minimal data
        using (var connection = new MySqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using var createCommand = new MySqlCommand("CREATE TABLE IF NOT EXISTS test_minimal (id INT PRIMARY KEY)", connection);
            await createCommand.ExecuteNonQueryAsync();

            using var insertCommand = new MySqlCommand("INSERT IGNORE INTO test_minimal (id) VALUES (1)", connection);
            await insertCommand.ExecuteNonQueryAsync();
        }

        var options = new MariaDbReadinessOptions
        {
            TestQuery = "SELECT * FROM test_minimal",
            ExpectedMinimumRows = 5,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.Ping,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(_connectionString!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConnectionFactory_Constructor_Succeeds()
    {
        // arrange - test connection factory constructor
        var options = new MariaDbReadinessOptions
        {
            VerificationStrategy = MariaDbVerificationStrategy.SimpleQuery,
            Timeout = TimeSpan.FromSeconds(30)
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal(() => new MySqlConnection(_connectionString!), options, logger);

        // act & assert
        await signal.WaitAsync();
    }
}
