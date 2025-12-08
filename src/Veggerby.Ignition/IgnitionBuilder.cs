using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

namespace Veggerby.Ignition;

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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(taskFactory);

        _signalRegistrations.Add(svc =>
        {
            var effectiveTimeout = timeout ?? _defaultSignalTimeout;
            svc.AddIgnitionFromTask(name, taskFactory, effectiveTimeout);
        });

        return this;
    }

    public IIgnitionBuilder AddSignal(string name, Task readyTask, TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(readyTask);

        _signalRegistrations.Add(svc =>
        {
            var effectiveTimeout = timeout ?? _defaultSignalTimeout;
            svc.AddIgnitionFromTask(name, readyTask, effectiveTimeout);
        });

        return this;
    }

    public IIgnitionBuilder AddSignal(IIgnitionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

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
        ArgumentNullException.ThrowIfNull(configure);

        _advancedConfigurations.Add(configure);

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
