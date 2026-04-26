using System.Threading;
using System.Threading.Tasks;

namespace Veggerby.Ignition.Bundles;

/// <summary>
/// Pre-built ignition bundle representing a typical database initialization trio:
/// connection establishment, schema validation, and initial data warmup.
/// </summary>
/// <remarks>
/// This bundle registers three signals: connect, validate-schema (optional), and warmup (optional).
/// To enforce the natural ordering (connect → validate-schema → warmup), configure the coordinator
/// with <see cref="IgnitionExecutionMode.Sequential"/> so signals execute in registration order.
/// In <see cref="IgnitionExecutionMode.Parallel"/> or <see cref="IgnitionExecutionMode.DependencyAware"/>
/// modes without an explicit graph, the signals may run concurrently.
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
    public void ConfigureBundle(IIgnitionRegistrar registrar, Action<IgnitionBundleOptions>? configure = null)
    {
        var options = new IgnitionBundleOptions { DefaultTimeout = _defaultTimeout };
        configure?.Invoke(options);

        var connectSignal = new DatabasePhaseSignal($"{_databaseName}:connect", _connectFactory, options.DefaultTimeout);
        registrar.AddSignal(connectSignal);

        DatabasePhaseSignal? validateSignal = null;
        if (_validateSchemaFactory is not null)
        {
            validateSignal = new DatabasePhaseSignal($"{_databaseName}:validate-schema", _validateSchemaFactory, options.DefaultTimeout);
            registrar.AddSignal(validateSignal);
        }

        DatabasePhaseSignal? warmupSignal = null;
        if (_warmupFactory is not null)
        {
            warmupSignal = new DatabasePhaseSignal($"{_databaseName}:warmup", _warmupFactory, options.DefaultTimeout);
            registrar.AddSignal(warmupSignal);
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
