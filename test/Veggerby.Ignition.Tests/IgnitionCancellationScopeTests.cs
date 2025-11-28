using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Veggerby.Ignition.Tests;

public class IgnitionCancellationScopeTests
{
    private static IgnitionCoordinator CreateCoordinator(IEnumerable<IIgnitionSignal> signals, Action<IgnitionOptions>? configure = null)
    {
        var opts = new IgnitionOptions();
        configure?.Invoke(opts);
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        return new IgnitionCoordinator(signals, optionsWrapper, logger);
    }

    #region CancellationScope Tests

    [Fact]
    public void CancellationScope_Create_SetsNameCorrectly()
    {
        // arrange & act
        using var scope = new CancellationScope("test-scope");

        // assert
        scope.Name.Should().Be("test-scope");
        scope.Parent.Should().BeNull();
        scope.IsCancelled.Should().BeFalse();
        scope.CancellationReason.Should().Be(CancellationReason.None);
        scope.TriggeringSignalName.Should().BeNull();
    }

    [Fact]
    public void CancellationScope_Cancel_SetsReasonAndTriggeringSignal()
    {
        // arrange
        using var scope = new CancellationScope("test-scope");

        // act
        scope.Cancel(CancellationReason.BundleCancelled, "failing-signal");

        // assert
        scope.IsCancelled.Should().BeTrue();
        scope.CancellationReason.Should().Be(CancellationReason.BundleCancelled);
        scope.TriggeringSignalName.Should().Be("failing-signal");
        scope.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void CancellationScope_CreateChildScope_InheritsParentCancellation()
    {
        // arrange
        using var parent = new CancellationScope("parent");
        using var child = parent.CreateChildScope("child");

        // act
        parent.Cancel(CancellationReason.GlobalTimeout);

        // assert
        parent.IsCancelled.Should().BeTrue();
        child.IsCancelled.Should().BeTrue();
        child.Token.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public void CancellationScope_ChildCancel_DoesNotAffectParent()
    {
        // arrange
        using var parent = new CancellationScope("parent");
        using var child = parent.CreateChildScope("child");

        // act
        child.Cancel(CancellationReason.PerSignalTimeout, "child-signal");

        // assert
        child.IsCancelled.Should().BeTrue();
        parent.IsCancelled.Should().BeFalse();
    }

    [Fact]
    public void CancellationScope_ThreeLevel_PropagatesFromRoot()
    {
        // arrange
        using var root = new CancellationScope("root");
        using var level1 = root.CreateChildScope("level1");
        using var level2 = level1.CreateChildScope("level2");

        // act
        root.Cancel(CancellationReason.GlobalTimeout, "root-signal");

        // assert
        root.IsCancelled.Should().BeTrue();
        level1.IsCancelled.Should().BeTrue();
        level2.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public void CancellationScope_NullOrWhitespaceName_ThrowsException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentNullException>(() => new CancellationScope(null!));
        Assert.Throws<ArgumentException>(() => new CancellationScope(""));
        Assert.Throws<ArgumentException>(() => new CancellationScope("  "));
    }

    #endregion

    #region IScopedIgnitionSignal Tests

    [Fact]
    public async Task ScopedSignal_WhenScopeCancelled_ReturnsCorrectStatus()
    {
        // arrange
        using var scope = new CancellationScope("test-scope");
        var innerSignal = new FakeSignal("inner", async ct =>
        {
            await Task.Delay(200, ct); // Short delay - cancellation will happen before this completes
        });

        var scopedSignal = new ScopedFakeSignal(innerSignal, scope, cancelScopeOnFailure: false);
        var coord = CreateCoordinator(new[] { scopedSignal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
        });

        // Start waiting in background
        var waitTask = coord.WaitAllAsync();

        // Give the signal time to start
        await Task.Delay(30);

        // act - cancel the scope
        scope.Cancel(CancellationReason.BundleCancelled, "external-trigger");

        await waitTask;
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(1);
        var signalResult = result.Results[0];
        signalResult.Status.Should().Be(IgnitionSignalStatus.Cancelled);
        signalResult.CancellationReason.Should().Be(CancellationReason.BundleCancelled);
        signalResult.CancelledBySignal.Should().Be("external-trigger");
    }

    [Fact]
    public async Task ScopedSignal_CancelScopeOnFailure_CancelsOtherSignals()
    {
        // arrange
        using var scope = new CancellationScope("bundle-scope");

        // This signal fails immediately
        var failingSignal = new ScopedFakeSignal(
            new FaultingSignal("failing", new InvalidOperationException("boom")),
            scope,
            cancelScopeOnFailure: true);

        // This signal would take a while but should be cancelled when failingSignal fails
        var longSignal = new ScopedFakeSignal(
            new FakeSignal("long", async ct => await Task.Delay(500, ct)),
            scope,
            cancelScopeOnFailure: false);

        var coord = CreateCoordinator(new IIgnitionSignal[] { failingSignal, longSignal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "failing" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r =>
            r.Name == "long" &&
            r.Status == IgnitionSignalStatus.Cancelled &&
            r.CancellationReason == CancellationReason.BundleCancelled &&
            r.CancelledBySignal == "failing");
    }

    [Fact]
    public async Task ScopedSignal_WithTimeout_CancelsScope()
    {
        // arrange
        using var scope = new CancellationScope("timeout-scope");

        var timedOutSignal = new ScopedFakeSignal(
            new FakeSignal("timeout-me", async ct => await Task.Delay(500, ct), timeout: TimeSpan.FromMilliseconds(50)),
            scope,
            cancelScopeOnFailure: true);

        var waitingSignal = new ScopedFakeSignal(
            new FakeSignal("waiting", async ct => await Task.Delay(500, ct)),
            scope,
            cancelScopeOnFailure: false);

        var coord = CreateCoordinator(new IIgnitionSignal[] { timedOutSignal, waitingSignal }, o =>
        {
            o.GlobalTimeout = TimeSpan.FromSeconds(2);
            o.ExecutionMode = IgnitionExecutionMode.Parallel;
            o.CancelIndividualOnTimeout = true;
        });

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "timeout-me" && r.Status == IgnitionSignalStatus.TimedOut);
        result.Results.Should().Contain(r =>
            r.Name == "waiting" &&
            r.Status == IgnitionSignalStatus.Cancelled &&
            r.CancellationReason == CancellationReason.BundleCancelled &&
            r.CancelledBySignal == "timeout-me");
    }

    #endregion

    #region CancelDependentsOnFailure Tests

    [Fact]
    public async Task DependencyAware_CancelDependentsOnFailure_True_MarksAsCancelled()
    {
        // arrange
        var depSignal = new FaultingSignal("dep", new InvalidOperationException("boom"));
        var childSignal = new FakeSignal("child", _ => Task.CompletedTask);

        var builder = new IgnitionGraphBuilder();
        builder.AddSignals(new IIgnitionSignal[] { depSignal, childSignal });
        builder.DependsOn(childSignal, depSignal);
        var graph = builder.Build();

        var opts = new IgnitionOptions
        {
            GlobalTimeout = TimeSpan.FromSeconds(5),
            ExecutionMode = IgnitionExecutionMode.DependencyAware,
            CancelDependentsOnFailure = true
        };
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var coord = new IgnitionCoordinator(new IIgnitionSignal[] { depSignal, childSignal }, graph, optionsWrapper, logger);

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "dep" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r =>
            r.Name == "child" &&
            r.Status == IgnitionSignalStatus.Cancelled &&
            r.CancellationReason == CancellationReason.DependencyFailed &&
            r.CancelledBySignal == "dep");
    }

    [Fact]
    public async Task DependencyAware_CancelDependentsOnFailure_False_MarksAsSkipped()
    {
        // arrange
        var depSignal = new FaultingSignal("dep", new InvalidOperationException("boom"));
        var childSignal = new FakeSignal("child", _ => Task.CompletedTask);

        var builder = new IgnitionGraphBuilder();
        builder.AddSignals(new IIgnitionSignal[] { depSignal, childSignal });
        builder.DependsOn(childSignal, depSignal);
        var graph = builder.Build();

        var opts = new IgnitionOptions
        {
            GlobalTimeout = TimeSpan.FromSeconds(5),
            ExecutionMode = IgnitionExecutionMode.DependencyAware,
            CancelDependentsOnFailure = false // default
        };
        var optionsWrapper = Options.Create(opts);
        var logger = Substitute.For<ILogger<IgnitionCoordinator>>();
        var coord = new IgnitionCoordinator(new IIgnitionSignal[] { depSignal, childSignal }, graph, optionsWrapper, logger);

        // act
        await coord.WaitAllAsync();
        var result = await coord.GetResultAsync();

        // assert
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Name == "dep" && r.Status == IgnitionSignalStatus.Failed);
        result.Results.Should().Contain(r =>
            r.Name == "child" &&
            r.Status == IgnitionSignalStatus.Skipped &&
            r.CancellationReason == CancellationReason.None);
    }

    #endregion

    #region DI Extensions Tests

    [Fact]
    public void AddIgnitionCancellationScope_CreatesAndRegistersScope()
    {
        // arrange
        var services = new ServiceCollection();

        // act
        var scope = services.AddIgnitionCancellationScope("test-scope");

        // assert
        scope.Should().NotBeNull();
        scope.Name.Should().Be("test-scope");

        var sp = services.BuildServiceProvider();
        var retrievedScope = sp.GetKeyedService<ICancellationScope>("test-scope");
        retrievedScope.Should().BeSameAs(scope);
    }

    [Fact]
    public void AddIgnitionCancellationScope_WithParent_CreatesChildScope()
    {
        // arrange
        var services = new ServiceCollection();
        var parentScope = services.AddIgnitionCancellationScope("parent-scope");

        // act
        var childScope = services.AddIgnitionCancellationScope("child-scope", parentScope);

        // assert
        childScope.Parent.Should().BeSameAs(parentScope);
    }

    [Fact]
    public void AddIgnitionSignalWithScope_RegistersScopedSignal()
    {
        // arrange
        var services = new ServiceCollection();
        var scope = services.AddIgnitionCancellationScope("test-scope");
        var signal = new FakeSignal("test-signal", _ => Task.CompletedTask);

        // act
        services.AddIgnitionSignalWithScope(signal, scope, cancelScopeOnFailure: true);

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>();
        signals.Should().Contain(s => s.Name == "test-signal");

        var scopedSignal = signals.First() as IScopedIgnitionSignal;
        scopedSignal.Should().NotBeNull();
        scopedSignal!.CancellationScope.Should().BeSameAs(scope);
        scopedSignal.CancelScopeOnFailure.Should().BeTrue();
    }

    [Fact]
    public void AddIgnitionFromTaskWithScope_RegistersScopedSignal()
    {
        // arrange
        var services = new ServiceCollection();
        var scope = services.AddIgnitionCancellationScope("test-scope");

        // act
        services.AddIgnitionFromTaskWithScope(
            "task-signal",
            _ => Task.CompletedTask,
            scope,
            cancelScopeOnFailure: true,
            timeout: TimeSpan.FromSeconds(5));

        // assert
        var sp = services.BuildServiceProvider();
        var signals = sp.GetServices<IIgnitionSignal>();
        signals.Should().Contain(s => s.Name == "task-signal");

        var scopedSignal = signals.First() as IScopedIgnitionSignal;
        scopedSignal.Should().NotBeNull();
        scopedSignal!.CancellationScope.Should().BeSameAs(scope);
        scopedSignal.CancelScopeOnFailure.Should().BeTrue();
        scopedSignal.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region IgnitionBundleOptions Cancellation Tests

    [Fact]
    public void IgnitionBundleOptions_EnableScopedCancellation_DefaultsFalse()
    {
        // arrange & act
        var options = new IgnitionBundleOptions();

        // assert
        options.EnableScopedCancellation.Should().BeFalse();
        options.CancellationScope.Should().BeNull();
    }

    [Fact]
    public void IgnitionBundleOptions_CanSetCancellationScope()
    {
        // arrange
        using var scope = new CancellationScope("bundle-scope");
        var options = new IgnitionBundleOptions();

        // act
        options.CancellationScope = scope;
        options.EnableScopedCancellation = true;

        // assert
        options.CancellationScope.Should().BeSameAs(scope);
        options.EnableScopedCancellation.Should().BeTrue();
    }

    #endregion

    #region Result Properties Tests

    [Fact]
    public void IgnitionSignalResult_WasCancelledByScope_ReturnsTrueForScopeCancellations()
    {
        // arrange & act
        var scopeCancelled = new IgnitionSignalResult("test", IgnitionSignalStatus.Cancelled, TimeSpan.Zero,
            CancellationReason: CancellationReason.ScopeCancelled);
        var bundleCancelled = new IgnitionSignalResult("test", IgnitionSignalStatus.Cancelled, TimeSpan.Zero,
            CancellationReason: CancellationReason.BundleCancelled);
        var depFailed = new IgnitionSignalResult("test", IgnitionSignalStatus.Cancelled, TimeSpan.Zero,
            CancellationReason: CancellationReason.DependencyFailed);
        var globalTimeout = new IgnitionSignalResult("test", IgnitionSignalStatus.TimedOut, TimeSpan.Zero,
            CancellationReason: CancellationReason.GlobalTimeout);
        var perSignalTimeout = new IgnitionSignalResult("test", IgnitionSignalStatus.TimedOut, TimeSpan.Zero,
            CancellationReason: CancellationReason.PerSignalTimeout);
        var externalCancel = new IgnitionSignalResult("test", IgnitionSignalStatus.TimedOut, TimeSpan.Zero,
            CancellationReason: CancellationReason.ExternalCancellation);

        // assert
        scopeCancelled.WasCancelledByScope.Should().BeTrue();
        bundleCancelled.WasCancelledByScope.Should().BeTrue();
        depFailed.WasCancelledByScope.Should().BeTrue();
        globalTimeout.WasCancelledByScope.Should().BeFalse();
        perSignalTimeout.WasCancelledByScope.Should().BeFalse();
        externalCancel.WasCancelledByScope.Should().BeFalse();
    }

    [Fact]
    public void IgnitionSignalResult_DefaultCancellationReason_IsNone()
    {
        // arrange & act
        var result = new IgnitionSignalResult("test", IgnitionSignalStatus.Succeeded, TimeSpan.Zero);

        // assert
        result.CancellationReason.Should().Be(CancellationReason.None);
        result.CancelledBySignal.Should().BeNull();
    }

    #endregion

    #region Helper Classes

    private sealed class ScopedFakeSignal : IScopedIgnitionSignal
    {
        private readonly IIgnitionSignal _inner;

        public ScopedFakeSignal(IIgnitionSignal inner, ICancellationScope scope, bool cancelScopeOnFailure)
        {
            _inner = inner;
            CancellationScope = scope;
            CancelScopeOnFailure = cancelScopeOnFailure;
        }

        public string Name => _inner.Name;
        public TimeSpan? Timeout => _inner.Timeout;
        public ICancellationScope? CancellationScope { get; }
        public bool CancelScopeOnFailure { get; }

        public Task WaitAsync(CancellationToken cancellationToken = default)
            => _inner.WaitAsync(cancellationToken);
    }

    #endregion
}
