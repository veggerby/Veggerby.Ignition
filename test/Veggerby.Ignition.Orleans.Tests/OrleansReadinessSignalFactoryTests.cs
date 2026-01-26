using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;

namespace Veggerby.Ignition.Orleans.Tests;

public class OrleansReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        var options = new OrleansReadinessOptions();

        // act
        var factory = new OrleansReadinessSignalFactory(options);

        // assert
        factory.Name.Should().Be("orleans-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new OrleansReadinessSignalFactory(null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        var options = new OrleansReadinessOptions { Timeout = timeout };

        // act
        var factory = new OrleansReadinessSignalFactory(options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new OrleansReadinessOptions { Stage = 2 };

        // act
        var factory = new OrleansReadinessSignalFactory(options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var options = new OrleansReadinessOptions();
        var factory = new OrleansReadinessSignalFactory(options);

        var services = new ServiceCollection();
        services.AddSingleton<IClusterClient>(_ =>
            Substitute.For<IClusterClient>());
        services.AddSingleton<ILogger<OrleansReadinessSignal>>(_ =>
            Substitute.For<ILogger<OrleansReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<OrleansReadinessSignal>();
        signal.Name.Should().Be("orleans-readiness");
    }
}
