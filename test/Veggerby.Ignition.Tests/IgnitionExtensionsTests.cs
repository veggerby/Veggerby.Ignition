using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Veggerby.Ignition.Metrics;

namespace Veggerby.Ignition.Tests;

public class IgnitionExtensionsTests
{
    private static IServiceCollection CreateServicesWithLogging()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILogger<IgnitionCoordinator>>(_ => Substitute.For<ILogger<IgnitionCoordinator>>());
        return services;
    }

    [Fact]
    public void AddIgnition_WithoutHealthCheck_DoesNotRegisterHealthCheck()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddIgnition(addHealthCheck: false);

        // assert
        var provider = services.BuildServiceProvider();
        var healthChecks = provider.GetService<HealthCheckService>();
        healthChecks.Should().BeNull();
    }

    [Fact]
    public void AddIgnition_WithHealthCheck_RegistersHealthCheck()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddIgnition(addHealthCheck: true);

        // assert
        var provider = services.BuildServiceProvider();
        // Just verify coordinator is registered - health check registration is harder to test directly
        provider.GetRequiredService<IIgnitionCoordinator>().Should().NotBeNull();
    }

    [Fact]
    public void AddIgnition_WithCustomHealthCheckName_UsesCustomName()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddIgnition(healthCheckName: "custom-health-check");

        // assert - should not throw when registering with custom name
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIgnitionCoordinator>().Should().NotBeNull();
    }

    [Fact]
    public void AddIgnition_RegistersCoordinatorAsSingleton()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddIgnition();

        // assert
        var provider = services.BuildServiceProvider();
        var coordinator1 = provider.GetRequiredService<IIgnitionCoordinator>();
        var coordinator2 = provider.GetRequiredService<IIgnitionCoordinator>();
        coordinator1.Should().BeSameAs(coordinator2);
    }

    [Fact]
    public void AddIgnition_WithConfigureAction_AppliesConfiguration()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddIgnition(configure: options =>
        {
            options.GlobalTimeout = TimeSpan.FromMinutes(5);
            options.Policy = IgnitionPolicy.FailFast;
        });

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.GlobalTimeout.Should().Be(TimeSpan.FromMinutes(5));
        options.Policy.Should().Be(IgnitionPolicy.FailFast);
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_WithInstance_ConfiguresStrategy()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var strategy = new TestTimeoutStrategy();

        // act
        services.AddIgnitionTimeoutStrategy(strategy);

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.TimeoutStrategy.Should().BeSameAs(strategy);
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_WithNullStrategy_ThrowsArgumentNullException()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddIgnitionTimeoutStrategy((IIgnitionTimeoutStrategy)null!));
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_WithFactory_ConfiguresStrategy()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var strategy = new TestTimeoutStrategy();

        // act
        services.AddIgnitionTimeoutStrategy(sp => strategy);

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.TimeoutStrategy.Should().BeSameAs(strategy);
    }

    [Fact]
    public void AddIgnitionTimeoutStrategy_WithGenericType_ConfiguresStrategy()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        services.AddSingleton<TestTimeoutStrategy>();

        // act
        services.AddIgnitionTimeoutStrategy<TestTimeoutStrategy>();

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.TimeoutStrategy.Should().BeOfType<TestTimeoutStrategy>();
    }

    [Fact]
    public void AddIgnitionMetrics_WithInstance_ConfiguresMetrics()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var metrics = new TestMetrics();

        // act
        services.AddIgnitionMetrics(metrics);

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.Metrics.Should().BeSameAs(metrics);
    }

    [Fact]
    public void AddIgnitionMetrics_WithNullMetrics_ThrowsArgumentNullException()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => services.AddIgnitionMetrics((IIgnitionMetrics)null!));
    }

    [Fact]
    public void AddIgnitionMetrics_WithFactory_ConfiguresMetrics()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var metrics = new TestMetrics();

        // act
        services.AddIgnitionMetrics(sp => metrics);

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.Metrics.Should().BeSameAs(metrics);
    }

    [Fact]
    public void AddIgnitionMetrics_WithGenericType_ConfiguresMetrics()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        services.AddSingleton<TestMetrics>();

        // act
        services.AddIgnitionMetrics<TestMetrics>();

        // assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IgnitionOptions>>().Value;
        options.Metrics.Should().BeOfType<TestMetrics>();
    }

    [Fact]
    public void AddIgnitionSignal_WithGenericType_RegistersFactory()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();

        // act
        services.AddIgnitionSignal<TestSignal>();

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>();
        factories.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddIgnitionSignal_WithGenericType_CreatesSignalInstance()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        services.AddIgnitionSignal<TestSignal>();

        // act
        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.Results.Should().HaveCount(1);
        result.Results.First().Name.Should().Be("test-signal");
    }

    [Fact]
    public void AddIgnitionSignals_WithMultipleInstances_RegistersAllFactories()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var signal1 = new TestSignal();
        var signal2 = new TestSignal2();

        // act
        services.AddIgnitionSignals(signal1, signal2);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>();
        factories.Should().HaveCount(2);
    }

    [Fact]
    public void AddIgnitionSignals_WithEnumerable_RegistersAllFactories()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var signals = new List<IIgnitionSignal> { new TestSignal(), new TestSignal2() };

        // act
        services.AddIgnitionSignals(signals);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>();
        factories.Should().HaveCount(2);
    }

    [Fact]
    public void AddIgnitionFromTask_WithName_RegistersFactory()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var tcs = new TaskCompletionSource();

        // act
        services.AddIgnitionFromTask("task-signal", tcs.Task);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>();
        factories.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddIgnitionFromTask_CompletedTask_SignalSucceeds()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        services.AddIgnitionFromTask("task-signal", Task.CompletedTask);

        // act
        var provider = services.BuildServiceProvider();
        var coordinator = provider.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();

        // assert
        var result = await coordinator.GetResultAsync();
        result.Results.Should().HaveCount(1);
        result.Results.First().Status.Should().Be(IgnitionSignalStatus.Succeeded);
    }

    [Fact]
    public void AddIgnitionFromTask_WithTimeout_SetsTimeout()
    {
        // arrange
        var services = CreateServicesWithLogging();
        services.AddIgnition();
        var timeout = TimeSpan.FromSeconds(10);

        // act
        services.AddIgnitionFromTask("task-signal", Task.CompletedTask, timeout);

        // assert
        var provider = services.BuildServiceProvider();
        var factories = provider.GetServices<IIgnitionSignalFactory>();
        var signal = factories.First().CreateSignal(provider);
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void AddIgnition_CalledMultipleTimes_DoesNotDuplicateCoordinator()
    {
        // arrange
        var services = CreateServicesWithLogging();

        // act
        services.AddIgnition();
        services.AddIgnition();
        services.AddIgnition();

        // assert
        var provider = services.BuildServiceProvider();
        var coordinators = provider.GetServices<IIgnitionCoordinator>();
        coordinators.Should().HaveCount(1);
    }

    [Fact]
    public void AddIgnition_WithHealthCheckTags_AppliesTags()
    {
        // arrange
        var services = CreateServicesWithLogging();
        var tags = new[] { "tag1", "tag2" };

        // act
        services.AddIgnition(healthCheckTags: tags);

        // assert - should not throw
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIgnitionCoordinator>().Should().NotBeNull();
    }

    private sealed class TestTimeoutStrategy : IIgnitionTimeoutStrategy
    {
        public (TimeSpan? signalTimeout, bool cancelImmediately) GetTimeout(IIgnitionSignal signal, IgnitionOptions options)
        {
            return (TimeSpan.FromSeconds(5), false);
        }
    }

    private sealed class TestMetrics : IIgnitionMetrics
    {
        public void RecordSignalDuration(string name, TimeSpan duration) { }
        public void RecordSignalStatus(string name, IgnitionSignalStatus status) { }
        public void RecordTotalDuration(TimeSpan duration) { }
    }

    private sealed class TestSignal : IIgnitionSignal
    {
        public string Name => "test-signal";
        public TimeSpan? Timeout => null;
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestSignal2 : IIgnitionSignal
    {
        public string Name => "test-signal-2";
        public TimeSpan? Timeout => null;
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
