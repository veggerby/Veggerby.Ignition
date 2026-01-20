using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using NSubstitute;
using Veggerby.Ignition.MariaDb;
using Xunit;

namespace Veggerby.Ignition.MariaDb.Tests;

public class MariaDbReadinessSignalTests
{
    [Fact]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignal((string)null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyConnectionString_ThrowsArgumentException()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new MariaDbReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullConnectionFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignal((Func<MySqlConnection>)null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignal("Server=localhost;", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignal("Server=localhost;", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Name.Should().Be("mariadb-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var options = new MariaDbReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_DefaultOptionsTimeout_ReturnsDefault()
    {
        // arrange
        var options = new MariaDbReadinessOptions(); // Default is 30 seconds
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(2), // Short timeout to fail quickly
            MaxRetries = 1 // Minimal retries for faster test
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal("Server=invalid-host-that-does-not-exist;Database=test;Connection Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // act & assert - Connection failures can throw various exception types
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var options = new MariaDbReadinessOptions
        {
            MaxRetries = 1
        };
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal("Server=invalid-host;Database=test;Connection Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // act - first call should fail and cache the result
        try
        {
            await signal.WaitAsync(cts.Token);
        }
        catch (Exception)
        {
            // Expected
        }

        // act - subsequent calls should return the same cached exception
        var exception1 = await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
        var exception2 = await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));

        // assert - both exceptions should be the same instance (cached)
        exception1.Should().BeSameAs(exception2);
    }

    [Fact]
    public async Task WaitAsync_WithCancellationToken_RespectsCancellation()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var logger = Substitute.For<ILogger<MariaDbReadinessSignal>>();
        var signal = new MariaDbReadinessSignal("Server=localhost;Database=test;Connection Timeout=30;", options, logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }

    [Fact]
    public void MariaDbReadinessOptions_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(8);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(500));
        options.VerificationStrategy.Should().Be(MariaDbVerificationStrategy.Ping);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.Stage.Should().BeNull();
        options.TestQuery.Should().BeNull();
        options.ExpectedMinimumRows.Should().BeNull();
        options.VerifyTables.Should().BeEmpty();
        options.FailOnMissingTables.Should().BeTrue();
        options.Schema.Should().BeNull();
    }

    [Fact]
    public void MariaDbReadinessOptions_CustomValues_ArePreserved()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions
        {
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromSeconds(1),
            VerificationStrategy = MariaDbVerificationStrategy.TableExists,
            Timeout = TimeSpan.FromSeconds(60),
            Stage = 2,
            TestQuery = "SELECT version();",
            ExpectedMinimumRows = 5,
            FailOnMissingTables = false,
            Schema = "myschema"
        };
        options.VerifyTables.AddRange(new[] { "users", "products" });

        // assert
        options.MaxRetries.Should().Be(10);
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.VerificationStrategy.Should().Be(MariaDbVerificationStrategy.TableExists);
        options.Timeout.Should().Be(TimeSpan.FromSeconds(60));
        options.Stage.Should().Be(2);
        options.TestQuery.Should().Be("SELECT version();");
        options.ExpectedMinimumRows.Should().Be(5);
        options.VerifyTables.Should().HaveCount(2);
        options.VerifyTables.Should().Contain("users");
        options.VerifyTables.Should().Contain("products");
        options.FailOnMissingTables.Should().BeFalse();
        options.Schema.Should().Be("myschema");
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_NullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "Server=localhost;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var factory = new MariaDbReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Name.Should().Be("mariadb-readiness");
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(20);
        var options = new MariaDbReadinessOptions { Timeout = timeout };
        var factory = new MariaDbReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new MariaDbReadinessOptions { Stage = 3 };
        var factory = new MariaDbReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_CreateSignal_ReturnsSignalWithCorrectConnectionString()
    {
        // arrange
        var connectionString = "Server=testhost;Database=testdb;User=user;";
        var options = new MariaDbReadinessOptions();
        var factory = new MariaDbReadinessSignalFactory(_ => connectionString, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("mariadb-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_CreateSignal_ResolvesConnectionStringFromServiceProvider()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var expectedConnectionString = "Server=dynamic-host;";

        var factory = new MariaDbReadinessSignalFactory(sp =>
        {
            // Simulate resolving from configuration
            return sp.GetService<string>() ?? expectedConnectionString;
        }, options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton("Server=from-di;");
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
    }

    [Fact]
    public void MariaDbReadinessSignalFactory_MultipleCreateSignal_ReturnsNewInstances()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var factory = new MariaDbReadinessSignalFactory(_ => "Server=localhost;", options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal1 = factory.CreateSignal(sp);
        var signal2 = factory.CreateSignal(sp);

        // assert
        signal1.Should().NotBeNull();
        signal2.Should().NotBeNull();
        signal1.Should().NotBeSameAs(signal2);
    }
}
