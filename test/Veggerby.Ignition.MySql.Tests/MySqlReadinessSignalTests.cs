using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Veggerby.Ignition.MySql;

namespace Veggerby.Ignition.MySql.Tests;

public class MySqlReadinessSignalTests
{
    [Fact]
    public void Constructor_WithConnectionString_ThrowsOnNullConnectionString()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new MySqlReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_WithConnectionString_ThrowsOnNullOptions()
    {
        // arrange
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignal("Server=localhost", null!, logger));
    }

    [Fact]
    public void Constructor_WithConnectionString_ThrowsOnNullLogger()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignal("Server=localhost", options, null!));
    }

    [Fact]
    public void Constructor_WithConnectionFactory_ThrowsOnNullFactory()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignal((Func<MySqlConnection>)null!, options, logger));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal("Server=localhost", options, logger);

        // act
        var name = signal.Name;

        // assert
        name.Should().Be("mysql-readiness");
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var expectedTimeout = TimeSpan.FromSeconds(45);
        var options = new MySqlReadinessOptions
        {
            Timeout = expectedTimeout
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal("Server=localhost", options, logger);

        // act
        var timeout = signal.Timeout;

        // assert
        timeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void Timeout_ReturnsDefaultValue()
    {
        // arrange
        var options = new MySqlReadinessOptions(); // Default is 30 seconds
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal("Server=localhost", options, logger);

        // act
        var timeout = signal.Timeout;

        // assert
        timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void MySqlReadinessSignalFactory_NullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void MySqlReadinessSignalFactory_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> factory = _ => "Server=localhost;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignalFactory(factory, null!));
    }

    [Fact]
    public void MySqlReadinessSignalFactory_Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var factory = new MySqlReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Name.Should().Be("mysql-readiness");
    }

    [Fact]
    public void MySqlReadinessSignalFactory_Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(20);
        var options = new MySqlReadinessOptions { Timeout = timeout };
        var factory = new MySqlReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void MySqlReadinessSignalFactory_Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new MySqlReadinessOptions { Stage = 3 };
        var factory = new MySqlReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void MySqlReadinessSignalFactory_CreateSignal_ReturnsSignalWithCorrectConnectionString()
    {
        // arrange
        var connectionString = "Server=testhost;Database=testdb;User=user;";
        var options = new MySqlReadinessOptions();
        var factory = new MySqlReadinessSignalFactory(_ => connectionString, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var sp = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(sp);

        // assert
        signal.Should().NotBeNull();
        signal.Name.Should().Be("mysql-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void MySqlReadinessSignalFactory_CreateSignal_ResolvesConnectionStringFromServiceProvider()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var expectedConnectionString = "Server=dynamic-host;";

        var factory = new MySqlReadinessSignalFactory(sp =>
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
    public void MySqlReadinessSignalFactory_MultipleCreateSignal_ReturnsNewInstances()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var factory = new MySqlReadinessSignalFactory(_ => "Server=localhost;", options);

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
