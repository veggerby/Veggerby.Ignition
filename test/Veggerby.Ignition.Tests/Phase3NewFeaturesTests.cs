using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Tests;

public class IsRequiredSignalTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var factories = signals.Select(s => new TestSignalFactory(s)).ToList();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);
    }

    [Fact]
    public async Task AdvisorySignal_Failure_DoesNotBlockStartup()
    {
        // arrange
        var advisory = new AdvisoryFakeSignal("cache-warmup");
        var required = new FakeSignal("critical-db", _ => Task.CompletedTask);
        var coord = CreateCoordinator([advisory, required], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act - should not throw even though advisory signal fails
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        coord.State.Should().Be(IgnitionState.Completed);
        result.Results.Should().Contain(r => r.Name == "cache-warmup" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r => r.Name == "critical-db" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task RequiredSignal_Failure_CausesFailedState()
    {
        // arrange
        var required = new FaultingSignal("critical-db", new InvalidOperationException("boom"));
        var coord = CreateCoordinator([required], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Policy = IgnitionPolicy.BestEffort;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        coord.State.Should().Be(IgnitionState.Failed);
        result.Results.Should().Contain(r => r.Name == "critical-db" && r.Status == IgnitionSignalStatus.Failed);
    }

    [Fact]
    public async Task SignalResult_CapturesIsRequired_True()
    {
        // arrange
        var signal = new FakeSignal("required-signal", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o => o.GlobalTimeout = TimeSpan.FromSeconds(5));

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().ContainSingle(r => r.Name == "required-signal" && r.IsRequired);
    }

    [Fact]
    public async Task SignalResult_CapturesIsRequired_False_ForAdvisorySignal()
    {
        // arrange
        var advisory = new SucceedingAdvisoryFakeSignal("optional-signal");
        var coord = CreateCoordinator([advisory], o => o.GlobalTimeout = TimeSpan.FromSeconds(5));

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().ContainSingle(r => r.Name == "optional-signal" && !r.IsRequired);
    }

    /// <summary>Advisory signal that fails immediately.</summary>
    private sealed class AdvisoryFakeSignal : IIgnitionSignal
    {
        public AdvisoryFakeSignal(string name) { Name = name; }
        public string Name { get; }
        public TimeSpan? Timeout => null;
        public bool IsRequired => false;
        public Task WaitAsync(CancellationToken ct = default)
            => Task.FromException(new InvalidOperationException("Advisory signal intentionally failed"));
    }

    /// <summary>Advisory signal that succeeds immediately.</summary>
    private sealed class SucceedingAdvisoryFakeSignal : IIgnitionSignal
    {
        public SucceedingAdvisoryFakeSignal(string name) { Name = name; }
        public string Name { get; }
        public TimeSpan? Timeout => null;
        public bool IsRequired => false;
        public Task WaitAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}

public class SignalFilterTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var factories = signals.Select(s => new TestSignalFactory(s)).ToList();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);
    }

    [Fact]
    public async Task Filter_AllowsSignal_ExecutesNormally()
    {
        // arrange
        var invocations = 0;
        var signal = new FakeSignal("test", _ => { invocations++; return Task.CompletedTask; });
        var filter = new AllowAllFilter();
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Filters = [filter];
        });

        // act
        await coord.WaitAllAsync();

        // assert
        invocations.Should().Be(1);
        filter.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task Filter_BlocksSignal_SkipsExecution()
    {
        // arrange
        var invocations = 0;
        var signal = new FakeSignal("test", _ => { invocations++; return Task.CompletedTask; });
        var filter = new DenyAllFilter();
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Filters = [filter];
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        invocations.Should().Be(0);
        result.Results.Should().ContainSingle(r => r.Name == "test" && r.Status == IgnitionSignalStatus.Skipped);
    }

    [Fact]
    public async Task Filter_SkipsOne_OtherSignalsStillRun()
    {
        // arrange
        var invocations1 = 0;
        var invocations2 = 0;
        var signal1 = new FakeSignal("signal1", _ => { invocations1++; return Task.CompletedTask; });
        var signal2 = new FakeSignal("signal2", _ => { invocations2++; return Task.CompletedTask; });
        var filter = new NamedFilter("signal1", allow: false);
        var coord = CreateCoordinator([signal1, signal2], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.Filters = [filter];
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        invocations1.Should().Be(0);
        invocations2.Should().Be(1);
        result.Results.Should().Contain(r => r.Name == "signal1" && r.Status == IgnitionSignalStatus.Skipped);
        result.Results.Should().Contain(r => r.Name == "signal2" && r.Status == IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task MultipleFilters_FirstDenies_SecondNotCalled()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var filter1 = new DenyAllFilter();
        var filter2 = new AllowAllFilter();
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Filters = [filter1, filter2];
        });

        // act
        await coord.WaitAllAsync();

        // assert
        filter1.InvocationCount.Should().Be(1);
        filter2.InvocationCount.Should().Be(0); // Short-circuited
    }

    private sealed class AllowAllFilter : IIgnitionSignalFilter
    {
        public int InvocationCount { get; private set; }

        public ValueTask<bool> ShouldExecuteAsync(IIgnitionSignal signal, CancellationToken cancellationToken)
        {
            InvocationCount++;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class DenyAllFilter : IIgnitionSignalFilter
    {
        public int InvocationCount { get; private set; }

        public ValueTask<bool> ShouldExecuteAsync(IIgnitionSignal signal, CancellationToken cancellationToken)
        {
            InvocationCount++;
            return ValueTask.FromResult(false);
        }
    }

    private sealed class NamedFilter(string targetName, bool allow) : IIgnitionSignalFilter
    {
        public ValueTask<bool> ShouldExecuteAsync(IIgnitionSignal signal, CancellationToken cancellationToken)
        {
            if (signal.Name == targetName)
            {
                return ValueTask.FromResult(allow);
            }

            return ValueTask.FromResult(true);
        }
    }
}

public class ValidatorTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var factories = signals.Select(s => new TestSignalFactory(s)).ToList();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);
    }

    [Fact]
    public async Task Validator_Passes_AllSignalsExecute()
    {
        // arrange
        var invocations = 0;
        var signal = new FakeSignal("test", _ => { invocations++; return Task.CompletedTask; });
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Validators = [new AlwaysPassValidator()];
        });

        // act
        await coord.WaitAllAsync();

        // assert
        invocations.Should().Be(1);
    }

    [Fact]
    public async Task Validator_Fails_ThrowsIgnitionValidationException()
    {
        // arrange
        var invocations = 0;
        var signal = new FakeSignal("test", _ => { invocations++; return Task.CompletedTask; });
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Validators = [new AlwaysFailValidator("Missing required configuration")];
        });

        // act & assert
        var ex = await Assert.ThrowsAsync<IgnitionValidationException>(() => coord.WaitAllAsync());
        ex.ValidationErrors.Should().ContainSingle(e => e == "Missing required configuration");
        invocations.Should().Be(0);
    }

    [Fact]
    public async Task MultipleValidators_BothFail_CollectsAllErrors()
    {
        // arrange
        var signal = new FakeSignal("test", _ => Task.CompletedTask);
        var coord = CreateCoordinator([signal], o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
            o.Validators =
            [
                new AlwaysFailValidator("Error A"),
                new AlwaysFailValidator("Error B")
            ];
        });

        // act & assert
        var ex = await Assert.ThrowsAsync<IgnitionValidationException>(() => coord.WaitAllAsync());
        ex.ValidationErrors.Should().HaveCount(2);
        ex.ValidationErrors.Should().Contain("Error A");
        ex.ValidationErrors.Should().Contain("Error B");
    }

    [Fact]
    public void IgnitionValidationException_Message_ContainsErrors()
    {
        // arrange & act
        var ex = new IgnitionValidationException(["Error A", "Error B"]);

        // assert
        ex.Message.Should().Contain("Error A");
        ex.Message.Should().Contain("Error B");
        ex.ValidationErrors.Should().HaveCount(2);
    }

    private sealed class AlwaysPassValidator : IIgnitionValidator
    {
        public ValueTask<IReadOnlyList<string>?> ValidateAsync(
            IReadOnlyList<IIgnitionSignalFactory> factories, IgnitionOptions options, CancellationToken ct)
        {
            return ValueTask.FromResult<IReadOnlyList<string>?>(null);
        }
    }

    private sealed class AlwaysFailValidator(string error) : IIgnitionValidator
    {
        public ValueTask<IReadOnlyList<string>?> ValidateAsync(
            IReadOnlyList<IIgnitionSignalFactory> factories, IgnitionOptions options, CancellationToken ct)
        {
            return ValueTask.FromResult<IReadOnlyList<string>?>([error]);
        }
    }
}

public class AsyncIgnitionPolicyTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Microsoft.Extensions.Options.Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var factories = signals.Select(s => new TestSignalFactory(s)).ToList();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        return new IgnitionCoordinator(factories, serviceProvider, optionsWrapper, logger);
    }

    [Fact]
    public async Task AsyncPolicy_ShouldContinueAsync_Called_InsteadOfSync()
    {
        // arrange
        var signal1 = new FaultingSignal("failing", new InvalidOperationException("boom"));
        var signal2 = new FakeSignal("second", _ => Task.CompletedTask);
        var policy = new TrackingAsyncPolicy(continueOnFailure: false);
        var coord = CreateCoordinator([signal1, signal2], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        AggregateException? ex = null;
        try
        {
            await coord.WaitAllAsync();
        }
        catch (AggregateException a)
        {
            ex = a;
        }

        // assert
        policy.AsyncInvocationCount.Should().BeGreaterThan(0);
        policy.SyncInvocationCount.Should().Be(0); // Async path used, sync not invoked
    }

    [Fact]
    public async Task AsyncPolicy_CanContinue_AllSignalsComplete()
    {
        // arrange
        var invocations = 0;
        var signal1 = new FaultingSignal("failing", new InvalidOperationException("boom"));
        var signal2 = new FakeSignal("second", _ => { invocations++; return Task.CompletedTask; });
        var policy = new TrackingAsyncPolicy(continueOnFailure: true);
        var coord = CreateCoordinator([signal1, signal2], o =>
        {
            o.ExecutionMode = IgnitionExecutionMode.Sequential;
            o.CustomPolicy = policy;
            o.GlobalTimeout = TimeSpan.FromSeconds(5);
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        invocations.Should().Be(1); // Continued despite failure
        result.Results.Should().HaveCount(2);
    }

    private sealed class TrackingAsyncPolicy(bool continueOnFailure) : IAsyncIgnitionPolicy
    {
        public int AsyncInvocationCount { get; private set; }
        public int SyncInvocationCount { get; private set; }

        public bool ShouldContinue(IgnitionPolicyContext context)
        {
            SyncInvocationCount++;
            return continueOnFailure || context.SignalResult.Status == IgnitionSignalStatus.Succeeded;
        }

        public ValueTask<bool> ShouldContinueAsync(IgnitionPolicyContext context, CancellationToken cancellationToken)
        {
            AsyncInvocationCount++;
            return ValueTask.FromResult(
                continueOnFailure || context.SignalResult.Status == IgnitionSignalStatus.Succeeded);
        }
    }
}

public class ConditionalRegistrationTests
{
    [Fact]
    public void AddIgnitionFromTaskIf_WhenTrue_RegistersSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();

        // act
        services.AddIgnitionFromTaskIf(
            condition: true,
            name: "conditional-signal",
            readyTaskFactory: _ => Task.CompletedTask);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Should().Contain(f => f.Name == "conditional-signal");
    }

    [Fact]
    public void AddIgnitionFromTaskIf_WhenFalse_DoesNotRegisterSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();

        // act
        services.AddIgnitionFromTaskIf(
            condition: false,
            name: "conditional-signal",
            readyTaskFactory: _ => Task.CompletedTask);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Should().NotContain(f => f.Name == "conditional-signal");
    }

    [Fact]
    public void AddIgnitionSignalIf_WhenTrue_RegistersSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        var signal = new FakeSignal("my-signal", _ => Task.CompletedTask);

        // act
        services.AddIgnitionSignalIf(condition: true, signal);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Should().Contain(f => f.Name == "my-signal");
    }

    [Fact]
    public void AddIgnitionSignalIf_WhenFalse_DoesNotRegisterSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        var signal = new FakeSignal("my-signal", _ => Task.CompletedTask);

        // act
        services.AddIgnitionSignalIf(condition: false, signal);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Should().NotContain(f => f.Name == "my-signal");
    }

    [Fact]
    public void AddIgnitionFromTaskIf_WithTask_WhenTrue_RegistersSignal()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddIgnition();
        var task = Task.CompletedTask;

        // act
        services.AddIgnitionFromTaskIf(condition: true, name: "from-task", readyTask: task);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>().ToList();
        factories.Should().Contain(f => f.Name == "from-task");
    }
}
