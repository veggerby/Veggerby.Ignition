using System.Collections.Generic;
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
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName, nameof(databaseName));
        ArgumentNullException.ThrowIfNull(connectFactory, nameof(connectFactory));

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

        DatabasePhaseSignal? validateSignal = null;
        if (_validateSchemaFactory is not null)
        {
            validateSignal = new DatabasePhaseSignal($"{_databaseName}:validate-schema", _validateSchemaFactory, options.DefaultTimeout);
            services.AddIgnitionSignal(validateSignal);
        }

        DatabasePhaseSignal? warmupSignal = null;
        if (_warmupFactory is not null)
        {
            warmupSignal = new DatabasePhaseSignal($"{_databaseName}:warmup", _warmupFactory, options.DefaultTimeout);
            services.AddIgnitionSignal(warmupSignal);
        }

        // Register dependency graph if any dependencies exist
        if (validateSignal is not null || warmupSignal is not null)
        {
            services.AddIgnitionGraph((builder, sp) =>
            {
                // Only add signals from this bundle to avoid unnecessary dependencies with other bundles
                var bundleSignals = new List<IIgnitionSignal> { connectSignal };
                if (validateSignal is not null)
                {
                    bundleSignals.Add(validateSignal);
                }
                if (warmupSignal is not null)
                {
                    bundleSignals.Add(warmupSignal);
                }

                builder.AddSignals(bundleSignals);

                // Schema validation depends on connection
                if (validateSignal is not null)
                {
                    builder.DependsOn(validateSignal, connectSignal);
                }

                // Warmup depends on schema validation if present, otherwise on connection
                if (warmupSignal is not null)
                {
                    var dependency = validateSignal ?? connectSignal;
                    builder.DependsOn(warmupSignal, dependency);
                }
            });
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
