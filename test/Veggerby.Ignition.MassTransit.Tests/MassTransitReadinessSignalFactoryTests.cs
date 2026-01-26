using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.MassTransit.Tests;

public class MassTransitReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        var options = new MassTransitReadinessOptions();

        // act
        var factory = new MassTransitReadinessSignalFactory(options);

        // assert
        factory.Name.Should().Be("masstransit-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MassTransitReadinessSignalFactory(null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        var options = new MassTransitReadinessOptions { Timeout = timeout };

        // act
        var factory = new MassTransitReadinessSignalFactory(options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new MassTransitReadinessOptions { Stage = 2 };

        // act
        var factory = new MassTransitReadinessSignalFactory(options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var options = new MassTransitReadinessOptions();
        var factory = new MassTransitReadinessSignalFactory(options);

        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IBus>());
        services.AddSingleton<ILogger<MassTransitReadinessSignal>>(_ => Substitute.For<ILogger<MassTransitReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<MassTransitReadinessSignal>();
        signal.Name.Should().Be("masstransit-readiness");
    }
}
