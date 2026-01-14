using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class IgnitionLifecycleHooksTests
{
    private static IgnitionCoordinator CreateCoordinator(
        IEnumerable<IIgnitionSignal> signals,
        Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var factories = signals.Select(s => new TestSignalFactory(s)).ToList();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);
    }

    [Fact]
    public async Task NullHooks_DoesNotThrow_CompletesNormally()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = null; // explicitly null
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task HookInvocationOrder_CorrectSequence()
    {
        // arrange
        var invocationOrder = new ConcurrentQueue<string>();
        var hooks = new TrackingLifecycleHooks(invocationOrder);

        var signal1 = new FakeSignal("signal1", async ct =>
        {
            invocationOrder.Enqueue("signal1-executing");
            await Task.Delay(10, ct);
        });
        var signal2 = new FakeSignal("signal2", async ct =>
        {
            invocationOrder.Enqueue("signal2-executing");
            await Task.Delay(10, ct);
        });

        var coord = CreateCoordinator(new[] { signal1, signal2 }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        var sequence = invocationOrder.ToArray();
        sequence.Should().StartWith("OnBeforeIgnitionAsync");
        sequence.Should().Contain("OnBeforeSignalAsync:signal1");
        sequence.Should().Contain("OnAfterSignalAsync:signal1");
        sequence.Should().Contain("OnBeforeSignalAsync:signal2");
        sequence.Should().Contain("OnAfterSignalAsync:signal2");
        sequence.Should().EndWith("OnAfterIgnitionAsync");
    }

    [Fact]
    public async Task ParallelExecution_AllHooksInvoked()
    {
        // arrange
        var beforeSignalCalls = new ConcurrentBag<string>();
        var afterSignalCalls = new ConcurrentBag<string>();
        var hooks = new CallbackLifecycleHooks
        {
            OnBeforeSignal = (name, ct) =>
            {
                beforeSignalCalls.Add(name);
                return Task.CompletedTask;
            },
            OnAfterSignal = (result, ct) =>
            {
                afterSignalCalls.Add(result.Name);
                return Task.CompletedTask;
            }
        };

        var signals = Enumerable.Range(0, 5)
            .Select(i => new FakeSignal($"signal{i}", async ct => await Task.Delay(10, ct)))
            .ToArray();

        var coord = CreateCoordinator(signals, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        beforeSignalCalls.Should().HaveCount(5);
        afterSignalCalls.Should().HaveCount(5);
        beforeSignalCalls.Should().Contain(signals.Select(s => s.Name));
        afterSignalCalls.Should().Contain(signals.Select(s => s.Name));
    }

    [Fact]
    public async Task OnBeforeIgnitionThrows_LogsWarning_ContinuesExecution()
    {
        // arrange
        var hooks = new FaultingBeforeIgnitionHooks();
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
        hooks.OnBeforeIgnitionCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnAfterIgnitionThrows_LogsWarning_DoesNotAffectResult()
    {
        // arrange
        var hooks = new FaultingAfterIgnitionHooks();
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
        hooks.OnAfterIgnitionCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnBeforeSignalThrows_LogsWarning_SignalStillExecutes()
    {
        // arrange
        var signalExecuted = false;
        var hooks = new FaultingBeforeSignalHooks();
        var signal = new FakeSignal("test", _ =>
        {
            signalExecuted = true;
            return Task.CompletedTask;
        });

        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        signalExecuted.Should().BeTrue();
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
        hooks.OnBeforeSignalCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnAfterSignalThrows_LogsWarning_DoesNotAffectSignalResult()
    {
        // arrange
        var hooks = new FaultingAfterSignalHooks();
        var signal = new FakeSignal("test", _ => Task.CompletedTask);

        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.GlobalTimeout = TimeSpan.FromSeconds(1);
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results[0].Status.Should().Be(IgnitionSignalStatus.Succeeded);
        hooks.OnAfterSignalCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnAfterSignal_ReceivesCorrectStatus_ForSucceededSignal()
    {
        // arrange
        IgnitionSignalResult? capturedResult = null;
        var hooks = new CallbackLifecycleHooks
        {
            OnAfterSignal = (result, ct) =>
            {
                capturedResult = result;
                return Task.CompletedTask;
            }
        };

        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Name.Should().Be("test");
        capturedResult.Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task OnAfterSignal_ReceivesCorrectStatus_ForFailedSignal()
    {
        // arrange
        IgnitionSignalResult? capturedResult = null;
        var hooks = new CallbackLifecycleHooks
        {
            OnAfterSignal = (result, ct) =>
            {
                capturedResult = result;
                return Task.CompletedTask;
            }
        };

        var signal = new FaultingSignal("test", new InvalidOperationException("boom"));
        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.Policy = IgnitionPolicy.BestEffort;
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Name.Should().Be("test");
        capturedResult.Status.Should().Be(IgnitionSignalStatus.Failed);
        capturedResult.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task OnAfterSignal_ReceivesCorrectStatus_ForTimedOutSignal()
    {
        // arrange
        IgnitionSignalResult? capturedResult = null;
        var hooks = new CallbackLifecycleHooks
        {
            OnAfterSignal = (result, ct) =>
            {
                capturedResult = result;
                return Task.CompletedTask;
            }
        };

        var signal = new FakeSignal(
            "test",
            async ct => await Task.Delay(1000, ct),
            timeout: TimeSpan.FromMilliseconds(50));

        var coord = CreateCoordinator(new[] { signal }, o =>
        {
            o.CancelIndividualOnTimeout = true;
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Name.Should().Be("test");
        capturedResult.Status.Should().Be(IgnitionSignalStatus.TimedOut);
    }

    [Fact]
    public async Task OnAfterIgnition_ReceivesCompleteResult()
    {
        // arrange
        IgnitionResult? capturedResult = null;
        var hooks = new CallbackLifecycleHooks
        {
            OnAfterIgnition = (result, ct) =>
            {
                capturedResult = result;
                return Task.CompletedTask;
            }
        };

        var signals = new[]
        {
            new FakeSignal("s1", _ => Task.CompletedTask),
            new FakeSignal("s2", _ => Task.CompletedTask)
        };

        var coord = CreateCoordinator(signals, o =>
        {
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        capturedResult.Should().NotBeNull();
        capturedResult!.Results.Should().HaveCount(2);
        capturedResult.Results.Should().OnlyContain(r => r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task EmptySignalSet_HooksStillInvoked()
    {
        // arrange
        var beforeCalled = false;
        var afterCalled = false;
        var hooks = new CallbackLifecycleHooks
        {
            OnBeforeIgnition = ct =>
            {
                beforeCalled = true;
                return Task.CompletedTask;
            },
            OnAfterIgnition = (result, ct) =>
            {
                afterCalled = true;
                return Task.CompletedTask;
            }
        };

        var coord = CreateCoordinator([], o =>
        {
            o.LifecycleHooks = hooks;
        });

        // act
        await coord.WaitAllAsync();

        // assert
        beforeCalled.Should().BeTrue();
        afterCalled.Should().BeTrue();
    }

    [Fact]
    public async Task StagedExecution_HooksInvokedForEachStage()
    {
        // arrange
        var signalCallOrder = new ConcurrentQueue<string>();
        var hooks = new TrackingLifecycleHooks(signalCallOrder);

        var stage0Signal = new FakeSignal("stage0", async ct =>
        {
            signalCallOrder.Enqueue("stage0-executing");
            await Task.Delay(10, ct);
        });
        var stage1Signal = new FakeSignal("stage1", async ct =>
        {
            signalCallOrder.Enqueue("stage1-executing");
            await Task.Delay(10, ct);
        });

        var factories = new IIgnitionSignalFactory[]
        {
            new StagedIgnitionSignalFactory(new TestSignalFactory(stage0Signal), 0),
            new StagedIgnationSignalFactory(new TestSignalFactory(stage1Signal), 1)
        };

        var opts = new IgnitionOptions
        {
            ExecutionMode = IgnitionExecutionMode.Staged,
            GlobalTimeout = TimeSpan.FromSeconds(5),
            LifecycleHooks = hooks
        };
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var coord = new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);

        // act
        await coord.WaitAllAsync();

        // assert
        var sequence = signalCallOrder.ToArray();
        sequence.Should().Contain("OnBeforeSignalAsync:stage0");
        sequence.Should().Contain("OnAfterSignalAsync:stage0");
        sequence.Should().Contain("OnBeforeSignalAsync:stage1");
        sequence.Should().Contain("OnAfterSignalAsync:stage1");
    }

    [Fact]
    public async Task DIRegistration_WithType_ResolvedCorrectly()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnitionLifecycleHooks<TestLifecycleHooks>();
        services.AddIgnition();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // act & assert
        options.LifecycleHooks.Should().NotBeNull();
        options.LifecycleHooks.Should().BeOfType<TestLifecycleHooks>();
    }

    [Fact]
    public async Task DIRegistration_WithFactory_ResolvedCorrectly()
    {
        // arrange
        var testHooks = new TestLifecycleHooks();
        var services = new ServiceCollection();
        services.AddIgnitionLifecycleHooks(_ => testHooks);
        services.AddIgnition();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // act & assert
        options.LifecycleHooks.Should().NotBeNull();
        options.LifecycleHooks.Should().BeSameAs(testHooks);
    }

    [Fact]
    public async Task SimpleMode_WithLifecycleHooks_ConfiguredCorrectly()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSimpleIgnition(builder => builder
            .UseWebApiProfile()
            .WithLifecycleHooks<TestLifecycleHooks>()
            .AddSignal("test", _ => Task.CompletedTask));

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // act & assert
        options.LifecycleHooks.Should().NotBeNull();
        options.LifecycleHooks.Should().BeOfType<TestLifecycleHooks>();
    }

    [Fact]
    public async Task SimpleMode_WithLifecycleHooksFactory_ConfiguredCorrectly()
    {
        // arrange
        var testHooks = new TestLifecycleHooks();
        var services = new ServiceCollection();
        services.AddSimpleIgnition(builder => builder
            .UseWebApiProfile()
            .WithLifecycleHooks(_ => testHooks)
            .AddSignal("test", _ => Task.CompletedTask));

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // act & assert
        options.LifecycleHooks.Should().NotBeNull();
        options.LifecycleHooks.Should().BeSameAs(testHooks);
    }

    // Test helper classes
    private sealed class TrackingLifecycleHooks : IIgnitionLifecycleHooks
    {
        private readonly ConcurrentQueue<string> _invocationOrder;

        public TrackingLifecycleHooks(ConcurrentQueue<string> invocationOrder)
        {
            _invocationOrder = invocationOrder;
        }

        public Task OnBeforeIgnitionAsync(CancellationToken cancellationToken)
        {
            _invocationOrder.Enqueue("OnBeforeIgnitionAsync");
            return Task.CompletedTask;
        }

        public Task OnAfterIgnitionAsync(IgnitionResult result, CancellationToken cancellationToken)
        {
            _invocationOrder.Enqueue("OnAfterIgnitionAsync");
            return Task.CompletedTask;
        }

        public Task OnBeforeSignalAsync(string signalName, CancellationToken cancellationToken)
        {
            _invocationOrder.Enqueue($"OnBeforeSignalAsync:{signalName}");
            return Task.CompletedTask;
        }

        public Task OnAfterSignalAsync(IgnitionSignalResult result, CancellationToken cancellationToken)
        {
            _invocationOrder.Enqueue($"OnAfterSignalAsync:{result.Name}");
            return Task.CompletedTask;
        }
    }

    private sealed class CallbackLifecycleHooks : IIgnitionLifecycleHooks
    {
        public Func<CancellationToken, Task>? OnBeforeIgnition { get; set; }
        public Func<IgnitionResult, CancellationToken, Task>? OnAfterIgnition { get; set; }
        public Func<string, CancellationToken, Task>? OnBeforeSignal { get; set; }
        public Func<IgnitionSignalResult, CancellationToken, Task>? OnAfterSignal { get; set; }

        public Task OnBeforeIgnitionAsync(CancellationToken cancellationToken)
            => OnBeforeIgnition?.Invoke(cancellationToken) ?? Task.CompletedTask;

        public Task OnAfterIgnitionAsync(IgnitionResult result, CancellationToken cancellationToken)
            => OnAfterIgnition?.Invoke(result, cancellationToken) ?? Task.CompletedTask;

        public Task OnBeforeSignalAsync(string signalName, CancellationToken cancellationToken)
            => OnBeforeSignal?.Invoke(signalName, cancellationToken) ?? Task.CompletedTask;

        public Task OnAfterSignalAsync(IgnitionSignalResult result, CancellationToken cancellationToken)
            => OnAfterSignal?.Invoke(result, cancellationToken) ?? Task.CompletedTask;
    }

    private sealed class FaultingBeforeIgnitionHooks : IIgnitionLifecycleHooks
    {
        public bool OnBeforeIgnitionCalled { get; private set; }

        public Task OnBeforeIgnitionAsync(CancellationToken cancellationToken)
        {
            OnBeforeIgnitionCalled = true;
            throw new InvalidOperationException("OnBeforeIgnition failed");
        }
    }

    private sealed class FaultingAfterIgnitionHooks : IIgnitionLifecycleHooks
    {
        public bool OnAfterIgnitionCalled { get; private set; }

        public Task OnAfterIgnitionAsync(IgnitionResult result, CancellationToken cancellationToken)
        {
            OnAfterIgnitionCalled = true;
            throw new InvalidOperationException("OnAfterIgnition failed");
        }
    }

    private sealed class FaultingBeforeSignalHooks : IIgnitionLifecycleHooks
    {
        public bool OnBeforeSignalCalled { get; private set; }

        public Task OnBeforeSignalAsync(string signalName, CancellationToken cancellationToken)
        {
            OnBeforeSignalCalled = true;
            throw new InvalidOperationException("OnBeforeSignal failed");
        }
    }

    private sealed class FaultingAfterSignalHooks : IIgnitionLifecycleHooks
    {
        public bool OnAfterSignalCalled { get; private set; }

        public Task OnAfterSignalAsync(IgnitionSignalResult result, CancellationToken cancellationToken)
        {
            OnAfterSignalCalled = true;
            throw new InvalidOperationException("OnAfterSignal failed");
        }
    }

    private sealed class TestLifecycleHooks : IIgnitionLifecycleHooks
    {
        // Default implementation from interface
    }

    private sealed class StagedIgnationSignalFactory : IIgnitionSignalFactory
    {
        private readonly IIgnitionSignalFactory _innerFactory;
        private readonly int _stage;

        public StagedIgnationSignalFactory(IIgnitionSignalFactory innerFactory, int stage)
        {
            _innerFactory = innerFactory;
            _stage = stage;
        }

        public string Name => _innerFactory.Name;
        public TimeSpan? Timeout => _innerFactory.Timeout;
        public int? Stage => _stage;

        public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
            => _innerFactory.CreateSignal(serviceProvider);
    }
}
