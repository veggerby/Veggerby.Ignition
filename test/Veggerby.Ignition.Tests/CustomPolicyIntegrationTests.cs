using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition.Tests;

public class CustomPolicyIntegrationTests
{
    [Fact]
    public void AddIgnitionPolicy_WithInstance_ConfiguresCustomPolicy()
    {
        // arrange
        var services = new ServiceCollection();
        var policy = new TestPolicy();

        // act
        services.AddIgnition();
        services.AddIgnitionPolicy(policy);

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().BeSameAs(policy);
    }

    [Fact]
    public void AddIgnitionPolicy_WithFactory_ConfiguresCustomPolicy()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnition();
        services.AddIgnitionPolicy(sp => new TestPolicy());

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().NotBeNull();
        options.CustomPolicy.Should().BeOfType<TestPolicy>();
    }

    [Fact]
    public void AddIgnitionPolicy_WithType_ConfiguresCustomPolicy()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddIgnition();
        services.AddIgnitionPolicy<TestPolicy>();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().NotBeNull();
        options.CustomPolicy.Should().BeOfType<TestPolicy>();
    }

    [Fact]
    public void SimpleMode_WithCustomPolicy_Instance_ConfiguresPolicy()
    {
        // arrange
        var services = new ServiceCollection();
        var policy = new TestPolicy();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithCustomPolicy(policy));

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().BeSameAs(policy);
    }

    [Fact]
    public void SimpleMode_WithCustomPolicy_Type_ConfiguresPolicy()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithCustomPolicy<TestPolicy>());

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().NotBeNull();
        options.CustomPolicy.Should().BeOfType<TestPolicy>();
    }

    [Fact]
    public void SimpleMode_WithCustomPolicy_Factory_ConfiguresPolicy()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .WithCustomPolicy(sp => new TestPolicy()));

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().NotBeNull();
        options.CustomPolicy.Should().BeOfType<TestPolicy>();
    }

    [Fact]
    public async Task SimpleMode_WithCustomPolicy_UsedByCoordinator()
    {
        // arrange
        var services = new ServiceCollection();
        var policy = new TestPolicy(shouldContinue: false);
        
        // Add logger manually to satisfy coordinator dependencies
        services.AddSingleton<ILogger<IgnitionCoordinator>>(Substitute.For<ILogger<IgnitionCoordinator>>());

        services.AddSimpleIgnition(ignition => ignition
            .UseWebApiProfile()
            .AddSignal("s1", ct => Task.CompletedTask)
            .AddSignal("s2", _ => Task.FromException(new InvalidOperationException("fail")))
            .WithCustomPolicy(policy));

        var sp = services.BuildServiceProvider();
        var coordinator = sp.GetRequiredService<IIgnitionCoordinator>();

        // act
        AggregateException? ex = null;
        try { await coordinator.WaitAllAsync(); } catch (AggregateException e) { ex = e; }

        // assert
        ex.Should().NotBeNull();
        policy.InvocationCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void AddIgnitionPolicy_WithFactory_ResolvesFromDI()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddSingleton<PolicyDependency>();

        // act
        services.AddIgnition();
        services.AddIgnitionPolicy(sp =>
        {
            var dep = sp.GetRequiredService<PolicyDependency>();
            return new PolicyWithDependency(dep);
        });

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<IgnitionOptions>>().Value;

        // assert
        options.CustomPolicy.Should().NotBeNull();
        options.CustomPolicy.Should().BeOfType<PolicyWithDependency>();
        var policyWithDep = (PolicyWithDependency)options.CustomPolicy!;
        policyWithDep.Dependency.Should().NotBeNull();
    }

    // Test helper classes
    private sealed class TestPolicy : IIgnitionPolicy
    {
        private readonly bool _shouldContinue;
        private int _invocationCount;

        public TestPolicy(bool shouldContinue = true)
        {
            _shouldContinue = shouldContinue;
        }

        public int InvocationCount => _invocationCount;

        public bool ShouldContinue(IgnitionPolicyContext context)
        {
            Interlocked.Increment(ref _invocationCount);
            return _shouldContinue;
        }
    }

    private sealed class PolicyDependency
    {
    }

    private sealed class PolicyWithDependency : IIgnitionPolicy
    {
        public PolicyDependency Dependency { get; }

        public PolicyWithDependency(PolicyDependency dependency)
        {
            Dependency = dependency;
        }

        public bool ShouldContinue(IgnitionPolicyContext context) => true;
    }
}
