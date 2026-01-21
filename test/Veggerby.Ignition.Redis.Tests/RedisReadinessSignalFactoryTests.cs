using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Redis.Tests;

public class RedisReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "localhost:6379";
        var options = new RedisReadinessOptions();

        // act
        var factory = new RedisReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("redis-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new RedisReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RedisReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "localhost:6379";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RedisReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "localhost:6379";
        var options = new RedisReadinessOptions { Timeout = timeout };

        // act
        var factory = new RedisReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "localhost:6379";
        var options = new RedisReadinessOptions { Stage = 2 };

        // act
        var factory = new RedisReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsRedisReadiness()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "localhost:6379";
        var options = new RedisReadinessOptions();

        // act
        var factory = new RedisReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("redis-readiness");
    }
}
