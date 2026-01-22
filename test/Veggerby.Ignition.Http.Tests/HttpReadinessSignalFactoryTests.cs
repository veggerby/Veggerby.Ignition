using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Http.Tests;

public class HttpReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string UrlFactory(IServiceProvider sp) => "http://example.com";
        var options = new HttpReadinessOptions();

        // act
        var factory = new HttpReadinessSignalFactory(UrlFactory, options);

        // assert
        factory.Name.Should().Be("http-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullUrlFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new HttpReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string UrlFactory(IServiceProvider sp) => "http://example.com";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpReadinessSignalFactory(UrlFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string UrlFactory(IServiceProvider sp) => "http://example.com";
        var options = new HttpReadinessOptions { Timeout = timeout };

        // act
        var factory = new HttpReadinessSignalFactory(UrlFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string UrlFactory(IServiceProvider sp) => "http://example.com";
        var options = new HttpReadinessOptions { Stage = 2 };

        // act
        var factory = new HttpReadinessSignalFactory(UrlFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        var url = "http://example.com/health";
        string UrlFactory(IServiceProvider sp) => url;
        var options = new HttpReadinessOptions();
        var factory = new HttpReadinessSignalFactory(UrlFactory, options);

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton<ILogger<HttpReadinessSignal>>(_ => Substitute.For<ILogger<HttpReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<HttpReadinessSignal>();
        signal.Name.Should().Be("http-readiness");
    }

    [Fact]
    public void CreateSignal_UsesUrlFactoryToResolveUrl()
    {
        // arrange
        var expectedUrl = "http://dynamic.example.com/health";
        string UrlFactory(IServiceProvider sp) => expectedUrl;
        var options = new HttpReadinessOptions();
        var factory = new HttpReadinessSignalFactory(UrlFactory, options);

        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddSingleton<ILogger<HttpReadinessSignal>>(_ => Substitute.For<ILogger<HttpReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
