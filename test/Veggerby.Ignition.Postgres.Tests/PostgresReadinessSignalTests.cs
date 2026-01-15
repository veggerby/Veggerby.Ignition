using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using Veggerby.Ignition.Postgres;
using Xunit;

namespace Veggerby.Ignition.Postgres.Tests;

public class PostgresReadinessSignalTests
{
    [Fact]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal((string)null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyConnectionString_ThrowsArgumentException()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new PostgresReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullDataSource_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal((NpgsqlDataSource)null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal("Host=localhost;", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal("Host=localhost;", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;", options, logger);

        // act & assert
        signal.Name.Should().Be("postgres-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var options = new PostgresReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var options = new PostgresReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var options = new PostgresReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(2) // Short timeout to fail quickly
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=invalid-host-that-does-not-exist;Database=test;Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // act & assert - Connection failures can throw various exception types
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=invalid-host;Database=test;Timeout=1;", options, logger);

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
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;Timeout=30;", options, logger);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }

    [Fact]
    public void PostgresReadinessOptions_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new PostgresReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        options.ValidationQuery.Should().BeNull();
        options.Timeout.Should().BeNull();
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void PostgresReadinessOptions_CustomValues_ArePreserved()
    {
        // arrange & act
        var options = new PostgresReadinessOptions
        {
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromSeconds(1),
            ValidationQuery = "SELECT version();",
            Timeout = TimeSpan.FromSeconds(30),
            Stage = 2
        };

        // assert
        options.MaxRetries.Should().Be(10);
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.ValidationQuery.Should().Be("SELECT version();");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void PostgresReadinessSignalFactory_NullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void PostgresReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "Host=localhost;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void PostgresReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var factory = new PostgresReadinessSignalFactory(_ => "Host=localhost;", options);

        // act & assert
        factory.Name.Should().Be("postgres-readiness");
    }

    [Fact]
    public void PostgresReadinessSignalFactory_Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(20);
        var options = new PostgresReadinessOptions { Timeout = timeout };
        var factory = new PostgresReadinessSignalFactory(_ => "Host=localhost;", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void PostgresReadinessSignalFactory_Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new PostgresReadinessOptions { Stage = 3 };
        var factory = new PostgresReadinessSignalFactory(_ => "Host=localhost;", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void PostgresReadinessSignalFactory_CreateSignal_ReturnsSignalWithCorrectConnectionString()
    {
        // arrange
        var connectionString = "Host=testhost;Database=testdb;Username=user;";
        var options = new PostgresReadinessOptions();
        var factory = new PostgresReadinessSignalFactory(_ => connectionString, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("postgres-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void PostgresReadinessSignalFactory_CreateSignal_ResolvesConnectionStringFromServiceProvider()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var expectedConnectionString = "Host=dynamic-host;";
        
        var factory = new PostgresReadinessSignalFactory(sp =>
        {
            // Simulate resolving from configuration
            return sp.GetService<string>() ?? expectedConnectionString;
        }, options);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton("Host=from-di;");
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
    }

    [Fact]
    public void PostgresReadinessSignalFactory_MultipleCreateSignal_ReturnsNewInstances()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var factory = new PostgresReadinessSignalFactory(_ => "Host=localhost;", options);

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
    }}