using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.MySql.Tests;

public class MySqlReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;";
        var options = new MySqlReadinessOptions();

        // act
        var factory = new MySqlReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("mysql-readiness");
        factory.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(20);
        var options = new MySqlReadinessOptions { Timeout = timeout };
        var factory = new MySqlReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new MySqlReadinessOptions { Stage = 3 };
        var factory = new MySqlReadinessSignalFactory(_ => "Server=localhost;", options);

        // act & assert
        factory.Stage.Should().Be(3);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var connectionString = "Server=testhost;Database=testdb;User=user;";
        var options = new MySqlReadinessOptions();
        var factory = new MySqlReadinessSignalFactory(_ => connectionString, options);

        var services = new ServiceCollection();
        services.AddLogging();
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<MySqlReadinessSignal>();
        signal.Name.Should().Be("mysql-readiness");
        signal.Timeout.Should().Be(options.Timeout);
    }

    [Fact]
    public void CreateSignal_UsesConnectionStringFactoryToResolveConnectionString()
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
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
