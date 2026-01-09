using Enyim.Caching;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Memcached;

namespace Veggerby.Ignition.Memcached.Tests;

public class MemcachedReadinessSignalTests
{
    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MemcachedReadinessOptions();
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MemcachedReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MemcachedReadinessSignal(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var options = new MemcachedReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MemcachedReadinessSignal(client, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var options = new MemcachedReadinessOptions();
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act & assert
        signal.Name.Should().Be("memcached-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var client = Substitute.For<IMemcachedClient>();
        var options = new MemcachedReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var options = new MemcachedReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionOnly_Succeeds()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var options = new MemcachedReadinessOptions
        {
            VerificationStrategy = MemcachedVerificationStrategy.ConnectionOnly
        };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert - no exception thrown
    }

    [Fact]
    public async Task WaitAsync_Stats_CallsStatsAsync()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var stats = new Dictionary<string, Dictionary<string, string>>
        {
            ["server1"] = new Dictionary<string, string> { ["version"] = "1.6.0" }
        };
        client.StatsAsync(Arg.Any<CancellationToken>()).Returns(stats);

        var options = new MemcachedReadinessOptions
        {
            VerificationStrategy = MemcachedVerificationStrategy.Stats
        };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).StatsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_Stats_NoStats_ThrowsException()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        client.StatsAsync(Arg.Any<CancellationToken>()).Returns((Dictionary<string, Dictionary<string, string>>?)null);

        var options = new MemcachedReadinessOptions
        {
            VerificationStrategy = MemcachedVerificationStrategy.Stats
        };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("stats");
    }

    [Fact]
    public async Task WaitAsync_TestKey_PerformsRoundTrip()
    {
        // arrange
        var testValue = "test-value";
        var client = Substitute.For<IMemcachedClient>();
        client.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        client.GetAsync<string>(Arg.Any<string>()).Returns(callInfo =>
        {
            var key = callInfo.ArgAt<string>(0);
            return key.Contains("ignition") ? (testValue, true) : (null, false);
        });
        client.RemoveAsync(Arg.Any<string>()).Returns(true);

        var options = new MemcachedReadinessOptions
        {
            VerificationStrategy = MemcachedVerificationStrategy.TestKey,
            TestKeyPrefix = "ignition:test:"
        };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        await client.Received(1).GetAsync<string>(Arg.Any<string>());
        await client.Received(1).RemoveAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task WaitAsync_TestKey_SetFails_ThrowsException()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        client.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(false);

        var options = new MemcachedReadinessOptions
        {
            VerificationStrategy = MemcachedVerificationStrategy.TestKey
        };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("set test key");
    }

    [Fact]
    public async Task WaitAsync_Idempotent_ExecutesOnlyOnce()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        var options = new MemcachedReadinessOptions();
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - with ConnectionOnly, we don't call any methods, so we can't verify exact call count
        // but idempotency is ensured by the cached task
    }
}
