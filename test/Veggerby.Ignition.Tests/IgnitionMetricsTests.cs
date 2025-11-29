using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionMetricsTests
{
    private static IgnitionCoordinator CreateCoordinator(
        IEnumerable<IIgnitionSignal> signals,
        Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        return new IgnitionCoordinator(signals, optionsWrapper, logger);
    }

    [Fact]
    public async Task NullMetrics_NoExceptionThrown()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Metrics = null; // No metrics configured
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(1);
        result.Results.First().Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task NullIgnitionMetrics_Instance_IsNoOp()
    {
        // arrange
        var metrics = NullIgnitionMetrics.Instance;

        // act & assert - no exceptions should be thrown
        metrics.RecordSignalDuration("test", TimeSpan.FromMilliseconds(100));
        metrics.RecordSignalStatus("test", IgnitionSignalStatus.Succeeded);
        metrics.RecordTotalDuration(TimeSpan.FromSeconds(1));

        await Task.CompletedTask; // Keep async pattern for consistency
    }

    [Fact]
    public async Task MetricsAdapter_RecordsSignalDuration()
    {
        // arrange
        var recordedDurations = new List<(string Name, TimeSpan Duration)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalDuration = (name, duration) => recordedDurations.Add((name, duration))
        };
        var signal = new FakeSignal("test-signal", async _ => await Task.Delay(20));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedDurations.Should().HaveCount(1);
        recordedDurations[0].Name.Should().Be("test-signal");
        recordedDurations[0].Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task MetricsAdapter_RecordsSignalStatus_Succeeded()
    {
        // arrange
        var recordedStatuses = new List<(string Name, IgnitionSignalStatus Status)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status))
        };
        var signal = new FakeSignal("success-signal", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(1);
        recordedStatuses[0].Name.Should().Be("success-signal");
        recordedStatuses[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task MetricsAdapter_RecordsSignalStatus_Failed()
    {
        // arrange
        var recordedStatuses = new List<(string Name, IgnitionSignalStatus Status)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status))
        };
        var signal = new FaultingSignal("failed-signal", new InvalidOperationException("boom"));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Policy = IgnitionPolicy.BestEffort;
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(1);
        recordedStatuses[0].Name.Should().Be("failed-signal");
        recordedStatuses[0].Status.Should().Be(IgnitionSignalStatus.Failed);
    }

    [Fact]
    public async Task MetricsAdapter_RecordsSignalStatus_TimedOut()
    {
        // arrange
        var recordedStatuses = new List<(string Name, IgnitionSignalStatus Status)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status))
        };
        var signal = new FakeSignal("timeout-signal", async ct => await Task.Delay(100, ct),
            timeout: TimeSpan.FromMilliseconds(30));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.CancelIndividualOnTimeout = true;
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(1);
        recordedStatuses[0].Name.Should().Be("timeout-signal");
        recordedStatuses[0].Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task MetricsAdapter_RecordsTotalDuration()
    {
        // arrange
        var recordedTotalDurations = new List<TimeSpan>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordTotalDuration = duration => recordedTotalDurations.Add(duration)
        };
        var signal = new FakeSignal("test-signal", async _ => await Task.Delay(20));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedTotalDurations.Should().HaveCount(1);
        recordedTotalDurations[0].Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task MetricsAdapter_RecordsAllSignals_InParallel()
    {
        // arrange
        var recordedStatuses = new System.Collections.Concurrent.ConcurrentBag<(string Name, IgnitionSignalStatus Status)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status))
        };
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var s3 = new FakeSignal("s3", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { s1, s2, s3 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(3);
        recordedStatuses.Should().Contain(x => x.Name == "s1" && x.Status == IgnitionSignalStatus.Succeeded);
        recordedStatuses.Should().Contain(x => x.Name == "s2" && x.Status == IgnitionSignalStatus.Succeeded);
        recordedStatuses.Should().Contain(x => x.Name == "s3" && x.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task MetricsAdapter_RecordsAllSignals_InSequential()
    {
        // arrange
        var recordedStatuses = new List<(string Name, IgnitionSignalStatus Status)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status))
        };
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { s1, s2 }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(2);
        recordedStatuses[0].Name.Should().Be("s1");
        recordedStatuses[1].Name.Should().Be("s2");
    }

    [Fact]
    public async Task MetricsAdapter_RecordsMixedStatuses()
    {
        // arrange
        var recordedStatuses = new System.Collections.Concurrent.ConcurrentBag<(string Name, IgnitionSignalStatus Status)>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status))
        };
        var succeeded = new FakeSignal("success", _ => Task.CompletedTask);
        var failed = new FaultingSignal("failed", new InvalidOperationException("boom"));
        var timedOut = new FakeSignal("timeout", async ct => await Task.Delay(100, ct),
            timeout: TimeSpan.FromMilliseconds(30));
        var coord = CreateCoordinator(new IIgnitionSignal[] { succeeded, failed, timedOut }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.Policy = IgnitionPolicy.BestEffort;
            o.CancelIndividualOnTimeout = true;
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(3);
        recordedStatuses.Should().Contain(x => x.Name == "success" && x.Status == IgnitionSignalStatus.Succeeded);
        recordedStatuses.Should().Contain(x => x.Name == "failed" && x.Status == IgnitionSignalStatus.Failed);
        recordedStatuses.Should().Contain(x => x.Name == "timeout" && x.Status == IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task MetricsAdapter_ZeroSignals_RecordsTotalDuration()
    {
        // arrange
        var recordedTotalDurations = new List<TimeSpan>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordTotalDuration = duration => recordedTotalDurations.Add(duration)
        };
        var coord = CreateCoordinator(Array.Empty<IIgnitionSignal>(), o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedTotalDurations.Should().HaveCount(1);
        recordedTotalDurations[0].Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public void AddIgnitionMetrics_RegistersMetricsInstance()
    {
        // arrange
        var metrics = new TestIgnitionMetrics();
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddIgnitionMetrics(metrics);
        var provider = services.BuildServiceProvider();

        // act
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.Metrics.Should().BeSameAs(metrics);
    }

    [Fact]
    public void AddIgnitionMetrics_Factory_RegistersMetricsViaFactory()
    {
        // arrange
        var metrics = new TestIgnitionMetrics();
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddIgnitionMetrics(_ => metrics);
        var provider = services.BuildServiceProvider();

        // act
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.Metrics.Should().BeSameAs(metrics);
    }

    [Fact]
    public void AddIgnitionMetrics_Generic_RegistersMetricsByType()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        services.AddIgnitionMetrics<TestIgnitionMetrics>();
        var provider = services.BuildServiceProvider();

        // act
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.Metrics.Should().NotBeNull();
        options.Metrics.Should().BeOfType<TestIgnitionMetrics>();
    }

    [Fact]
    public void AddIgnitionMetrics_ThrowsOnNullMetrics()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        var act = () => services.AddIgnitionMetrics((IIgnitionMetrics)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metrics");
    }

    [Fact]
    public void AddIgnitionMetrics_Factory_ThrowsOnNullFactory()
    {
        // arrange
        var services = new ServiceCollection();

        // act & assert
        var act = () => services.AddIgnitionMetrics((Func<IServiceProvider, IIgnitionMetrics>)null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("metricsFactory");
    }

    [Fact]
    public async Task MetricsAdapter_IntegrationTest_WithDI()
    {
        // arrange
        var recordedStatuses = new List<(string Name, IgnitionSignalStatus Status)>();
        var recordedTotalDurations = new List<TimeSpan>();
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (name, status) => recordedStatuses.Add((name, status)),
            OnRecordTotalDuration = duration => recordedTotalDurations.Add(duration)
        };
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        services.AddIgnition(o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
        });
        services.AddIgnitionMetrics(metrics);
        services.AddIgnitionFromTask("di-signal", Task.CompletedTask);
        var provider = services.BuildServiceProvider();
        var coord = provider.GetRequiredService<IIgnitionCoordinator>();

        // act
        await coord.WaitAllAsync();

        // assert
        recordedStatuses.Should().HaveCount(1);
        recordedStatuses[0].Name.Should().Be("di-signal");
        recordedTotalDurations.Should().HaveCount(1);
    }

    [Fact]
    public async Task MetricsAdapter_ThreadSafety_ParallelSignals()
    {
        // arrange
        var recordedCount = 0;
        var metrics = new TestIgnitionMetrics
        {
            OnRecordSignalStatus = (_, _) => Interlocked.Increment(ref recordedCount)
        };
        var signals = Enumerable.Range(0, 10)
            .Select(i => new FakeSignal($"s{i}", async _ => await Task.Delay(10)))
            .ToArray();
        var coord = CreateCoordinator(signals, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Metrics = metrics;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        recordedCount.Should().Be(10);
    }

    /// <summary>
    /// Test implementation of IIgnitionMetrics for verifying behavior.
    /// </summary>
    private sealed class TestIgnitionMetrics : IIgnitionMetrics
    {
        public Action<string, TimeSpan>? OnRecordSignalDuration { get; set; }
        public Action<string, IgnitionSignalStatus>? OnRecordSignalStatus { get; set; }
        public Action<TimeSpan>? OnRecordTotalDuration { get; set; }

        public void RecordSignalDuration(string name, TimeSpan duration)
        {
            OnRecordSignalDuration?.Invoke(name, duration);
        }

        public void RecordSignalStatus(string name, IgnitionSignalStatus status)
        {
            OnRecordSignalStatus?.Invoke(name, status);
        }

        public void RecordTotalDuration(TimeSpan duration)
        {
            OnRecordTotalDuration?.Invoke(duration);
        }
    }
}
