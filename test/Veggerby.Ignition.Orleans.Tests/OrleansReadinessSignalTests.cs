using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Veggerby.Ignition.Orleans;

namespace Veggerby.Ignition.Orleans.Tests;

public class OrleansReadinessSignalTests
{
    [Fact]
    public void Constructor_NullClusterClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new OrleansReadinessOptions();
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new OrleansReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var clusterClient = Substitute.For<IClusterClient>();
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new OrleansReadinessSignal(clusterClient, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var clusterClient = Substitute.For<IClusterClient>();
        var options = new OrleansReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new OrleansReadinessSignal(clusterClient, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var clusterClient = Substitute.For<IClusterClient>();
        var options = new OrleansReadinessOptions();
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();
        var signal = new OrleansReadinessSignal(clusterClient, options, logger);

        // act & assert
        signal.Name.Should().Be("orleans-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var clusterClient = Substitute.For<IClusterClient>();
        var options = new OrleansReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();
        var signal = new OrleansReadinessSignal(clusterClient, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var clusterClient = Substitute.For<IClusterClient>();
        var options = new OrleansReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();
        var signal = new OrleansReadinessSignal(clusterClient, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ValidClient_Succeeds()
    {
        // arrange
        var clusterClient = Substitute.For<IClusterClient>();
        var managementGrain = Substitute.For<IManagementGrain>();
        
        // Mock GetHosts to return at least one active silo
        var hosts = new Dictionary<SiloAddress, SiloStatus>
        {
            { SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111), 0), SiloStatus.Active }
        };
        managementGrain.GetHosts(true).Returns(Task.FromResult(hosts));
        clusterClient.GetGrain<IManagementGrain>(0).Returns(managementGrain);

        var options = new OrleansReadinessOptions();
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();
        var signal = new OrleansReadinessSignal(clusterClient, options, logger);

        // act
        await signal.WaitAsync();

        // assert - no exception thrown
    }

    [Fact]
    public async Task WaitAsync_Idempotent_ExecutesOnce()
    {
        // arrange
        var clusterClient = Substitute.For<IClusterClient>();
        var managementGrain = Substitute.For<IManagementGrain>();
        
        // Mock GetHosts to return at least one active silo
        var hosts = new Dictionary<SiloAddress, SiloStatus>
        {
            { SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111), 0), SiloStatus.Active }
        };
        managementGrain.GetHosts(true).Returns(Task.FromResult(hosts));
        clusterClient.GetGrain<IManagementGrain>(0).Returns(managementGrain);

        var options = new OrleansReadinessOptions();
        var logger = Substitute.For<ILogger<OrleansReadinessSignal>>();
        var signal = new OrleansReadinessSignal(clusterClient, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - Only one set of log calls should occur due to caching
        logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("completed successfully")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
