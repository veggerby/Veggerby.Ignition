using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Fluent builder implementation for simplified ignition configuration.
/// </summary>
internal sealed class IgnitionBuilder : IIgnitionBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Action<IServiceCollection>> _signalRegistrations = new();
    private TimeSpan? _globalTimeout;
    private TimeSpan? _defaultSignalTimeout;
    private bool _tracingEnabled = true;
    private bool _healthCheckEnabled = true;
    private IgnitionPolicy _policy = IgnitionPolicy.BestEffort;
    private IgnitionExecutionMode _executionMode = IgnitionExecutionMode.Parallel;
    private readonly List<Action<IgnitionOptions>> _advancedConfigurations = new();

    public IgnitionBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    public IIgnitionBuilder AddSignal(string name, Func<CancellationToken, Task> taskFactory, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(taskFactory, nameof(taskFactory));

        _signalRegistrations.Add(svc =>
        {
            var effectiveTimeout = timeout ?? _defaultSignalTimeout;
            svc.AddIgnitionFromTask(name, taskFactory, effectiveTimeout);
        });

        return this;
    }

    public IIgnitionBuilder AddSignal(string name, Task readyTask, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(readyTask, nameof(readyTask));

        _signalRegistrations.Add(svc =>
        {
            var effectiveTimeout = timeout ?? _defaultSignalTimeout;
            svc.AddIgnitionFromTask(name, readyTask, effectiveTimeout);
        });

        return this;
    }

    public IIgnitionBuilder AddSignal(IIgnitionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal, nameof(signal));

        _signalRegistrations.Add(svc => svc.AddIgnitionSignal(signal));

        return this;
    }

    public IIgnitionBuilder AddSignal<TSignal>() where TSignal : class, IIgnitionSignal
    {
        _signalRegistrations.Add(svc => svc.AddIgnitionSignal<TSignal>());

        return this;
    }

    public IIgnitionBuilder UseWebApiProfile()
    {
        _globalTimeout = TimeSpan.FromSeconds(30);
        _defaultSignalTimeout = TimeSpan.FromSeconds(10);
        _policy = IgnitionPolicy.BestEffort;
        _executionMode = IgnitionExecutionMode.Parallel;
        _tracingEnabled = true;
        _healthCheckEnabled = true;

        return this;
    }

    public IIgnitionBuilder UseWorkerProfile()
    {
        _globalTimeout = TimeSpan.FromSeconds(60);
        _defaultSignalTimeout = TimeSpan.FromSeconds(20);
        _policy = IgnitionPolicy.FailFast;
        _executionMode = IgnitionExecutionMode.Parallel;
        _tracingEnabled = true;
        _healthCheckEnabled = true;

        return this;
    }

    public IIgnitionBuilder UseCliProfile()
    {
        _globalTimeout = TimeSpan.FromSeconds(15);
        _defaultSignalTimeout = TimeSpan.FromSeconds(5);
        _policy = IgnitionPolicy.FailFast;
        _executionMode = IgnitionExecutionMode.Sequential;
        _tracingEnabled = false;
        _healthCheckEnabled = false;

        return this;
    }

    public IIgnitionBuilder WithGlobalTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Global timeout cannot be negative.");
        }

        _globalTimeout = timeout;

        return this;
    }

    public IIgnitionBuilder WithDefaultSignalTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Default signal timeout cannot be negative.");
        }

        _defaultSignalTimeout = timeout;

        return this;
    }

    public IIgnitionBuilder WithTracing(bool enabled = true)
    {
        _tracingEnabled = enabled;

        return this;
    }

    public IIgnitionBuilder ConfigureAdvanced(Action<IgnitionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure, nameof(configure));

        _advancedConfigurations.Add(configure);

        return this;
    }

    /// <summary>
    /// Configures lifecycle hooks for observing ignition execution using a type-based registration.
    /// </summary>
    /// <typeparam name="TLifecycleHooks">Concrete implementation of <see cref="IIgnitionLifecycleHooks"/>.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Lifecycle hooks are invoked at key execution points:
    /// <list type="bullet">
    ///   <item>Before/after the entire ignition process</item>
    ///   <item>Before/after each individual signal</item>
    /// </list>
    /// </para>
    /// <para>
    /// Hooks provide read-only observation and cannot modify ignition behavior.
    /// Exceptions in hooks are caught and logged but do not affect execution.
    /// </para>
    /// </remarks>
    public IIgnitionBuilder WithLifecycleHooks<TLifecycleHooks>() where TLifecycleHooks : class, IIgnitionLifecycleHooks
    {
        _services.AddIgnitionLifecycleHooks<TLifecycleHooks>();

        return this;
    }

    /// <summary>
    /// Configures lifecycle hooks using a factory delegate.
    /// </summary>
    /// <param name="factory">Factory delegate that produces the lifecycle hooks using the service provider.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <c>null</c>.</exception>
    /// <remarks>
    /// Use this overload when the lifecycle hooks require dependencies from the DI container.
    /// Common use cases include telemetry enrichment, logging, cleanup, and external system integration.
    /// </remarks>
    public IIgnitionBuilder WithLifecycleHooks(Func<IServiceProvider, IIgnitionLifecycleHooks> factory)
    {
        ArgumentNullException.ThrowIfNull(factory, nameof(factory));

        _services.AddIgnitionLifecycleHooks(factory);

        return this;
    }

    /// <summary>
    /// Configures a custom ignition policy using a concrete instance.
    /// </summary>
    /// <param name="policy">The policy instance to use.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Custom policies enable domain-specific failure handling strategies beyond the built-in policies
    /// (FailFast, BestEffort, ContinueOnTimeout).
    /// </para>
    /// <para>
    /// Common use cases include:
    /// <list type="bullet">
    ///   <item>Retry strategies</item>
    ///   <item>Circuit breakers</item>
    ///   <item>Conditional fail-fast based on signal importance</item>
    ///   <item>Percentage-based success thresholds</item>
    /// </list>
    /// </para>
    /// </remarks>
    public IIgnitionBuilder WithCustomPolicy(IIgnitionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy, nameof(policy));

        _services.AddIgnitionPolicy(policy);

        return this;
    }

    /// <summary>
    /// Configures a custom ignition policy by type, allowing DI to construct the policy instance.
    /// </summary>
    /// <typeparam name="TPolicy">Concrete implementation of <see cref="IIgnitionPolicy"/>.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// The policy type must have a constructor compatible with DI resolution.
    /// Use this overload when the policy requires dependencies injected via constructor.
    /// </remarks>
    public IIgnitionBuilder WithCustomPolicy<TPolicy>() where TPolicy : class, IIgnitionPolicy
    {
        _services.AddIgnitionPolicy<TPolicy>();

        return this;
    }

    /// <summary>
    /// Configures a custom ignition policy using a factory delegate.
    /// </summary>
    /// <param name="policyFactory">Factory delegate that produces the policy using the service provider.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when the policy requires dependencies from the DI container or
    /// when you need to configure the policy with specific parameters.
    /// </remarks>
    public IIgnitionBuilder WithCustomPolicy(Func<IServiceProvider, IIgnitionPolicy> policyFactory)
    {
        ArgumentNullException.ThrowIfNull(policyFactory, nameof(policyFactory));

        _services.AddIgnitionPolicy(policyFactory);

        return this;
    }

    /// <summary>
    /// Internal method to finalize registration by applying all configurations to the service collection.
    /// </summary>
    internal void Build()
    {
        // Register all signals first
        foreach (var registration in _signalRegistrations)
        {
            registration(_services);
        }

        // Configure ignition with applied profile settings and advanced configurations
        _services.AddIgnition(options =>
        {
            if (_globalTimeout.HasValue)
            {
                options.GlobalTimeout = _globalTimeout.Value;
            }

            options.Policy = _policy;
            options.ExecutionMode = _executionMode;
            options.EnableTracing = _tracingEnabled;

            // Apply any advanced configurations
            foreach (var configure in _advancedConfigurations)
            {
                configure(options);
            }
        }, addHealthCheck: _healthCheckEnabled);
    }
}
