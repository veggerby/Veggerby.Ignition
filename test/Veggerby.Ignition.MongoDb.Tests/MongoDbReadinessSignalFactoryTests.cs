using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.MongoDb.Tests;

public class MongoDbReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "mongodb://localhost:27017";
        var options = new MongoDbReadinessOptions();

        // act
        var factory = new MongoDbReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("mongodb-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MongoDbReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "mongodb://localhost:27017";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MongoDbReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "mongodb://localhost:27017";
        var options = new MongoDbReadinessOptions { Timeout = timeout };

        // act
        var factory = new MongoDbReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "mongodb://localhost:27017";
        var options = new MongoDbReadinessOptions { Stage = 2 };

        // act
        var factory = new MongoDbReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var connectionString = "mongodb://localhost:27017/testdb";
        string ConnectionStringFactory(IServiceProvider sp) => connectionString;
        var options = new MongoDbReadinessOptions();
        var factory = new MongoDbReadinessSignalFactory(ConnectionStringFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<MongoDbReadinessSignal>>(_ => Substitute.For<ILogger<MongoDbReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<MongoDbReadinessSignal>();
        signal.Name.Should().Be("mongodb-readiness");
    }

    [Fact]
    public void CreateSignal_UsesConnectionStringFactoryToResolveConnectionString()
    {
        // arrange
        var expectedConnectionString = "mongodb://dynamic.mongodb.local:27017/dynamicdb";
        string ConnectionStringFactory(IServiceProvider sp) => expectedConnectionString;
        var options = new MongoDbReadinessOptions();
        var factory = new MongoDbReadinessSignalFactory(ConnectionStringFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<MongoDbReadinessSignal>>(_ => Substitute.For<ILogger<MongoDbReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
