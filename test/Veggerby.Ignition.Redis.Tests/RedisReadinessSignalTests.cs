using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Veggerby.Ignition.Redis;

namespace Veggerby.Ignition.Redis.Tests;

public class RedisReadinessSignalTests
{
    [Fact]
    public void Constructor_NullConnectionMultiplexer_ThrowsArgumentNullException()
    {
        // arrange
        var options = new RedisReadinessOptions();
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RedisReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RedisReadinessSignal(multiplexer, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var options = new RedisReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new RedisReadinessSignal(multiplexer, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var options = new RedisReadinessOptions();
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act & assert
        signal.Name.Should().Be("redis-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var options = new RedisReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        var options = new RedisReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionNotConnected_ThrowsException()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.IsConnected.Returns(false);
        var options = new RedisReadinessOptions();
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("not connected");
    }

    [Fact]
    public async Task WaitAsync_ConnectionOnly_SucceedsWhenConnected()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.IsConnected.Returns(true);
        multiplexer.GetEndPoints(false).Returns(Array.Empty<System.Net.EndPoint>());

        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.ConnectionOnly
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act
        await signal.WaitAsync();

        // assert - no exception thrown
    }

    [Fact]
    public async Task WaitAsync_Ping_CallsPingCommand()
    {
        // arrange
        var db = Substitute.For<IDatabase>();
        db.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(1));

        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.IsConnected.Returns(true);
        multiplexer.GetEndPoints(false).Returns(Array.Empty<System.Net.EndPoint>());
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.Ping
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await db.Received(1).PingAsync(Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task WaitAsync_PingAndTestKey_CallsPingAndKeyOperations()
    {
        // arrange
        var db = Substitute.For<IDatabase>();
        db.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(1));
        db.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>()).Returns(true);
        db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(callInfo => callInfo.ArgAt<RedisKey>(0).ToString().Contains("ignition") ? "test-value" : RedisValue.Null);
        db.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(true);

        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.IsConnected.Returns(true);
        multiplexer.GetEndPoints(false).Returns(Array.Empty<System.Net.EndPoint>());
        multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        var options = new RedisReadinessOptions
        {
            VerificationStrategy = RedisVerificationStrategy.PingAndTestKey,
            TestKeyPrefix = "ignition:test:"
        };
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await db.Received(1).PingAsync(Arg.Any<CommandFlags>());
        await db.Received(1).StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<bool>(), Arg.Any<When>(), Arg.Any<CommandFlags>());
        await db.Received(1).StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
        await db.Received(1).KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task WaitAsync_Idempotent_ExecutesOnlyOnce()
    {
        // arrange
        var multiplexer = Substitute.For<IConnectionMultiplexer>();
        multiplexer.IsConnected.Returns(true);
        multiplexer.GetEndPoints(false).Returns(Array.Empty<System.Net.EndPoint>());

        var options = new RedisReadinessOptions();
        var logger = Substitute.For<ILogger<RedisReadinessSignal>>();
        var signal = new RedisReadinessSignal(multiplexer, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert
        var _ = multiplexer.Received(1).IsConnected;
    }
}
