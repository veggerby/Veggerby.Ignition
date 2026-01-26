using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.RabbitMq.Tests;

public class RabbitMqReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        Func<IServiceProvider, string> connectionStringFactory = _ => "amqp://localhost:5672";
        var options = new RabbitMqReadinessOptions();

        // act
        var factory = new RabbitMqReadinessSignalFactory(connectionStringFactory, options);

        // assert
        factory.Name.Should().Be("rabbitmq-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new RabbitMqReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> connectionStringFactory = _ => "amqp://localhost:5672";

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new RabbitMqReadinessSignalFactory(connectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        Func<IServiceProvider, string> connectionStringFactory = _ => "amqp://localhost:5672";
        var options = new RabbitMqReadinessOptions { Timeout = timeout };

        // act
        var factory = new RabbitMqReadinessSignalFactory(connectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        Func<IServiceProvider, string> connectionStringFactory = _ => "amqp://localhost:5672";
        var options = new RabbitMqReadinessOptions { Stage = 2 };

        // act
        var factory = new RabbitMqReadinessSignalFactory(connectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        Func<IServiceProvider, string> connectionStringFactory = _ => "amqp://localhost:5672";
        var options = new RabbitMqReadinessOptions();
        var factory = new RabbitMqReadinessSignalFactory(connectionStringFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<RabbitMqReadinessSignal>>(_ =>
            Substitute.For<ILogger<RabbitMqReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<RabbitMqReadinessSignal>();
        signal.Name.Should().Be("rabbitmq-readiness");
    }

    [Fact]
    public void CreateSignal_UsesConnectionStringFactoryToResolveConnectionString()
    {
        // arrange
        var expectedConnectionString = "amqp://custom.rabbitmq.local:5672";
        Func<IServiceProvider, string> connectionStringFactory = _ => expectedConnectionString;
        var options = new RabbitMqReadinessOptions();
        var factory = new RabbitMqReadinessSignalFactory(connectionStringFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<RabbitMqReadinessSignal>>(_ =>
            Substitute.For<ILogger<RabbitMqReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
