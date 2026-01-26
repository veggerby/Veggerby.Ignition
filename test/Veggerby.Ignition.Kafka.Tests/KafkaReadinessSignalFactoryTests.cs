using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Kafka.Tests;

public class KafkaReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string BootstrapServersFactory(IServiceProvider sp) => "localhost:9092";
        var options = new KafkaReadinessOptions();

        // act
        var factory = new KafkaReadinessSignalFactory(BootstrapServersFactory, options);

        // assert
        factory.Name.Should().Be("kafka-readiness");
        factory.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullBootstrapServersFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string BootstrapServersFactory(IServiceProvider sp) => "localhost:9092";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new KafkaReadinessSignalFactory(BootstrapServersFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(60);
        string BootstrapServersFactory(IServiceProvider sp) => "localhost:9092";
        var options = new KafkaReadinessOptions { Timeout = timeout };

        // act
        var factory = new KafkaReadinessSignalFactory(BootstrapServersFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string BootstrapServersFactory(IServiceProvider sp) => "localhost:9092";
        var options = new KafkaReadinessOptions { Stage = 2 };

        // act
        var factory = new KafkaReadinessSignalFactory(BootstrapServersFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var bootstrapServers = "localhost:9092";
        string BootstrapServersFactory(IServiceProvider sp) => bootstrapServers;
        var options = new KafkaReadinessOptions();
        var factory = new KafkaReadinessSignalFactory(BootstrapServersFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<KafkaReadinessSignal>>(_ => Substitute.For<ILogger<KafkaReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<KafkaReadinessSignal>();
        signal.Name.Should().Be("kafka-readiness");
    }

    [Fact]
    public void CreateSignal_UsesBootstrapServersFactoryToResolveBootstrapServers()
    {
        // arrange
        var expectedBootstrapServers = "dynamic.kafka.local:9092";
        string BootstrapServersFactory(IServiceProvider sp) => expectedBootstrapServers;
        var options = new KafkaReadinessOptions();
        var factory = new KafkaReadinessSignalFactory(BootstrapServersFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<KafkaReadinessSignal>>(_ => Substitute.For<ILogger<KafkaReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
