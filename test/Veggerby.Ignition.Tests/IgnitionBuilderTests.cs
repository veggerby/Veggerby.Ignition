using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Xunit;

namespace Veggerby.Ignition.Tests;

public class IgnitionBuilderTests
{
    private static IServiceCollection CreateServicesWithLogging()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        return services;
    }

    [Fact]
    public async Task AddSimpleIgnition_WithWebApiProfile_AppliesCorrectDefaults()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal("test", ct => Task.CompletedTask));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.GlobalTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.Policy.Should().Be(IgnitionPolicy.BestEffort);
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Parallel);
        options.EnableTracing.Should().BeTrue();

        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();
        coordinator.Should().NotBeNull();
    }

    [Fact]
    public async Task AddSimpleIgnition_WithWorkerProfile_AppliesCorrectDefaults()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWorkerProfile()
            .AddSignal("test", ct => Task.CompletedTask));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.GlobalTimeout.Should().Be(TimeSpan.FromSeconds(60));
        options.Policy.Should().Be(IgnitionPolicy.FailFast);
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Parallel);
        options.EnableTracing.Should().BeTrue();
    }

    [Fact]
    public async Task AddSimpleIgnition_WithCliProfile_AppliesCorrectDefaults()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseCliProfile()
            .AddSignal("test", ct => Task.CompletedTask));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.GlobalTimeout.Should().Be(TimeSpan.FromSeconds(15));
        options.Policy.Should().Be(IgnitionPolicy.FailFast);
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Sequential);
        options.EnableTracing.Should().BeFalse();
    }

    [Fact]
    public async Task AddSimpleIgnition_WithMultipleSignals_RegistersAllSuccessfully()
    {
        // arrange
        var services = CreateServicesWithLogging();
        var signal1Executed = false;
        var signal2Executed = false;

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal("signal1", async ct =>
            {
                signal1Executed = true;
                await Task.CompletedTask;
            })
            .AddSignal("signal2", async ct =>
            {
                signal2Executed = true;
                await Task.CompletedTask;
            }));

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // assert
        await coordinator.WaitAllAsync();

        signal1Executed.Should().BeTrue();
        signal2Executed.Should().BeTrue();

        var result = await coordinator.GetResultAsync();
        result.Results.Count.Should().Be(2);
        result.Results.All(r => r.Status == IgnitionSignalStatus.Succeeded).Should().BeTrue();
    }

    [Fact]
    public async Task AddSimpleIgnition_WithCustomTimeout_OverridesProfileDefaults()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithGlobalTimeout(TimeSpan.FromSeconds(45))
            .AddSignal("test", ct => Task.CompletedTask));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.GlobalTimeout.Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public async Task AddSimpleIgnition_WithTracingDisabled_SetsTracingToFalse()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithTracing(false)
            .AddSignal("test", ct => Task.CompletedTask));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.EnableTracing.Should().BeFalse();
    }

    [Fact]
    public async Task AddSimpleIgnition_WithAdvancedConfiguration_AppliesCustomSettings()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal("test", ct => Task.CompletedTask)
            .ConfigureAdvanced(options =>
            {
                options.MaxDegreeOfParallelism = 5;
                options.CancelOnGlobalTimeout = true;
            }));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.MaxDegreeOfParallelism.Should().Be(5);
        options.CancelOnGlobalTimeout.Should().BeTrue();
    }

    [Fact]
    public async Task AddSimpleIgnition_WithSignalFromTask_RegistersCorrectly()
    {
        // arrange
        var services = CreateServicesWithLogging();
        var tcs = new TaskCompletionSource();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal("test", tcs.Task));

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // Complete the signal
        tcs.SetResult();

        // assert
        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();
        result.Results.Count.Should().Be(1);
        result.Results.First().Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task AddSimpleIgnition_WithCustomSignalType_RegistersCorrectly()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal<TestCustomSignal>());

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // assert
        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();
        result.Results.Count.Should().Be(1);
        result.Results.First().Name.Should().Be("custom-signal");
        result.Results.First().Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public async Task AddSimpleIgnition_WithCustomSignalInstance_RegistersCorrectly()
    {
        // arrange
        var services = CreateServicesWithLogging();
        var customSignal = new TestCustomSignal();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal(customSignal));

        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();

        // assert
        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();
        result.Results.Count.Should().Be(1);
        result.Results.First().Name.Should().Be("custom-signal");
    }

    [Fact]
    public async Task AddSimpleIgnition_WithPerSignalTimeout_AppliesTimeout()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal("test", ct => Task.CompletedTask, TimeSpan.FromSeconds(3)));

        var provider = services.BuildServiceProvider();
        var signals = provider.GetServices<IIgnitionSignal>().ToList();

        // assert
        signals.Count.Should().Be(1);
        signals.First().Timeout.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task AddSimpleIgnition_WithDefaultSignalTimeout_AppliesDefaultToSignalsWithoutExplicitTimeout()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithDefaultSignalTimeout(TimeSpan.FromSeconds(7))
            .AddSignal("test1", ct => Task.CompletedTask)
            .AddSignal("test2", ct => Task.CompletedTask, TimeSpan.FromSeconds(3)));

        var provider = services.BuildServiceProvider();
        var signals = provider.GetServices<IIgnitionSignal>().ToList();

        // assert
        signals.Count.Should().Be(2);
        var test1 = signals.First(s => s.Name == "test1");
        var test2 = signals.First(s => s.Name == "test2");

        test1.Timeout.Should().Be(TimeSpan.FromSeconds(7));
        test2.Timeout.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void AddSimpleIgnition_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddSimpleIgnition(null!));
    }

    [Fact]
    public void AddSimpleIgnition_WithNegativeGlobalTimeout_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithGlobalTimeout(TimeSpan.FromSeconds(-5))));
    }

    [Fact]
    public void AddSimpleIgnition_WithNegativeDefaultSignalTimeout_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithDefaultSignalTimeout(TimeSpan.FromSeconds(-3))));
    }

    [Fact]
    public async Task AddSimpleIgnition_NoProfile_UsesDefaultSettings()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .AddSignal("test", ct => Task.CompletedTask));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert - should use underlying AddIgnition defaults
        options.Policy.Should().Be(IgnitionPolicy.BestEffort);
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Parallel);
    }

    private sealed class TestCustomSignal : IIgnitionSignal
    {
        public string Name => "custom-signal";
        public TimeSpan? Timeout => TimeSpan.FromSeconds(5);

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
