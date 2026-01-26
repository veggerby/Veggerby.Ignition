using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Pulsar.Client.Tests;

public class PulsarReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions();

        // act
        var factory = new PulsarReadinessSignalFactory(serviceUrlFactory, options);

        // assert
        factory.Name.Should().Be("pulsar-readiness");
        factory.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullServiceUrlFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new PulsarReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "pulsar://localhost:6650";

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new PulsarReadinessSignalFactory(serviceUrlFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(60);
        Func<IServiceProvider, string> serviceUrlFactory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions { Timeout = timeout };

        // act
        var factory = new PulsarReadinessSignalFactory(serviceUrlFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions { Stage = 2 };

        // act
        var factory = new PulsarReadinessSignalFactory(serviceUrlFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "pulsar://localhost:6650";
        var options = new PulsarReadinessOptions();
        var factory = new PulsarReadinessSignalFactory(serviceUrlFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<PulsarReadinessSignal>>(_ =>
            Substitute.For<ILogger<PulsarReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<PulsarReadinessSignal>();
        signal.Name.Should().Be("pulsar-readiness");
    }

    [Fact]
    public void CreateSignal_UsesServiceUrlFactoryToResolveUrl()
    {
        // arrange
        var expectedUrl = "pulsar://custom.pulsar.local:6650";
        Func<IServiceProvider, string> serviceUrlFactory = _ => expectedUrl;
        var options = new PulsarReadinessOptions();
        var factory = new PulsarReadinessSignalFactory(serviceUrlFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<PulsarReadinessSignal>>(_ =>
            Substitute.For<ILogger<PulsarReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
