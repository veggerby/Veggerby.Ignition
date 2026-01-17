using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Veggerby.Ignition.Metrics;

namespace Veggerby.Ignition.Metrics.Prometheus.Tests;

public class PrometheusIgnitionMetricsTests
{
    [Fact]
    public void RecordSignalDuration_DoesNotThrow()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert - no exceptions should be thrown
        metrics.RecordSignalDuration("test-signal", TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public void RecordSignalDuration_WithEmptyName_ThrowsArgumentException()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentException>(() => metrics.RecordSignalDuration("", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RecordSignalDuration_WithNullName_ThrowsArgumentNullException()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => metrics.RecordSignalDuration(null!, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void RecordSignalStatus_DoesNotThrow()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

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
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentException>(() => metrics.RecordSignalStatus("", IgnitionSignalStatus.Succeeded));
    }

    [Fact]
    public void RecordSignalStatus_WithNullName_ThrowsArgumentNullException()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => metrics.RecordSignalStatus(null!, IgnitionSignalStatus.Succeeded));
    }

    [Fact]
    public void RecordTotalDuration_DoesNotThrow()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert - no exceptions should be thrown
        metrics.RecordTotalDuration(TimeSpan.FromSeconds(5.25));
    }

    [Fact]
    public void RecordSignalDuration_WithVariousDurations_DoesNotThrow()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();

        // act & assert - test various durations
        metrics.RecordSignalDuration("fast", TimeSpan.FromMilliseconds(1));
        metrics.RecordSignalDuration("medium", TimeSpan.FromSeconds(1));
        metrics.RecordSignalDuration("slow", TimeSpan.FromSeconds(30));
        metrics.RecordSignalDuration("very-slow", TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AddPrometheusIgnitionMetrics_RegistersMetrics()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddPrometheusIgnitionMetrics();
        var provider = services.BuildServiceProvider();

        // assert
        var metrics = provider.GetService<IIgnitionMetrics>();
        metrics.Should().NotBeNull();
        metrics.Should().BeOfType<PrometheusIgnitionMetrics>();
    }

    [Fact]
    public void AddPrometheusIgnitionMetrics_ConfiguresIgnitionOptions()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddPrometheusIgnitionMetrics();
        var provider = services.BuildServiceProvider();

        // assert
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.Metrics.Should().NotBeNull();
        options.Metrics.Should().BeOfType<PrometheusIgnitionMetrics>();
    }

    [Fact]
    public void AddPrometheusIgnitionMetrics_WithNullServices_ThrowsArgumentNullException()
    {
        // arrange
        IServiceCollection services = null!;

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddPrometheusIgnitionMetrics());
    }

    [Fact]
    public void AddPrometheusIgnitionMetrics_RegistersSingleton()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddPrometheusIgnitionMetrics();
        var provider = services.BuildServiceProvider();

        // act
        var metrics1 = provider.GetService<IIgnitionMetrics>();
        var metrics2 = provider.GetService<IIgnitionMetrics>();

        // assert
        metrics1.Should().BeSameAs(metrics2);
    }

    [Fact]
    public async Task PrometheusIgnitionMetrics_IsThreadSafe()
    {
        // arrange
        var metrics = new PrometheusIgnitionMetrics();
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
}
