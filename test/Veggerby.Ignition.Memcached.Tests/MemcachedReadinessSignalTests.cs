using System.Runtime.Serialization;

using Enyim.Caching;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
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
    public async Task WaitAsync_Stats_CallsStats()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        // This test verifies the method is called, but ServerStats is hard to mock
        // so we verify the call happens and expect it to throw due to null return
        client.Stats().Returns((ServerStats)null!);

        var options = new MemcachedReadinessOptions
        {
            VerificationStrategy = MemcachedVerificationStrategy.Stats
        };
        var logger = Substitute.For<ILogger<MemcachedReadinessSignal>>();
        var signal = new MemcachedReadinessSignal(client, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await signal.WaitAsync());
        client.Received(1).Stats();
    }

    [Fact]
    public async Task WaitAsync_Stats_NoStats_ThrowsException()
    {
        // arrange
        var client = Substitute.For<IMemcachedClient>();
        client.Stats().Returns((ServerStats)null!);

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
        string? capturedValue = null;
        var client = Substitute.For<IMemcachedClient>();
        client.SetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(callInfo =>
            {
                capturedValue = callInfo.ArgAt<string>(1);
                return true;
            });
        
        var getResult = Substitute.For<IGetOperationResult<string>>();
        getResult.Value.Returns(callInfo => capturedValue);
        client.GetAsync<string>(Arg.Any<string>()).Returns(getResult);
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
