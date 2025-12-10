using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Veggerby.Ignition.Benchmarks;

/// <summary>
/// Benchmarks for concurrency limiting via MaxDegreeOfParallelism.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class ConcurrencyLimitingBenchmarks
{
    private IServiceProvider _serviceProvider = null!;

    [Params(100)]
    public int SignalCount { get; set; }

    [Params(1, 4, 8, -1)]
    public int MaxDegreeOfParallelism { get; set; }

    [IterationSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.EnableTracing = false;
            options.MaxDegreeOfParallelism = MaxDegreeOfParallelism;
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

    [IterationCleanup]
    public void Cleanup()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
