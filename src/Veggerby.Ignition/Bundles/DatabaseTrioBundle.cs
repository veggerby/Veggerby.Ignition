using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Veggerby.Ignition.Bundles;

/// <summary>
/// Pre-built ignition bundle representing a typical database initialization trio:
/// connection establishment, schema validation, and initial data warmup.
/// </summary>
/// <remarks>
/// This bundle demonstrates a dependency-aware pattern where schema validation depends on connection,
/// and data warmup depends on schema validation. Users provide factory delegates for each phase.
/// </remarks>
public sealed class DatabaseTrioBundle : IIgnitionBundle
{
    private readonly Func<CancellationToken, Task> _connectFactory;
    private readonly Func<CancellationToken, Task>? _validateSchemaFactory;
    private readonly Func<CancellationToken, Task>? _warmupFactory;
    private readonly string _databaseName;
    private readonly TimeSpan? _defaultTimeout;

    /// <summary>
    /// Creates a database trio bundle with the specified initialization phases.
    /// </summary>
    /// <param name="databaseName">Human-friendly database name used in signal names (e.g., "primary-db").</param>
    /// <param name="connectFactory">Factory that establishes the database connection.</param>
    /// <param name="validateSchemaFactory">Optional factory that validates the database schema (runs after connection).</param>
    /// <param name="warmupFactory">Optional factory that warms up initial data or caches (runs after schema validation).</param>
    /// <param name="defaultTimeout">Optional default timeout per phase.</param>
    public DatabaseTrioBundle(
        string databaseName,
        Func<CancellationToken, Task> connectFactory,
        Func<CancellationToken, Task>? validateSchemaFactory = null,
        Func<CancellationToken, Task>? warmupFactory = null,
        TimeSpan? defaultTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentNullException.ThrowIfNull(connectFactory);

        _databaseName = databaseName;
        _connectFactory = connectFactory;
        _validateSchemaFactory = validateSchemaFactory;
        _warmupFactory = warmupFactory;
        _defaultTimeout = defaultTimeout;
    }

    /// <inheritdoc/>
    public string Name => $"DatabaseTrio:{_databaseName}";

    /// <inheritdoc/>
    public void ConfigureBundle(IServiceCollection services, Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions { DefaultTimeout = _defaultTimeout };
        configure?.Invoke(options);

        var connectSignal = new DatabasePhaseSignal($"{_databaseName}:connect", _connectFactory, options.DefaultTimeout);
        services.AddIgnitionSignal(connectSignal);

        if (_validateSchemaFactory is not null)
        {
            var validateSignal = new DatabasePhaseSignal($"{_databaseName}:validate-schema", _validateSchemaFactory, options.DefaultTimeout);
            services.AddIgnitionSignal(validateSignal);

            // Register dependency graph if not already configured
            services.AddIgnitionGraph((builder, sp) =>
            {
                var signals = sp.GetServices<IIgnitionSignal>();
                builder.AddSignals(signals);
                builder.DependsOn(validateSignal, connectSignal);

                if (_warmupFactory is not null)
                {
                    var warmupSignal = sp.GetServices<IIgnitionSignal>()
                        .FirstOrDefault(s => s.Name == $"{_databaseName}:warmup");
                    
                    if (warmupSignal is not null)
                    {
                        builder.DependsOn(warmupSignal, validateSignal);
                    }
                }
            });
        }

        if (_warmupFactory is not null)
        {
            var warmupSignal = new DatabasePhaseSignal($"{_databaseName}:warmup", _warmupFactory, options.DefaultTimeout);
            services.AddIgnitionSignal(warmupSignal);

            if (_validateSchemaFactory is null)
            {
                // Warmup depends directly on connect if no schema validation
                services.AddIgnitionGraph((builder, sp) =>
                {
                    var signals = sp.GetServices<IIgnitionSignal>();
                    builder.AddSignals(signals);
                    builder.DependsOn(warmupSignal, connectSignal);
                });
            }
        }
    }

    private sealed class DatabasePhaseSignal : IIgnitionSignal
    {
        private readonly Func<CancellationToken, Task> _factory;

        public DatabasePhaseSignal(string name, Func<CancellationToken, Task> factory, TimeSpan? timeout)
        {
            Name = name;
            _factory = factory;
            Timeout = timeout;
        }

        public string Name { get; }
        public TimeSpan? Timeout { get; }

        public Task WaitAsync(CancellationToken cancellationToken = default)
            => _factory(cancellationToken);
    }
}
