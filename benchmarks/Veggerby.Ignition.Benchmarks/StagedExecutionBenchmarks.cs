using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;
using Veggerby.Ignition.Stages;

namespace Veggerby.Ignition.Benchmarks;

/// <summary>
/// Test signal that implements IStagedIgnitionSignal.
/// </summary>
internal sealed class StagedTestSignal : IStagedIgnitionSignal
{
    private readonly int _delayMs;

    public StagedTestSignal(string name, int stage, int delayMs)
    {
        Name = name;
        Stage = stage;
        _delayMs = delayMs;
    }

    public string Name { get; }
    public int Stage { get; }
    public TimeSpan? Timeout => null;

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return Task.Delay(_delayMs, cancellationToken);
    }
}

/// <summary>
/// Benchmarks for Staged execution mode with varying stage counts.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class StagedExecutionBenchmarks
{
    private IServiceProvider _serviceProvider = null!;

    [Params(2, 5, 10)]
    public int StageCount { get; set; }

    [Params(10)]
    public int SignalsPerStage { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.Staged;
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.EnableTracing = false;
        });

        for (int stage = 0; stage < StageCount; stage++)
        {
            for (int i = 0; i < SignalsPerStage; i++)
            {
                var name = $"stage-{stage}-signal-{i}";
                var signal = new StagedTestSignal(name, stage, delayMs: 10);
                services.AddIgnitionSignal(signal);
            }
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
