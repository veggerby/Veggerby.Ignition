using global::MassTransit;

using Microsoft.Extensions.Logging;

using Veggerby.Ignition.MassTransit;

namespace Veggerby.Ignition.MassTransit.Tests;

public class MassTransitReadinessSignalTests
{
    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var bus = Substitute.For<IBus>();
        var options = new MassTransitReadinessOptions();
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(bus, options, logger);

        // act & assert
        Assert.Equal("masstransit-readiness", signal.Name);
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var bus = Substitute.For<IBus>();
        var options = new MassTransitReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(bus, options, logger);

        // act & assert
        Assert.Equal(TimeSpan.FromSeconds(15), signal.Timeout);
    }

    [Fact]
    public async Task WaitAsync_BusNotBusControl_ThrowsException()
    {
        // arrange
        var bus = Substitute.For<IBus>(); // Not IBusControl
        var options = new MassTransitReadinessOptions();
        var logger = Substitute.For<ILogger<MassTransitReadinessSignal>>();
        var signal = new MassTransitReadinessSignal(bus, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    public void Options_DefaultValues_AreCorrect()
    {
        // arrange & act
        var options = new MassTransitReadinessOptions();

        // assert
        Assert.Null(options.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.BusReadyTimeout);
    }
}
