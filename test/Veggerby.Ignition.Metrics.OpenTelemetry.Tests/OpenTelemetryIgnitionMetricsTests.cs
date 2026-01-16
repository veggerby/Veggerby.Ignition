using Microsoft.Extensions.DependencyInjection;

using Veggerby.Ignition.Metrics;

namespace Veggerby.Ignition.Metrics.OpenTelemetry.Tests;

public class OpenTelemetryIgnitionMetricsTests
{
    [Fact]
    public void RecordSignalDuration_DoesNotThrow()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert - no exceptions should be thrown
        metrics.RecordSignalDuration("test-signal", TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void RecordSignalDuration_WithEmptyName_ThrowsArgumentException()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentException>(() => metrics.RecordSignalDuration("", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RecordSignalDuration_WithNullName_ThrowsArgumentNullException()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => metrics.RecordSignalDuration(null!, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RecordSignalStatus_DoesNotThrow()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert - no exceptions should be thrown
        metrics.RecordSignalStatus("test-signal", IgnitionSignalStatus.Succeeded);
        metrics.RecordSignalStatus("test-signal", IgnitionSignalStatus.Failed);
        metrics.RecordSignalStatus("test-signal", IgnitionSignalStatus.TimedOut);
        metrics.RecordSignalStatus("test-signal", IgnitionSignalStatus.Skipped);
        metrics.RecordSignalStatus("test-signal", IgnitionSignalStatus.Cancelled);
    }

    [Fact]
    public void RecordSignalStatus_WithEmptyName_ThrowsArgumentException()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentException>(() => metrics.RecordSignalStatus("", IgnitionSignalStatus.Succeeded));
    }

    [Fact]
    public void RecordSignalStatus_WithNullName_ThrowsArgumentNullException()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => metrics.RecordSignalStatus(null!, IgnitionSignalStatus.Succeeded));
    }

    [Fact]
    public void RecordTotalDuration_DoesNotThrow()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert - no exceptions should be thrown
        metrics.RecordTotalDuration(TimeSpan.FromSeconds(5.25));
    }

    [Fact]
    public void RecordSignalDuration_WithVariousDurations_DoesNotThrow()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert - test various durations
        metrics.RecordSignalDuration("fast", TimeSpan.FromMilliseconds(1));
        metrics.RecordSignalDuration("medium", TimeSpan.FromSeconds(1));
        metrics.RecordSignalDuration("slow", TimeSpan.FromSeconds(30));
        metrics.RecordSignalDuration("very-slow", TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // arrange
        var metrics = new OpenTelemetryIgnitionMetrics();

        // act & assert - multiple dispose calls should not throw
        metrics.Dispose();
        metrics.Dispose();
    }

    [Fact]
    public void AddOpenTelemetryIgnitionMetrics_RegistersMetrics()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddOpenTelemetryIgnitionMetrics();
        var provider = services.BuildServiceProvider();

        // assert
        var metrics = provider.GetService<IIgnitionMetrics>();
        metrics.Should().NotBeNull();
        metrics.Should().BeOfType<OpenTelemetryIgnitionMetrics>();
    }

    [Fact]
    public void AddOpenTelemetryIgnitionMetrics_WithNullServices_ThrowsArgumentNullException()
    {
        // arrange
        IServiceCollection services = null!;

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddOpenTelemetryIgnitionMetrics());
    }

    [Fact]
    public void AddOpenTelemetryIgnitionMetrics_RegistersSingleton()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddOpenTelemetryIgnitionMetrics();
        var provider = services.BuildServiceProvider();

        // act
        var metrics1 = provider.GetService<IIgnitionMetrics>();
        var metrics2 = provider.GetService<IIgnitionMetrics>();

        // assert
        metrics1.Should().BeSameAs(metrics2);
    }

    [Fact]
    public async Task OpenTelemetryIgnitionMetrics_IsThreadSafe()
    {
        // arrange
        using var metrics = new OpenTelemetryIgnitionMetrics();
        var tasks = new List<Task>();

        // act - record metrics from multiple threads concurrently
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                metrics.RecordSignalDuration($"signal-{index}", TimeSpan.FromMilliseconds(index * 10));
                metrics.RecordSignalStatus($"signal-{index}", IgnitionSignalStatus.Succeeded);
            }));
        }

        // assert - should not throw
        await Task.WhenAll(tasks);
    }

    [Fact]
    public void OpenTelemetryIgnitionMetrics_CreatesMeter()
    {
        // arrange & act
        using var metrics = new OpenTelemetryIgnitionMetrics();

        // assert - meter should be created (implicit through not throwing)
        metrics.RecordSignalDuration("test", TimeSpan.FromSeconds(1));
    }
}
