using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Veggerby.Ignition.Benchmarks;

/// <summary>
/// Benchmarks measuring coordinator overhead (minimal work signals).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class CoordinatorOverheadBenchmarks
{
    private IServiceProvider _serviceProvider = null!;

    [Params(1, 10, 100, 1000)]
    public int SignalCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Parallel;
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.EnableTracing = false;
        });

        for (int i = 0; i < SignalCount; i++)
        {
            var name = $"signal-{i}";
            services.AddIgnitionFromTask(name, _ => Task.CompletedTask);
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
