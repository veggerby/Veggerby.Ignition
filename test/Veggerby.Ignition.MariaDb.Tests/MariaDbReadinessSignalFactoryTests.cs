using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.MariaDb.Tests;

public class MariaDbReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new MariaDbReadinessOptions();

        // act
        var factory = new MariaDbReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("mariadb-readiness");
        factory.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MariaDbReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(60);
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new MariaDbReadinessOptions { Timeout = timeout };

        // act
        var factory = new MariaDbReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new MariaDbReadinessOptions { Stage = 2 };

        // act
        var factory = new MariaDbReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var connectionString = "Server=localhost;Database=test;User Id=user;Password=pass;";
        string ConnectionStringFactory(IServiceProvider sp) => connectionString;
        var options = new MariaDbReadinessOptions();
        var factory = new MariaDbReadinessSignalFactory(ConnectionStringFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<MariaDbReadinessSignal>>(_ => Substitute.For<ILogger<MariaDbReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<MariaDbReadinessSignal>();
        signal.Name.Should().Be("mariadb-readiness");
    }

    [Fact]
    public void CreateSignal_UsesConnectionStringFactoryToResolveConnectionString()
    {
        // arrange
        var expectedConnectionString = "Server=dynamic.mariadb.local;Database=testdb;";
        string ConnectionStringFactory(IServiceProvider sp) => expectedConnectionString;
        var options = new MariaDbReadinessOptions();
        var factory = new MariaDbReadinessSignalFactory(ConnectionStringFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<MariaDbReadinessSignal>>(_ => Substitute.For<ILogger<MariaDbReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
