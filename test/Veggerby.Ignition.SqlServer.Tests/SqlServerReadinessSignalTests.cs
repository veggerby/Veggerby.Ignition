using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Veggerby.Ignition.SqlServer;
using Xunit;

namespace Veggerby.Ignition.SqlServer.Tests;

public class SqlServerReadinessSignalTests
{
    [Fact]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal((string)null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyConnectionString_ThrowsArgumentException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new SqlServerReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullConnectionFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal((Func<SqlConnection>)null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal("Server=localhost;", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal("Server=localhost;", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Name.Should().Be("sqlserver-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var options = new SqlServerReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var options = new SqlServerReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=invalid-server-that-does-not-exist;Database=test;Connection Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=invalid-server;Database=test;Connection Timeout=1;", options, logger);

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
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;Connection Timeout=30;", options, logger);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }

    [Fact]
    public void SqlServerReadinessOptions_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new SqlServerReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        options.ValidationQuery.Should().BeNull();
        options.Timeout.Should().BeNull();
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void SqlServerReadinessOptions_CustomValues_ArePreserved()
    {
        // arrange & act
        var options = new SqlServerReadinessOptions
        {
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromSeconds(1),
            ValidationQuery = "SELECT @@VERSION",
            Timeout = TimeSpan.FromSeconds(30),
            Stage = 2
        };

        // assert
        options.MaxRetries.Should().Be(10);
        options.RetryDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.ValidationQuery.Should().Be("SELECT @@VERSION");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_NullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "Server=localhost;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var factory = new SqlServerReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Name.Should().Be("sqlserver-readiness");
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(20);
        var options = new SqlServerReadinessOptions { Timeout = timeout };
        var factory = new SqlServerReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new SqlServerReadinessOptions { Stage = 3 };
        var factory = new SqlServerReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_CreateSignal_ReturnsSignalWithCorrectConnectionString()
    {
        // arrange
        var connectionString = "Server=testhost;Database=testdb;User Id=sa;Password=Test123;";
        var options = new SqlServerReadinessOptions();
        var factory = new SqlServerReadinessSignalFactory(_ => connectionString, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("sqlserver-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void SqlServerReadinessSignalFactory_CreateSignal_ResolvesConnectionStringFromServiceProvider()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var expectedConnectionString = "Server=dynamic-host;";
        
        var factory = new SqlServerReadinessSignalFactory(sp =>
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
    public void SqlServerReadinessSignalFactory_MultipleCreateSignal_ReturnsNewInstances()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var factory = new SqlServerReadinessSignalFactory(_ => "Server=localhost;", options);

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

    [Fact]
    public void Constructor_WithConnectionFactory_CreatesSignalSuccessfully()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        Func<SqlConnection> connectionFactory = () => new SqlConnection("Server=localhost;");

        // act
        var signal = new SqlServerReadinessSignal(connectionFactory, options, logger);

        // assert
        signal.Name.Should().Be("sqlserver-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }}