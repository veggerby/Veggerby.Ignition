namespace Veggerby.Ignition.Tests;

public class IgnitionBundleOptionsTests
{
    [Fact]
    public void DefaultTimeout_DefaultsToNull()
    {
        // arrange & act
        var options = new IgnitionBundleOptions();

        // assert
        options.DefaultTimeout.Should().BeNull();
    }

    [Fact]
    public void DefaultTimeout_CanBeSet()
    {
        // arrange
        var options = new IgnitionBundleOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.DefaultTimeout = timeout;

        // assert
        options.DefaultTimeout.Should().Be(timeout);
    }

    [Fact]
    public void Policy_DefaultsToNull()
    {
        // arrange & act
        var options = new IgnitionBundleOptions();

        // assert
        options.Policy.Should().BeNull();
    }

    [Fact]
    public void Policy_CanBeSet()
    {
        // arrange
        var options = new IgnitionBundleOptions();

        // act
        options.Policy = IgnitionPolicy.FailFast;

        // assert
        options.Policy.Should().Be(IgnitionPolicy.FailFast);
    }

    [Fact]
    public void Policy_CanBeSetToAllPolicyValues()
    {
        // arrange
        var options = new IgnitionBundleOptions();

        // act & assert
        options.Policy = IgnitionPolicy.BestEffort;
        options.Policy.Should().Be(IgnitionPolicy.BestEffort);

        options.Policy = IgnitionPolicy.FailFast;
        options.Policy.Should().Be(IgnitionPolicy.FailFast);

        options.Policy = IgnitionPolicy.ContinueOnTimeout;
        options.Policy.Should().Be(IgnitionPolicy.ContinueOnTimeout);
    }

    [Fact]
    public void EnableScopedCancellation_DefaultsToFalse()
    {
        // arrange & act
        var options = new IgnitionBundleOptions();

        // assert
        options.EnableScopedCancellation.Should().BeFalse();
    }

    [Fact]
    public void EnableScopedCancellation_CanBeSetToTrue()
    {
        // arrange
        var options = new IgnitionBundleOptions();

        // act
        options.EnableScopedCancellation = true;

        // assert
        options.EnableScopedCancellation.Should().BeTrue();
    }

    [Fact]
    public void CancellationScope_DefaultsToNull()
    {
        // arrange & act
        var options = new IgnitionBundleOptions();

        // assert
        options.CancellationScope.Should().BeNull();
    }

    [Fact]
    public void CancellationScope_CanBeSet()
    {
        // arrange
        var options = new IgnitionBundleOptions();
        var scope = new CancellationScope("test-scope");

        // act
        options.CancellationScope = scope;

        // assert
        options.CancellationScope.Should().BeSameAs(scope);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // arrange
        var timeout = TimeSpan.FromMinutes(2);
        var policy = IgnitionPolicy.FailFast;
        var scope = new CancellationScope("bundle-scope");

        // act
        var options = new IgnitionBundleOptions
        {
            DefaultTimeout = timeout,
            Policy = policy,
            EnableScopedCancellation = true,
            CancellationScope = scope
        };

        // assert
        options.DefaultTimeout.Should().Be(timeout);
        options.Policy.Should().Be(policy);
        options.EnableScopedCancellation.Should().BeTrue();
        options.CancellationScope.Should().BeSameAs(scope);
    }
}
