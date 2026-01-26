using Enyim.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Memcached.Tests;

public class MemcachedReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        var options = new MemcachedReadinessOptions();

        // act
        var factory = new MemcachedReadinessSignalFactory(options);

        // assert
        factory.Name.Should().Be("memcached-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MemcachedReadinessSignalFactory(null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        var options = new MemcachedReadinessOptions { Timeout = timeout };

        // act
        var factory = new MemcachedReadinessSignalFactory(options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new MemcachedReadinessOptions { Stage = 2 };

        // act
        var factory = new MemcachedReadinessSignalFactory(options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsMemcachedReadiness()
    {
        // arrange
        var options = new MemcachedReadinessOptions();

        // act
        var factory = new MemcachedReadinessSignalFactory(options);

        // assert
        factory.Name.Should().Be("memcached-readiness");
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsMemcachedReadinessSignal()
    {
        // arrange
        var options = new MemcachedReadinessOptions();
        var factory = new MemcachedReadinessSignalFactory(options);

        var memcachedClient = Substitute.For<IMemcachedClient>();
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();

        var services = new ServiceCollection();
        services.AddSingleton(memcachedClient);
        services.AddSingleton(logger);
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<MemcachedReadinessSignal>();
        signal.Name.Should().Be("memcached-readiness");
    }
}
