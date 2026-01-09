using System.Threading;
using System.Threading.Tasks;

using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Grpc;

namespace Veggerby.Ignition.Grpc.Tests;

public class GrpcReadinessSignalTests
{
    [Fact]
    public void Constructor_NullChannel_ThrowsArgumentNullException()
    {
        // arrange
        var options = new GrpcReadinessOptions();
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new GrpcReadinessSignal(null!, "http://example.com", options, logger));
    }

    [Fact]
    public void Constructor_NullServiceUrl_ThrowsArgumentNullException()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://example.com");
        var options = new GrpcReadinessOptions();
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new GrpcReadinessSignal(channel, null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyServiceUrl_ThrowsArgumentException()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://example.com");
        var options = new GrpcReadinessOptions();
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new GrpcReadinessSignal(channel, string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://example.com");
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new GrpcReadinessSignal(channel, "http://example.com", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://example.com");
        var options = new GrpcReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new GrpcReadinessSignal(channel, "http://example.com", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://example.com");
        var options = new GrpcReadinessOptions();
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();
        var signal = new GrpcReadinessSignal(channel, "http://example.com", options, logger);

        // act & assert
        signal.Name.Should().Be("grpc-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var channel = GrpcChannel.ForAddress("http://example.com");
        var options = new GrpcReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();
        var signal = new GrpcReadinessSignal(channel, "http://example.com", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://example.com");
        var options = new GrpcReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();
        var signal = new GrpcReadinessSignal(channel, "http://example.com", options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var channel = GrpcChannel.ForAddress("http://invalid-host-that-does-not-exist.local");
        var options = new GrpcReadinessOptions();
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();
        var signal = new GrpcReadinessSignal(channel, "http://invalid-host-that-does-not-exist.local", options, logger);

        // act & assert - Connection failures can throw various exception types
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_Idempotent_ExecutesOnce()
    {
        // arrange - We can't easily test idempotency with real gRPC without a mock server
        // This test validates the idempotent behavior pattern by ensuring multiple awaits
        // on a failed connection don't cause different exceptions
        var channel = GrpcChannel.ForAddress("http://invalid-host.local");
        var options = new GrpcReadinessOptions();
        var logger = Substitute.For<ILogger<GrpcReadinessSignal>>();
        var signal = new GrpcReadinessSignal(channel, "http://invalid-host.local", options, logger);

        // act
        Exception? firstException = null;
        Exception? secondException = null;

        try
        {
            await signal.WaitAsync();
        }
        catch (Exception ex)
        {
            firstException = ex;
        }

        try
        {
            await signal.WaitAsync();
        }
        catch (Exception ex)
        {
            secondException = ex;
        }

        // assert - Same exception instance indicates idempotent execution (task cached)
        firstException.Should().NotBeNull();
        secondException.Should().NotBeNull();
        secondException.Should().BeSameAs(firstException);
    }
}
