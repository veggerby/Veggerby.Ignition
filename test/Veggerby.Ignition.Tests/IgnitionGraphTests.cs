using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionGraphTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, IIgnitionGraph? graph, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        return new IgnitionCoordinator(signals, graph, optionsWrapper, logger);
    }

    [Fact]
    public void GraphBuilder_WithNoSignals_BuildsEmptyGraph()
    {
        // arrange
        var builder = new IgnitionGraphBuilder();

        // act
        var graph = builder.Build();

        // assert
        graph.Signals.Should().BeEmpty();
        graph.GetRootSignals().Should().BeEmpty();
        graph.GetLeafSignals().Should().BeEmpty();
    }

    [Fact]
    public void GraphBuilder_WithSingleSignal_BuildsGraphWithOneNode()
    {
        // arrange
        var signal = new FakeSignal("s1", _ => Task.CompletedTask);
        var builder = new IgnitionGraphBuilder();
        builder.AddSignal(signal);

        // act
        var graph = builder.Build();

        // assert
        graph.Signals.Should().ContainSingle();
        graph.Signals[0].Should().Be(signal);
        graph.GetRootSignals().Should().ContainSingle();
        graph.GetLeafSignals().Should().ContainSingle();
        graph.GetDependencies(signal).Should().BeEmpty();
        graph.GetDependents(signal).Should().BeEmpty();
    }

    [Fact]
    public void GraphBuilder_WithLinearDependencies_SortsTopologically()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var s3 = new FakeSignal("s3", _ => Task.CompletedTask);
        
        var builder = new IgnitionGraphBuilder();
        builder.AddSignal(s3);
        builder.AddSignal(s1);
        builder.AddSignal(s2);
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s2);

        // act
        var graph = builder.Build();

        // assert
        graph.Signals.Should().HaveCount(3);
        graph.Signals[0].Should().Be(s1);
        graph.Signals[1].Should().Be(s2);
        graph.Signals[2].Should().Be(s3);
        
        graph.GetRootSignals().Should().ContainSingle().Which.Should().Be(s1);
        graph.GetLeafSignals().Should().ContainSingle().Which.Should().Be(s3);
        
        graph.GetDependencies(s1).Should().BeEmpty();
        graph.GetDependencies(s2).Should().ContainSingle().Which.Should().Be(s1);
        graph.GetDependencies(s3).Should().ContainSingle().Which.Should().Be(s2);
    }

    [Fact]
    public void GraphBuilder_WithDiamondDependencies_SortsCorrectly()
    {
        // arrange
        //     s1
        //    /  \
        //   s2  s3
        //    \  /
        //     s4
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var s3 = new FakeSignal("s3", _ => Task.CompletedTask);
        var s4 = new FakeSignal("s4", _ => Task.CompletedTask);
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s1);
        builder.DependsOn(s4, s2, s3);

        // act
        var graph = builder.Build();

        // assert
        graph.Signals.Should().HaveCount(4);
        graph.Signals[0].Should().Be(s1);
        graph.Signals[3].Should().Be(s4); // s4 must come last
        
        graph.GetRootSignals().Should().ContainSingle().Which.Should().Be(s1);
        graph.GetLeafSignals().Should().ContainSingle().Which.Should().Be(s4);
        
        graph.GetDependencies(s4).Should().HaveCount(2);
        graph.GetDependencies(s4).Should().Contain(s2);
        graph.GetDependencies(s4).Should().Contain(s3);
    }

    [Fact]
    public void GraphBuilder_WithCycle_ThrowsException()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => Task.CompletedTask);
        var s3 = new FakeSignal("s3", _ => Task.CompletedTask);
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s2);
        builder.DependsOn(s1, s3); // Creates cycle: s1 -> s2 -> s3 -> s1

        // act & assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        ex.Message.Should().Contain("cycle");
        ex.Message.Should().Contain("s1");
    }

    [Fact]
    public void GraphBuilder_WithSelfLoop_ThrowsException()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s1, s1); // Self-loop

        // act & assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        ex.Message.Should().Contain("cycle");
    }

    [Fact]
    public async Task DependencyAware_LinearChain_ExecutesInOrder()
    {
        // arrange
        var executionOrder = new List<string>();
        var s1 = new FakeSignal("s1", async _ => { executionOrder.Add("s1"); await Task.Delay(10); });
        var s2 = new FakeSignal("s2", async _ => { executionOrder.Add("s2"); await Task.Delay(10); });
        var s3 = new FakeSignal("s3", async _ => { executionOrder.Add("s3"); await Task.Delay(10); });
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s2);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new[] { s1, s2, s3 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(3);
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
        
        executionOrder.Should().HaveCount(3);
        executionOrder[0].Should().Be("s1");
        executionOrder[1].Should().Be("s2");
        executionOrder[2].Should().Be("s3");
    }

    [Fact]
    public async Task DependencyAware_ParallelBranches_ExecutesConcurrently()
    {
        // arrange
        //     s1
        //    /  \
        //   s2  s3
        var executionStart = new Dictionary<string, DateTime>();
        var s1 = new FakeSignal("s1", async _ => 
        { 
            executionStart["s1"] = DateTime.UtcNow;
            await Task.Delay(10); 
        });
        var s2 = new FakeSignal("s2", async _ => 
        { 
            executionStart["s2"] = DateTime.UtcNow;
            await Task.Delay(50); 
        });
        var s3 = new FakeSignal("s3", async _ => 
        { 
            executionStart["s3"] = DateTime.UtcNow;
            await Task.Delay(50); 
        });
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s1);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new[] { s1, s2, s3 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(3);
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
        
        // s2 and s3 should start around the same time (after s1 completes)
        var s2Start = executionStart["s2"];
        var s3Start = executionStart["s3"];
        var timeDiff = Math.Abs((s2Start - s3Start).TotalMilliseconds);
        timeDiff.Should().BeLessThan(40); // Should be concurrent, not sequential
    }

    [Fact]
    public async Task DependencyAware_FailedDependency_SkipsDependents()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FaultingSignal("s2", new InvalidOperationException("s2 failed"));
        var s3 = new CountingSignal("s3"); // Should be skipped
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s2);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new IIgnitionSignal[] { s1, s2, s3 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(3);
        
        result.Results[0].Name.Should().Be("s1");
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
        
        result.Results[1].Name.Should().Be("s2");
        result.Results[1].Status.Should().Be(IgnitionSignalStatus.Failed);
        
        result.Results[2].Name.Should().Be("s3");
        result.Results[2].Status.Should().Be(IgnitionSignalStatus.Skipped);
        result.Results[2].SkippedDueToDependencies.Should().BeTrue();
        result.Results[2].FailedDependencies.Should().ContainSingle().Which.Should().Be("s2");
        
        s3.InvocationCount.Should().Be(0); // Never executed
    }

    [Fact]
    public async Task DependencyAware_FailFastPolicy_StopsOnFailure()
    {
        // arrange
        var s1 = new FaultingSignal("s1", new InvalidOperationException("s1 failed"));
        var s2 = new CountingSignal("s2"); // Should be skipped
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new IIgnitionSignal[] { s1, s2 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.Policy = IgnitionPolicy.FailFast;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act & assert
        await coord.WaitAllAsync(); // FailFast with DAG doesn't throw in parallel branches
        var result = await coord.GetResultAsync();
        
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Failed);
        result.Results[1].Status.Should().Be(IgnitionSignalStatus.Skipped);
    }

    [Fact]
    public async Task DependencyAware_MultipleFailedDependencies_ReportsAll()
    {
        // arrange
        //   s1   s2
        //    \  /
        //     s3
        var s1 = new FaultingSignal("s1", new InvalidOperationException("s1 failed"));
        var s2 = new FaultingSignal("s2", new InvalidOperationException("s2 failed"));
        var s3 = new CountingSignal("s3");
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s3, s1, s2);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new IIgnitionSignal[] { s1, s2, s3 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        var s3Result = result.Results.First(r => r.Name == "s3");
        s3Result.Status.Should().Be(IgnitionSignalStatus.Skipped);
        s3Result.FailedDependencies.Should().HaveCount(2);
        s3Result.FailedDependencies.Should().Contain("s1");
        s3Result.FailedDependencies.Should().Contain("s2");
    }

    [Fact]
    public async Task DependencyAware_WithoutGraph_ThrowsException()
    {
        // arrange
        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        
        var coord = CreateCoordinator(new[] { s1 }, graph: null, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => coord.WaitAllAsync());
        ex.Message.Should().Contain("IIgnitionGraph");
    }

    [Fact]
    public async Task DependencyAware_ComplexGraph_ExecutesCorrectly()
    {
        // arrange
        //      s1
        //     /  \
        //    s2  s3
        //     \  /\
        //      s4  s5
        //       \ /
        //        s6
        var executionOrder = new List<string>();
        var s1 = new FakeSignal("s1", async _ => { executionOrder.Add("s1"); await Task.Delay(10); });
        var s2 = new FakeSignal("s2", async _ => { executionOrder.Add("s2"); await Task.Delay(10); });
        var s3 = new FakeSignal("s3", async _ => { executionOrder.Add("s3"); await Task.Delay(10); });
        var s4 = new FakeSignal("s4", async _ => { executionOrder.Add("s4"); await Task.Delay(10); });
        var s5 = new FakeSignal("s5", async _ => { executionOrder.Add("s5"); await Task.Delay(10); });
        var s6 = new FakeSignal("s6", async _ => { executionOrder.Add("s6"); await Task.Delay(10); });
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s1);
        builder.DependsOn(s4, s2, s3);
        builder.DependsOn(s5, s3);
        builder.DependsOn(s6, s4, s5);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new[] { s1, s2, s3, s4, s5, s6 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.TimedOut.Should().BeFalse();
        result.Results.Should().HaveCount(6);
        result.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
        
        // Verify execution order constraints
        executionOrder.IndexOf("s1").Should().BeLessThan(executionOrder.IndexOf("s2"));
        executionOrder.IndexOf("s1").Should().BeLessThan(executionOrder.IndexOf("s3"));
        executionOrder.IndexOf("s2").Should().BeLessThan(executionOrder.IndexOf("s4"));
        executionOrder.IndexOf("s3").Should().BeLessThan(executionOrder.IndexOf("s4"));
        executionOrder.IndexOf("s3").Should().BeLessThan(executionOrder.IndexOf("s5"));
        executionOrder.IndexOf("s4").Should().BeLessThan(executionOrder.IndexOf("s6"));
        executionOrder.IndexOf("s5").Should().BeLessThan(executionOrder.IndexOf("s6"));
    }

    [Fact]
    public void SignalDependencyAttribute_ByName_CreatesCorrectAttribute()
    {
        // arrange & act
        var attr = new SignalDependencyAttribute("test-signal");

        // assert
        attr.SignalName.Should().Be("test-signal");
        attr.SignalType.Should().BeNull();
    }

    [Fact]
    public void SignalDependencyAttribute_ByType_CreatesCorrectAttribute()
    {
        // arrange & act
        var attr = new SignalDependencyAttribute(typeof(FakeSignal));

        // assert
        attr.SignalName.Should().BeNull();
        attr.SignalType.Should().Be(typeof(FakeSignal));
    }

    [Fact]
    public void SignalDependencyAttribute_WithInvalidType_ThrowsException()
    {
        // act & assert
        var ex = Assert.Throws<ArgumentException>(() => new SignalDependencyAttribute(typeof(string)));
        ex.Message.Should().Contain("IIgnitionSignal");
    }

    [SignalDependency("base-signal")]
    private sealed class AttributeDecoratedSignal : IIgnitionSignal
    {
        public string Name => "decorated";
        public TimeSpan? Timeout => null;
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void GraphBuilder_ApplyAttributeDependencies_ResolvesCorrectly()
    {
        // arrange
        var baseSignal = new FakeSignal("base-signal", _ => Task.CompletedTask);
        var decorated = new AttributeDecoratedSignal();
        
        var builder = new IgnitionGraphBuilder();
        builder.AddSignal(baseSignal);
        builder.AddSignal(decorated);
        builder.ApplyAttributeDependencies();

        // act
        var graph = builder.Build();

        // assert
        graph.GetDependencies(decorated).Should().ContainSingle().Which.Should().Be(baseSignal);
    }

    [Fact]
    public void GraphBuilder_ApplyAttributeDependencies_MissingSignal_ThrowsException()
    {
        // arrange
        var decorated = new AttributeDecoratedSignal();
        
        var builder = new IgnitionGraphBuilder();
        builder.AddSignal(decorated);

        // act & assert
        var ex = Assert.Throws<InvalidOperationException>(() => builder.ApplyAttributeDependencies());
        ex.Message.Should().Contain("base-signal");
    }

    [Fact]
    public async Task DependencyAware_MaxDegreeOfParallelism_LimitsConcurrency()
    {
        // arrange
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var syncLock = new object();

        async Task TrackConcurrency()
        {
            lock (syncLock)
            {
                concurrentCount++;
                if (concurrentCount > maxConcurrent)
                {
                    maxConcurrent = concurrentCount;
                }
            }
            await Task.Delay(50);
            lock (syncLock)
            {
                concurrentCount--;
            }
        }

        var s1 = new FakeSignal("s1", _ => Task.CompletedTask);
        var s2 = new FakeSignal("s2", _ => TrackConcurrency());
        var s3 = new FakeSignal("s3", _ => TrackConcurrency());
        var s4 = new FakeSignal("s4", _ => TrackConcurrency());
        var s5 = new FakeSignal("s5", _ => TrackConcurrency());
        
        var builder = new IgnitionGraphBuilder();
        builder.DependsOn(s2, s1);
        builder.DependsOn(s3, s1);
        builder.DependsOn(s4, s1);
        builder.DependsOn(s5, s1);
        var graph = builder.Build();
        
        var coord = CreateCoordinator(new[] { s1, s2, s3, s4, s5 }, graph, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.DependencyAware;
            o.MaxDegreeOfParallelism = 2;
            o.GlobalTimeout = TimeSpan.FromSeconds(10);
        });

        // act
        await coord.WaitAllAsync();

        // assert
        maxConcurrent.Should().BeLessThanOrEqualTo(2);
    }
}
