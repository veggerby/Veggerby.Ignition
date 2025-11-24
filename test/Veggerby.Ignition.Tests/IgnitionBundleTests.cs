using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Veggerby.Ignition.Bundles;
using Xunit;

namespace Veggerby.Ignition.Tests;

public class IgnitionBundleTests
{
    [Fact]
    public void AddIgnitionBundle_RegistersSignalsFromBundle()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle = new TestBundle("test-bundle", 3);

        // act
        services.AddIgnitionBundle(bundle);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(3);
        signals.Should().Contain(s => s.Name == "test-bundle-signal-0");
        signals.Should().Contain(s => s.Name == "test-bundle-signal-1");
        signals.Should().Contain(s => s.Name == "test-bundle-signal-2");
    }

    [Fact]
    public void AddIgnitionBundle_AppliesBundleOptions()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle = new TestBundle("test-bundle", 2);

        // act
        services.AddIgnitionBundle(bundle, opts =>
        {
            opts.DefaultTimeout = TimeSpan.FromSeconds(10);
        });

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().AllSatisfy(s => s.Timeout.Should().Be(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void AddIgnitionBundle_GenericOverload_RegistersSignals()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnitionBundle<TestBundle>();

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(2);
        signals.Should().Contain(s => s.Name == "default-bundle-signal-0");
        signals.Should().Contain(s => s.Name == "default-bundle-signal-1");
    }

    [Fact]
    public void AddIgnitionBundles_RegistersMultipleBundles()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle1 = new TestBundle("bundle1", 2);
        var bundle2 = new TestBundle("bundle2", 3);

        // act
        services.AddIgnitionBundles(bundle1, bundle2);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(5);
        signals.Should().Contain(s => s.Name == "bundle1-signal-0");
        signals.Should().Contain(s => s.Name == "bundle2-signal-2");
    }

    [Fact]
    public async Task HttpDependencyBundle_SingleEndpoint_RegistersSignal()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle = new HttpDependencyBundle("https://example.com", TimeSpan.FromSeconds(5));

        // act
        services.AddIgnitionBundle(bundle);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(1);
        signals[0].Name.Should().Be("http:https://example.com");
        signals[0].Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void HttpDependencyBundle_MultipleEndpoints_RegistersMultipleSignals()
    {
        // arrange
        var services = new ServiceCollection();
        var endpoints = new[] { "https://api1.example.com", "https://api2.example.com" };
        var bundle = new HttpDependencyBundle(endpoints, TimeSpan.FromSeconds(10));

        // act
        services.AddIgnitionBundle(bundle);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(2);
        signals.Should().Contain(s => s.Name == "http:https://api1.example.com");
        signals.Should().Contain(s => s.Name == "http:https://api2.example.com");
    }

    [Fact]
    public void HttpDependencyBundle_EmptyEndpoints_ThrowsArgumentException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentException>(() => new HttpDependencyBundle(Array.Empty<string>()));
    }

    [Fact]
    public void HttpDependencyBundle_NullEndpoints_ThrowsArgumentNullException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentNullException>(() => new HttpDependencyBundle((string[])null!));
    }

    [Fact]
    public async Task DatabaseTrioBundle_AllPhases_RegistersThreeSignals()
    {
        // arrange
        var services = new ServiceCollection();

        var bundle = new DatabaseTrioBundle(
            "test-db",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TimeSpan.FromSeconds(5));

        // act
        services.AddIgnitionBundle(bundle);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(3);
        signals.Should().Contain(s => s.Name == "test-db:connect");
        signals.Should().Contain(s => s.Name == "test-db:validate-schema");
        signals.Should().Contain(s => s.Name == "test-db:warmup");

        // verify timeouts
        signals.Should().AllSatisfy(s => s.Timeout.Should().Be(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void DatabaseTrioBundle_OnlyConnect_RegistersSingleSignal()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle = new DatabaseTrioBundle(
            "minimal-db",
            _ => Task.CompletedTask);

        // act
        services.AddIgnitionBundle(bundle);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(1);
        signals[0].Name.Should().Be("minimal-db:connect");
    }

    [Fact]
    public void DatabaseTrioBundle_ConnectAndWarmup_RegistersTwoSignals()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle = new DatabaseTrioBundle(
            "partial-db",
            _ => Task.CompletedTask,
            warmupFactory: _ => Task.CompletedTask);

        // act
        services.AddIgnitionBundle(bundle);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();
        signals.Should().HaveCount(2);
        signals.Should().Contain(s => s.Name == "partial-db:connect");
        signals.Should().Contain(s => s.Name == "partial-db:warmup");
    }

    [Fact]
    public void DatabaseTrioBundle_NullDatabaseName_ThrowsArgumentNullException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentNullException>(() => new DatabaseTrioBundle(
            null!,
            _ => Task.CompletedTask));
    }

    [Fact]
    public void DatabaseTrioBundle_EmptyDatabaseName_ThrowsArgumentException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentException>(() => new DatabaseTrioBundle(
            "",
            _ => Task.CompletedTask));
    }

    [Fact]
    public void DatabaseTrioBundle_NullConnectFactory_ThrowsArgumentNullException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentNullException>(() => new DatabaseTrioBundle(
            "test-db",
            null!));
    }

    [Fact]
    public async Task Bundle_WithCoordinator_ExecutesSignals()
    {
        // arrange
        var services = new ServiceCollection();
        var executed = new List<string>();

        var bundle = new TestExecutionBundle("exec-bundle", executed);
        services.AddIgnitionBundle(bundle);

        services.AddIgnition(opts =>
        {
            opts.GlobalTimeout = TimeSpan.FromSeconds(5);
            opts.Policy = IgnitionPolicy.BestEffort;
        });

        // Mock logger for coordinator
        services.AddSingleton(Substitute.For<ILogger<IgnitionCoordinator>>());

        // act
        var sp = services.BuildServiceProvider();
        var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();
        await coordinator.WaitAllAsync();

        // assert
        executed.Should().HaveCount(3);
        executed.Should().Contain("exec-bundle-0");
        executed.Should().Contain("exec-bundle-1");
        executed.Should().Contain("exec-bundle-2");

        var result = await coordinator.GetResultAsync();
        result.Results.Should().HaveCount(3);
        result.Results.Should().AllSatisfy(r => r.Status.Should().Be(IgnitionSignalStatus.Succeeded));
    }

    [Fact]
    public async Task BundleOptions_OverrideTimeout_AppliedToSignals()
    {
        // arrange
        var services = new ServiceCollection();
        var bundle = new TestBundle("timeout-bundle", 2);

        services.AddIgnitionBundle(bundle, opts =>
        {
            opts.DefaultTimeout = TimeSpan.FromSeconds(15);
        });

        services.AddIgnition(opts =>
        {
            opts.GlobalTimeout = TimeSpan.FromSeconds(30);
        });

        // act
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>().ToList();

        // assert
        signals.Should().AllSatisfy(s => s.Timeout.Should().Be(TimeSpan.FromSeconds(15)));
    }

    // Test helper bundles
    private sealed class TestBundle : IIgnitionBundle
    {
        private readonly string _prefix;
        private readonly int _signalCount;

        public TestBundle(string prefix, int signalCount)
        {
            _prefix = prefix;
            _signalCount = signalCount;
        }

        public TestBundle()
        {
            _prefix = "default-bundle";
            _signalCount = 2;
        }

        public string Name => _prefix;

        public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
        {
            var options = new IgnitionBundleOptions();
            configure?.Invoke(options);

            for (int i = 0; i < _signalCount; i++)
            {
                var signal = new TestBundleSignal($"{_prefix}-signal-{i}", options.DefaultTimeout);
                services.AddIgnitionSignal(signal);
            }
        }

        private sealed class TestBundleSignal : IIgnitionSignal
        {
            public TestBundleSignal(string name, TimeSpan? timeout)
            {
                Name = name;
                Timeout = timeout;
            }

            public string Name { get; }
            public TimeSpan? Timeout { get; }

            public Task WaitAsync(CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }

    private sealed class TestExecutionBundle : IIgnitionBundle
    {
        private readonly string _prefix;
        private readonly List<string> _executed;

        public TestExecutionBundle(string prefix, List<string> executed)
        {
            _prefix = prefix;
            _executed = executed;
        }

        public string Name => _prefix;

        public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
        {
            for (int i = 0; i < 3; i++)
            {
                var signalName = $"{_prefix}-{i}";
                services.AddIgnitionFromTask(signalName, ct =>
                {
                    _executed.Add(signalName);
                    return Task.CompletedTask;
                });
            }
        }
    }
}
