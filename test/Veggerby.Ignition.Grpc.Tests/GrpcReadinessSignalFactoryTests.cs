using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Grpc.Tests;

public class GrpcReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "http://localhost:5000";
        var options = new GrpcReadinessOptions();

        // act
        var factory = new GrpcReadinessSignalFactory(serviceUrlFactory, options);

        // assert
        factory.Name.Should().Be("grpc-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullServiceUrlFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new GrpcReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new GrpcReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "http://localhost:5000";

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new GrpcReadinessSignalFactory(serviceUrlFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        Func<IServiceProvider, string> serviceUrlFactory = _ => "http://localhost:5000";
        var options = new GrpcReadinessOptions { Timeout = timeout };

        // act
        var factory = new GrpcReadinessSignalFactory(serviceUrlFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "http://localhost:5000";
        var options = new GrpcReadinessOptions { Stage = 2 };

        // act
        var factory = new GrpcReadinessSignalFactory(serviceUrlFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        Func<IServiceProvider, string> serviceUrlFactory = _ => "http://localhost:5000";
        var options = new GrpcReadinessOptions();
        var factory = new GrpcReadinessSignalFactory(serviceUrlFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<GrpcReadinessSignal>>(_ =>
            Substitute.For<ILogger<GrpcReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<GrpcReadinessSignal>();
        signal.Name.Should().Be("grpc-readiness");
    }

    [Fact]
    public void CreateSignal_UsesServiceUrlFactoryToResolveUrl()
    {
        // arrange
        var expectedUrl = "http://custom.grpc.local:5000";
        Func<IServiceProvider, string> serviceUrlFactory = _ => expectedUrl;
        var options = new GrpcReadinessOptions();
        var factory = new GrpcReadinessSignalFactory(serviceUrlFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<GrpcReadinessSignal>>(_ =>
            Substitute.For<ILogger<GrpcReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
