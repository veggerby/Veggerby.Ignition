using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Veggerby.Ignition.Benchmarks;

/// <summary>
/// Benchmarks for different execution modes (Parallel, Sequential, DependencyAware, Staged)
/// with varying signal counts.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class ExecutionModeBenchmarks
{
    private IServiceProvider _serviceProvider = null!;

    [Params(10, 100, 1000)]
    public int SignalCount { get; set; }

    [Params(IgnitionExecutionMode.Parallel, IgnitionExecutionMode.Sequential)]
    public IgnitionExecutionMode ExecutionMode { get; set; }

    [Params(10)]
    public int SignalDelayMs { get; set; }

    [IterationSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = ExecutionMode;
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.Policy = IgnitionPolicy.BestEffort;
            options.EnableTracing = false;
        });

        for (int i = 0; i < SignalCount; i++)
        {
            var name = $"signal-{i}";
            var delay = SignalDelayMs;
            services.AddIgnitionFromTask(name, ct => Task.Delay(delay, ct));
        }

        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark]
    public async Task WaitAllAsync()
    {
        var coordinator = _serviceProvider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();
    }

    [IterationCleanup]
    public void Cleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
