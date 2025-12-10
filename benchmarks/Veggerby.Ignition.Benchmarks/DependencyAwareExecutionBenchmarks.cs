using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Veggerby.Ignition;

namespace Veggerby.Ignition.Benchmarks;

/// <summary>
/// Benchmarks for DependencyAware (DAG) execution mode with varying graph structures.
/// Uses the declarative attribute-based approach for simplicity.
/// Note: Multimodal distributions may occur due to parallel scheduling variance - this is expected.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class DependencyAwareExecutionBenchmarks
{
    private IServiceProvider _serviceProvider = null!;

    [Params(10, 50, 100)]
    public int SignalCount { get; set; }

    // 10ms delay represents realistic signal work
    // We measure DAG coordination overhead, not synthetic signal delays
    private const int SignalDelayMs = 10;

    [IterationSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIgnition(options =>
        {
            options.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.EnableTracing = false;
        });

        // Create a simple DAG: chains of 10 signals each
        // Structure: independent chains that can run in parallel
        int chainLength = 10;
        int chainCount = SignalCount / chainLength;

        // Use manual graph builder to add dependencies
        var graphBuilder = new IgnitionGraphBuilder();
        var signalsByName = new Dictionary<string, IIgnitionSignal>();

        // Create and register all signals
        for (int chain = 0; chain < chainCount; chain++)
        {
            for (int link = 0; link < chainLength; link++)
            {
                var name = $"chain-{chain}-link-{link}";
                var signal = new BenchmarkSignal(name, delayMs: SignalDelayMs);
                signalsByName[name] = signal;
                graphBuilder.AddSignal(signal);
                services.AddIgnitionSignal(signal);
            }
        }

        // Add dependencies (each link depends on previous link in chain)
        for (int chain = 0; chain < chainCount; chain++)
        {
            for (int link = 1; link < chainLength; link++)
            {
                var currentName = $"chain-{chain}-link-{link}";
                var previousName = $"chain-{chain}-link-{link - 1}";
                graphBuilder.DependsOn(signalsByName[currentName], signalsByName[previousName]);
            }
        }

        services.AddSingleton<IIgnitionGraph>(graphBuilder.Build());
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

/// <summary>
/// Simple benchmark signal for DAG testing.
/// </summary>
internal sealed class BenchmarkSignal : IIgnitionSignal
{
    private readonly int _delayMs;

    public BenchmarkSignal(string name, int delayMs)
    {
        Name = name;
        _delayMs = delayMs;
    }

    public string Name { get; }
    public TimeSpan? Timeout => null;

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return Task.Delay(_delayMs, cancellationToken);
    }
}
