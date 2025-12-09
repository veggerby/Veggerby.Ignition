using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Veggerby.Ignition.Benchmarks;

/// <summary>
/// Benchmarks comparing performance with and without metrics/tracing enabled.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ObservabilityOverheadBenchmarks
{
    private IServiceProvider _serviceProvider = null!;

    [Params(100)]
    public int SignalCount { get; set; }

    [Params(true, false)]
    public bool EnableTracing { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.EnableTracing = EnableTracing;
        });

        for (int i = 0; i < SignalCount; i++)
        {
            var name = $"signal-{i}";
            services.AddIgnitionFromTask(name, ct => Task.Delay(10, ct));
        }

        _serviceProvider = services.BuildServiceProvider();
    }

    [Benchmark]
    public async Task WaitAllAsync()
    {
        var coordinator = _serviceProvider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
