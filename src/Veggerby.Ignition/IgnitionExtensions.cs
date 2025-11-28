using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Veggerby.Ignition;

/// <summary>
/// Extension methods for registering ignition (startup readiness) services and signals with the dependency injection container.
/// </summary>
public static class IgnitionExtensions
{
    /// <summary>
    /// Registers the ignition coordinator and (optionally) the readiness health check plus configuration.
    /// </summary>
    /// <remarks>
    /// This must be called once to enable readiness coordination. Additional calls will only apply
    /// further option configuration (the singleton coordinator is registered with the DI container if absent).
    /// When <paramref name="addHealthCheck"/> is true a health check named <paramref name="healthCheckName"/> is registered.
    /// Tags supplied through <paramref name="healthCheckTags"/> are attached to the health check registration.
    /// </remarks>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for <see cref="IgnitionOptions"/>.</param>
    /// <param name="addHealthCheck">Whether to add the ignition readiness health check (default: true).</param>
    /// <param name="healthCheckName">Name used for the health check registration (default: <c>ignition-readiness</c>).</param>
    /// <param name="healthCheckTags">Optional set of tags applied to the health check registration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddIgnition(
        this IServiceCollection services,
        Action<IgnitionOptions>? configure = null,
        bool addHealthCheck = true,
        string healthCheckName = "ignition-readiness",
        IEnumerable<string>? healthCheckTags = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<IIgnitionCoordinator>(sp =>
        {
            var signals = sp.GetServices<IIgnitionSignal>();
            var graph = sp.GetService<IIgnitionGraph>();
            var options = sp.GetRequiredService<IOptions<IgnitionOptions>>();
            var logger = sp.GetRequiredService<ILogger<IgnitionCoordinator>>();
            return new IgnitionCoordinator(signals, graph, options, logger);
        });

        if (addHealthCheck)
        {
            services.AddHealthChecks().AddCheck<IgnitionHealthCheck>(healthCheckName, tags: healthCheckTags?.ToArray() ?? []);
        }

        return services;
    }

    /// <summary>
    /// Configures a custom timeout strategy for determining per-signal timeout and cancellation behavior.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="strategy">The timeout strategy instance to use.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The timeout strategy is applied via <see cref="IgnitionOptions.TimeoutStrategy"/> configuration.
    /// Call this method after <see cref="AddIgnition"/> to ensure the options are properly configured.
    /// </para>
    /// <para>
    /// Custom strategies enable advanced scenarios such as:
    /// <list type="bullet">
    ///   <item>Exponential scaling based on failure count</item>
    ///   <item>Adaptive timeouts (e.g., slow I/O detection)</item>
    ///   <item>Dynamic per-stage deadlines</item>
    ///   <item>User-defined per-class or per-assembly defaults</item>
    /// </list>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddIgnitionTimeoutStrategy(
        this IServiceCollection services,
        IIgnitionTimeoutStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        services.Configure<IgnitionOptions>(options => options.TimeoutStrategy = strategy);

        return services;
    }

    /// <summary>
    /// Configures a custom timeout strategy for determining per-signal timeout and cancellation behavior using a factory delegate.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="strategyFactory">Factory delegate that produces the timeout strategy using the service provider.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when the timeout strategy requires dependencies from the DI container.
    /// The factory is invoked once when options are first accessed.
    /// </remarks>
    public static IServiceCollection AddIgnitionTimeoutStrategy(
        this IServiceCollection services,
        Func<IServiceProvider, IIgnitionTimeoutStrategy> strategyFactory)
    {
        ArgumentNullException.ThrowIfNull(strategyFactory);

        services.AddSingleton<IIgnitionTimeoutStrategy>(strategyFactory);
        services.AddOptions<IgnitionOptions>()
            .Configure<IIgnitionTimeoutStrategy>((options, strategy) => options.TimeoutStrategy = strategy);

        return services;
    }

    /// <summary>
    /// Configures a custom timeout strategy by type, allowing DI to construct the strategy instance.
    /// </summary>
    /// <typeparam name="TStrategy">Concrete implementation of <see cref="IIgnitionTimeoutStrategy"/>.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    /// <remarks>
    /// The strategy type must have a constructor compatible with DI resolution.
    /// Use this overload when the timeout strategy requires dependencies injected via constructor.
    /// </remarks>
    public static IServiceCollection AddIgnitionTimeoutStrategy<TStrategy>(
        this IServiceCollection services) where TStrategy : class, IIgnitionTimeoutStrategy
    {
        services.AddSingleton<IIgnitionTimeoutStrategy, TStrategy>();
        services.AddOptions<IgnitionOptions>()
            .Configure<IIgnitionTimeoutStrategy>((options, strategy) => options.TimeoutStrategy = strategy);

        return services;
    }

    /// <summary>
    /// Registers a concrete ignition signal instance.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="signal">Concrete signal instance to add (registered as singleton).</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionSignal(
        this IServiceCollection services,
        IIgnitionSignal signal)
    {
        services.AddSingleton(signal);
        return services;
    }

    /// <summary>
    /// Registers a signal type to be constructed by DI when enumerated as <see cref="IIgnitionSignal"/>.
    /// </summary>
    /// <typeparam name="TSignal">Concrete implementation of <see cref="IIgnitionSignal"/>.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionSignal<TSignal>(
        this IServiceCollection services) where TSignal : class, IIgnitionSignal
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IIgnitionSignal, TSignal>());
        return services;
    }

    /// <summary>
    /// Registers multiple ignition signal instances from an enumerable.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="signals">Enumerable sequence of signal instances to register.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionSignals(
        this IServiceCollection services,
        IEnumerable<IIgnitionSignal> signals)
    {
        foreach (var s in signals)
        {
            services.AddSingleton(s);
        }

        return services;
    }

    /// <summary>
    /// Registers multiple ignition signal instances using a params array convenience overload.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="signals">Signal instances to register.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionSignals(
        this IServiceCollection services,
        params IIgnitionSignal[] signals)
        => services.AddIgnitionSignals((IEnumerable<IIgnitionSignal>)signals);

    /// <summary>
    /// Adapts an existing already-created readiness <see cref="Task"/> into an ignition signal.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="name">Logical signal name used for diagnostics and result reporting.</param>
    /// <param name="readyTask">Task that completes when the underlying component is ready.</param>
    /// <param name="timeout">Optional per-signal timeout limit applied by the coordinator.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionFromTask(
        this IServiceCollection services,
        string name,
        Task readyTask,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(readyTask, nameof(readyTask));

        return services.AddIgnitionSignal(IgnitionSignal.FromTask(name, readyTask, timeout));
    }

    /// <summary>
    /// Adapts a lazily-invoked readiness task factory into an ignition signal. The factory is executed once on first wait.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="name">Logical signal name used for diagnostics and result reporting.</param>
    /// <param name="readyTaskFactory">Factory producing the readiness task. Receives the cancellation token from the FIRST wait invocation; subsequent waits reuse the previously created task and cannot alter the token.</param>
    /// <param name="timeout">Optional per-signal timeout limit applied by the coordinator.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionFromTask(
        this IServiceCollection services,
        string name,
        Func<CancellationToken, Task> readyTaskFactory,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(readyTaskFactory, nameof(readyTaskFactory));

        return services.AddIgnitionSignal(IgnitionSignal.FromTaskFactory(name, readyTaskFactory, timeout));
    }

    /// <summary>
    /// Registers a readiness signal for a single lazily-resolved <typeparamref name="TService"/> instance using a non-cancellable selector.
    /// </summary>
    /// <typeparam name="TService">Service type that exposes a readiness task (for example a hosted background service exposing <c>ReadyTask</c>).</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskSelector">Selector returning the readiness <see cref="Task"/> from the resolved service instance. Invoked once on first wait.</param>
    /// <param name="name">Optional explicit signal name (defaults to the simple type name of <typeparamref name="TService"/>).</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global option.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Semantics:
    /// 1. The underlying service is resolved lazily on the first call to <see cref="IIgnitionSignal.WaitAsync(System.Threading.CancellationToken)"/>.
    /// 2. The produced readiness task is cached; subsequent waits reuse the same task (idempotent execution).
    /// 3. Provided <paramref name="name"/> (when non-null) is validated for non-empty/whitespace.
    /// 4. If multiple registrations of <typeparamref name="TService"/> exist, this overload uses the first resolved instance from the container.
    /// For aggregating all instances use <see cref="AddIgnitionForAll{TService}(IServiceCollection, Func{TService, Task}, string?, TimeSpan?)"/>.
    /// </remarks>
    public static IServiceCollection AddIgnitionFor<TService>(
        this IServiceCollection services,
        Func<TService, Task> taskSelector,
        string? name = null,
        TimeSpan? timeout = null) where TService : class
    {
        ArgumentNullException.ThrowIfNull(taskSelector, nameof(taskSelector));

        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        }

        services.AddSingleton<IIgnitionSignal>(sp => new ServiceReadySignal<TService>(
            sp,
            name ?? typeof(TService).Name,
            (svc, _) => taskSelector(svc),
            timeout));

        return services;
    }

    /// <summary>
    /// Registers a readiness signal for a single lazily-resolved <typeparamref name="TService"/> instance using a cancellable selector.
    /// </summary>
    /// <typeparam name="TService">Service type that exposes a readiness task (for example a hosted background service).</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskSelector">Selector returning the readiness task for the resolved service instance; receives the cancellation token captured from the FIRST wait invocation (linked to coordinator cancellations).</param>
    /// <param name="name">Optional explicit signal name (defaults to the simple type name of <typeparamref name="TService"/>).</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global option.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Semantics:
    /// 1. Service resolution and selector invocation occur once (first wait) and the resulting task is cached.
    /// 2. The passed <see cref="CancellationToken"/> is captured from the first wait invocation and linked to coordinator driven cancellations (global timeout cancellation or per‑signal timeout when configured). Subsequent waits do not create a new task and therefore cannot change the token.
    /// 3. Name validation and multi-instance behavior mirror the non-cancellable overload.
    /// </remarks>
    public static IServiceCollection AddIgnitionFor<TService>(
        this IServiceCollection services,
        Func<TService, CancellationToken, Task> taskSelector,
        string? name = null,
        TimeSpan? timeout = null) where TService : class
    {
        ArgumentNullException.ThrowIfNull(taskSelector, nameof(taskSelector));

        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        }

        services.AddSingleton<IIgnitionSignal>(sp => new ServiceReadySignal<TService>(
            sp,
            name ?? typeof(TService).Name,
            (svc, ct) => taskSelector(svc, ct),
            timeout));

        return services;
    }

    /// <summary>
    /// Registers an ignition signal backed by a provider-based readiness task factory (multi-service composition scenario).
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskFactory">Factory that produces the readiness task when first awaited (executed once).</param>
    /// <param name="name">Logical signal name used for diagnostics and reporting.</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global option.</param>
    public static IServiceCollection AddIgnitionFromFactory(
        this IServiceCollection services,
        Func<IServiceProvider, Task> taskFactory,
        string name,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(taskFactory, nameof(taskFactory));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        services.AddSingleton<IIgnitionSignal>(sp => new ServiceCompositeReadySignal(sp, name, taskFactory, timeout));

        return services;
    }

    /// <summary>
    /// Registers a composite readiness signal aggregating all currently registered <typeparamref name="TService"/> instances using a non‑cancellable selector.
    /// </summary>
    /// <typeparam name="TService">Service type exposing (directly or indirectly) a readiness <see cref="Task"/>.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskSelector">Selector that produces the readiness task for a given service instance (invoked once per instance on first wait).</param>
    /// <param name="groupName">Optional explicit group name; defaults to <c>TypeName[*]</c> when omitted.</param>
    /// <param name="timeout">Optional per-signal timeout applied to the composite (covers the aggregate <c>Task.WhenAll</c>).</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Semantics / lifecycle:
    /// 1. Instance snapshot: the set of <typeparamref name="TService"/> instances is resolved once at the first call to <see cref="IIgnitionSignal.WaitAsync(System.Threading.CancellationToken)"/>.
    ///    Later registrations are NOT included; determinism is preserved.
    /// 2. Per-instance tasks are invoked immediately after snapshot and combined via <c>Task.WhenAll</c>; the aggregate task is cached for idempotency.
    /// 3. If zero instances are present the signal completes successfully immediately (classified as succeeded).
    /// 4. Any fault in a constituent task causes the composite to fault; timeouts are evaluated against the composite duration.
    /// 5. Use individual registrations instead of this composite if per-instance classification or granularity is required.
    /// </remarks>
    /// <remarks>
    /// This aggregates all instances under a single logical signal for reduced result noise. If per-instance classification is required,
    /// register distinct signals manually or introduce a custom adapter producing multiple <see cref="IIgnitionSignal"/> entries.
    /// Instances are resolved lazily on first wait and the combined task is cached to ensure idempotency.
    /// </remarks>
    public static IServiceCollection AddIgnitionForAll<TService>(
        this IServiceCollection services,
        Func<TService, Task> taskSelector,
        string? groupName = null,
        TimeSpan? timeout = null) where TService : class
    {
        ArgumentNullException.ThrowIfNull(taskSelector, nameof(taskSelector));

        if (groupName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupName, nameof(groupName));
        }

        services.AddSingleton<IIgnitionSignal>(sp => new ServiceEnumerableReadySignal<TService>(
            sp,
            groupName ?? $"{typeof(TService).Name}[*]",
            (svc, _) => taskSelector(svc),
            timeout));

        return services;
    }

    /// <summary>
    /// Registers a composite readiness signal aggregating all <typeparamref name="TService"/> instances using a cancellable selector.
    /// </summary>
    /// <typeparam name="TService">Service type exposing a readiness task.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskSelector">Cancellable selector producing the readiness task for each instance. The token from the FIRST wait invocation (linked to coordinator cancellations) is propagated to all instance invocations; subsequent waits do not recreate tasks.</param>
    /// <param name="groupName">Optional explicit group name; defaults to <c>TypeName[*]</c>.</param>
    /// <param name="timeout">Optional per-signal timeout for the composite wait.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Behavior mirrors the non‑cancellable overload (snapshot at first wait, cached aggregate task) with added cancellation propagation: the token from the initial wait is passed to every instance selector enabling cooperative cancellation on global or per‑signal timeout when configured.
    /// </remarks>
    public static IServiceCollection AddIgnitionForAll<TService>(
        this IServiceCollection services,
        Func<TService, CancellationToken, Task> taskSelector,
        string? groupName = null,
        TimeSpan? timeout = null) where TService : class
    {
        ArgumentNullException.ThrowIfNull(taskSelector, nameof(taskSelector));

        if (groupName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupName, nameof(groupName));
        }

        services.AddSingleton<IIgnitionSignal>(sp => new ServiceEnumerableReadySignal<TService>(
            sp,
            groupName ?? $"{typeof(TService).Name}[*]",
            (svc, ct) => taskSelector(svc, ct),
            timeout));

        return services;
    }

    /// <summary>
    /// Registers a scoped composite readiness signal aggregating all scoped <typeparamref name="TService"/> instances using a non‑cancellable selector.
    /// </summary>
    /// <typeparam name="TService">Scoped service type exposing a readiness task.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskSelector">Selector producing the readiness task for each scoped instance.</param>
    /// <param name="groupName">Optional explicit group name; defaults to <c>TypeName[*]</c>.</param>
    /// <param name="timeout">Optional per-signal timeout applied to the composite aggregate task.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Semantics / lifecycle:
    /// 1. A new scope is created on first wait and held until all readiness tasks complete (success/fault/timeout) ensuring scoped dependencies remain alive.
    /// 2. Instances are snapshotted once (later registrations not included) and tasks aggregated via <c>Task.WhenAll</c>; the aggregate task is cached.
    /// 3. If there are zero instances the scope is disposed immediately and the signal completes successfully.
    /// 4. The scope is always disposed via a synchronous continuation (no async void) after the aggregate task finishes.
    /// 5. Prefer per-instance registration if you need individual outcome visibility or differential timeouts.
    /// </remarks>
    public static IServiceCollection AddIgnitionForAllScoped<TService>(
        this IServiceCollection services,
        Func<TService, Task> taskSelector,
        string? groupName = null,
        TimeSpan? timeout = null) where TService : class
    {
        ArgumentNullException.ThrowIfNull(taskSelector, nameof(taskSelector));

        if (groupName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupName, nameof(groupName));
        }

        services.AddSingleton<IIgnitionSignal>(sp => new ScopedServiceEnumerableReadySignal<TService>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            groupName ?? $"{typeof(TService).Name}[*]",
            (svc, _) => taskSelector(svc),
            timeout));

        return services;
    }

    /// <summary>
    /// Registers a scoped composite readiness signal aggregating all scoped <typeparamref name="TService"/> instances using a cancellable selector.
    /// </summary>
    /// <typeparam name="TService">Scoped service type exposing a readiness task.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="taskSelector">Cancellable selector producing the readiness task for each scoped instance. The token from the FIRST wait invocation is propagated to all instance invocations within the scope.</param>
    /// <param name="groupName">Optional explicit group name; defaults to <c>TypeName[*]</c>.</param>
    /// <param name="timeout">Optional per-signal timeout applied to the composite.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Semantics mirror the non‑cancellable scoped overload with added cancellation propagation: the token from the initial wait is passed to every scoped instance selector enabling cooperative cancellation when global or per‑signal timeouts trigger cancellation.
    /// </remarks>
    public static IServiceCollection AddIgnitionForAllScoped<TService>(
        this IServiceCollection services,
        Func<TService, CancellationToken, Task> taskSelector,
        string? groupName = null,
        TimeSpan? timeout = null) where TService : class
    {
        ArgumentNullException.ThrowIfNull(taskSelector, nameof(taskSelector));

        if (groupName is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(groupName, nameof(groupName));
        }

        services.AddSingleton<IIgnitionSignal>(sp => new ScopedServiceEnumerableReadySignal<TService>(
            sp.GetRequiredService<IServiceScopeFactory>(),
            groupName ?? $"{typeof(TService).Name}[*]",
            (svc, ct) => taskSelector(svc, ct),
            timeout));

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IIgnitionGraph"/> for dependency-aware execution.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="graph">The ignition graph defining signal dependencies.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// When using dependency-aware execution mode (<see cref="IgnitionExecutionMode.DependencyAware"/>),
    /// an <see cref="IIgnitionGraph"/> must be registered. Use <see cref="IgnitionGraphBuilder"/> to construct the graph.
    /// </remarks>
    public static IServiceCollection AddIgnitionGraph(
        this IServiceCollection services,
        IIgnitionGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        services.AddSingleton(graph);
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IIgnitionGraph"/> built from a configuration delegate.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Delegate to configure the graph builder.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// The builder is invoked once when the service provider is created.
    /// All signals must already be registered before building the graph.
    /// </remarks>
    public static IServiceCollection AddIgnitionGraph(
        this IServiceCollection services,
        Action<IgnitionGraphBuilder, IServiceProvider> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        services.AddSingleton<IIgnitionGraph>(sp =>
        {
            var builder = new IgnitionGraphBuilder();
            configure(builder, sp);
            return builder.Build();
        });

        return services;
    }

    /// <summary>
    /// Registers an ignition bundle, invoking its <see cref="IIgnitionBundle.ConfigureBundle"/> method to register signals and dependencies.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="bundle">The bundle to register.</param>
    /// <param name="configure">Optional configuration delegate for per-bundle options.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// Bundles provide a convenient way to register grouped sets of signals with optional shared configuration.
    /// The bundle's <see cref="IIgnitionBundle.ConfigureBundle"/> method is invoked immediately to register signals and configure dependencies.
    /// Per-bundle options (e.g., <see cref="IgnitionBundleOptions.DefaultTimeout"/>) are applied to signals registered by the bundle.
    /// </remarks>
    public static IServiceCollection AddIgnitionBundle(
        this IServiceCollection services,
        IIgnitionBundle bundle,
        Action<IgnitionBundleOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        bundle.ConfigureBundle(services, configure);

        return services;
    }

    /// <summary>
    /// Registers an ignition bundle by type, constructing it with the default constructor and invoking its <see cref="IIgnitionBundle.ConfigureBundle"/> method.
    /// </summary>
    /// <typeparam name="TBundle">Concrete implementation of <see cref="IIgnitionBundle"/> with a parameterless constructor.</typeparam>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="configure">Optional configuration delegate for per-bundle options.</param>
    /// <returns>The same service collection for chaining.</returns>
    /// <remarks>
    /// This overload requires the bundle type to have a public parameterless constructor.
    /// If the bundle requires constructor dependencies, use the instance-based overload instead.
    /// </remarks>
    public static IServiceCollection AddIgnitionBundle<TBundle>(
        this IServiceCollection services,
        Action<IgnitionBundleOptions>? configure = null) where TBundle : class, IIgnitionBundle, new()
    {
        var bundle = new TBundle();
        bundle.ConfigureBundle(services, configure);

        return services;
    }

    /// <summary>
    /// Registers multiple ignition bundles from an enumerable.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="bundles">Enumerable sequence of bundle instances to register.</param>
    /// <param name="configure">Optional configuration delegate applied to all bundles.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionBundles(
        this IServiceCollection services,
        IEnumerable<IIgnitionBundle> bundles,
        Action<IgnitionBundleOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(bundles);

        foreach (var bundle in bundles)
        {
            services.AddIgnitionBundle(bundle, configure);
        }

        return services;
    }

    /// <summary>
    /// Registers multiple ignition bundles using a params array convenience overload.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="bundles">Bundle instances to register.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionBundles(
        this IServiceCollection services,
        params IIgnitionBundle[] bundles)
        => services.AddIgnitionBundles((IEnumerable<IIgnitionBundle>)bundles);

    private sealed class ServiceReadySignal<TService>(IServiceProvider provider, string name, Func<TService, CancellationToken, Task> selector, TimeSpan? timeout) : IIgnitionSignal where TService : class
    {
        private readonly IServiceProvider _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        private readonly Func<TService, CancellationToken, Task> _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        private readonly object _sync = new();
        private Task? _cachedTask;
        private bool _created;

        public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
        public TimeSpan? Timeout { get; } = timeout;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (!_created)
            {
                lock (_sync)
                {
                    if (!_created)
                    {
                        _cachedTask = _selector(_provider.GetRequiredService<TService>(), cancellationToken);
                        _created = true;
                    }
                }
            }

            if (_cachedTask!.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                return _cachedTask;
            }

            return _cachedTask.WaitAsync(cancellationToken);
        }
    }

    private sealed class ServiceCompositeReadySignal(IServiceProvider provider, string name, Func<IServiceProvider, Task> factory, TimeSpan? timeout) : IIgnitionSignal
    {
        private readonly IServiceProvider _provider = provider;
        private readonly Func<IServiceProvider, Task> _factory = factory;
        private readonly object _sync = new();
        private Task? _task;
        private bool _created;

        public string Name { get; } = name;
        public TimeSpan? Timeout { get; } = timeout;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (!_created)
            {
                lock (_sync)
                {
                    if (!_created)
                    {
                        _task = _factory(_provider); // factory currently non-cancellable; capture semantics only
                        _created = true;
                    }
                }
            }

            if (_task!.IsCompleted || !cancellationToken.CanBeCanceled)
            {
                return _task;
            }

            return _task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ServiceEnumerableReadySignal<TService>(IServiceProvider provider, string name, Func<TService, CancellationToken, Task> taskSelector, TimeSpan? timeout) : IIgnitionSignal where TService : class
    {
        private readonly object _sync = new();
        private Task? _cached;
        private bool _created;

        public string Name { get; } = name;
        public TimeSpan? Timeout { get; } = timeout;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (!_created)
            {
                lock (_sync)
                {
                    if (!_created)
                    {
                        var instances = provider.GetServices<TService>().ToList();

                        if (instances.Count == 0)
                        {
                            _cached = Task.CompletedTask;
                        }
                        else
                        {
                            var tasks = new Task[instances.Count];
                            for (int i = 0; i < instances.Count; i++)
                            {
                                tasks[i] = taskSelector(instances[i], cancellationToken);
                            }
                            _cached = Task.WhenAll(tasks);
                        }

                        _created = true;
                    }
                }
            }

            if (cancellationToken.CanBeCanceled && !_cached!.IsCompleted)
            {
                return _cached!.WaitAsync(cancellationToken);
            }

            return _cached!;
        }
    }

    private sealed class ScopedServiceEnumerableReadySignal<TService>(IServiceScopeFactory scopeFactory, string name, Func<TService, CancellationToken, Task> taskSelector, TimeSpan? timeout) : IIgnitionSignal where TService : class
    {
        private readonly object _sync = new();
        private Task? _cached;
        private bool _created;

        public string Name { get; } = name;
        public TimeSpan? Timeout { get; } = timeout;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (!_created)
            {
                lock (_sync)
                {
                    if (!_created)
                    {
                        var scope = scopeFactory.CreateScope();
                        var provider = scope.ServiceProvider;
                        var instances = provider.GetServices<TService>().ToList();

                        if (instances.Count == 0)
                        {
                            scope.Dispose();
                            _cached = Task.CompletedTask;
                        }
                        else
                        {
                            var tasks = new Task[instances.Count];
                            for (int i = 0; i < instances.Count; i++)
                            {
                                tasks[i] = taskSelector(instances[i], cancellationToken);
                            }

                            _cached = Task.WhenAll(tasks).ContinueWith(t =>
                            {
                                scope.Dispose();
                                return t;
                            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
                        }
                        
                        _created = true;
                    }
                }
            }

            if (cancellationToken.CanBeCanceled && !_cached!.IsCompleted)
            {
                return _cached!.WaitAsync(cancellationToken);
            }

            return _cached!;
        }
    }

    /// <summary>
    /// Creates a new root cancellation scope and registers it as a singleton in the service collection.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="scopeName">Name for the cancellation scope.</param>
    /// <returns>The created cancellation scope for use in further configuration.</returns>
    /// <remarks>
    /// <para>
    /// Use this method to create a hierarchical cancellation scope that can be shared across multiple signals or bundles.
    /// The scope is registered as a singleton and can be retrieved from the service provider using
    /// <see cref="IServiceProvider.GetService(Type)"/> with the scope's name.
    /// </para>
    /// <para>
    /// Child scopes can be created using <see cref="ICancellationScope.CreateChildScope(string)"/> or
    /// <see cref="AddIgnitionCancellationScope(IServiceCollection, string, ICancellationScope)"/>.
    /// </para>
    /// </remarks>
    public static ICancellationScope AddIgnitionCancellationScope(
        this IServiceCollection services,
        string scopeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeName);

        var scope = new CancellationScope(scopeName);
        services.AddSingleton(scope);
        services.AddKeyedSingleton<ICancellationScope>(scopeName, scope);
        return scope;
    }

    /// <summary>
    /// Creates a child cancellation scope under the specified parent and registers it as a singleton.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="scopeName">Name for the child cancellation scope.</param>
    /// <param name="parent">Parent scope that this child will inherit cancellation from.</param>
    /// <returns>The created child cancellation scope for use in further configuration.</returns>
    public static ICancellationScope AddIgnitionCancellationScope(
        this IServiceCollection services,
        string scopeName,
        ICancellationScope parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeName);
        ArgumentNullException.ThrowIfNull(parent);

        var scope = new CancellationScope(scopeName, parent);
        services.AddKeyedSingleton<ICancellationScope>(scopeName, scope);
        return scope;
    }

    /// <summary>
    /// Registers an ignition signal that participates in hierarchical cancellation via the specified scope.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="signal">The signal to register.</param>
    /// <param name="scope">The cancellation scope this signal belongs to.</param>
    /// <param name="cancelScopeOnFailure">When true, failing or timing out this signal cancels all other signals in the scope.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionSignalWithScope(
        this IServiceCollection services,
        IIgnitionSignal signal,
        ICancellationScope scope,
        bool cancelScopeOnFailure = false)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(scope);

        var scopedSignal = new ScopedSignalWrapper(signal, scope, cancelScopeOnFailure);
        services.AddSingleton<IIgnitionSignal>(scopedSignal);
        return services;
    }

    /// <summary>
    /// Registers an ignition signal from a task factory that participates in hierarchical cancellation.
    /// </summary>
    /// <param name="services">Target DI service collection.</param>
    /// <param name="name">Name for the signal.</param>
    /// <param name="taskFactory">Factory that produces the readiness task.</param>
    /// <param name="scope">The cancellation scope this signal belongs to.</param>
    /// <param name="cancelScopeOnFailure">When true, failing or timing out this signal cancels all other signals in the scope.</param>
    /// <param name="timeout">Optional per-signal timeout.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddIgnitionFromTaskWithScope(
        this IServiceCollection services,
        string name,
        Func<CancellationToken, Task> taskFactory,
        ICancellationScope scope,
        bool cancelScopeOnFailure = false,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(taskFactory);
        ArgumentNullException.ThrowIfNull(scope);

        var signal = IgnitionSignal.FromTaskFactory(name, taskFactory, timeout);
        var scopedSignal = new ScopedSignalWrapper(signal, scope, cancelScopeOnFailure);
        services.AddSingleton<IIgnitionSignal>(scopedSignal);
        return services;
    }

    /// <summary>
    /// Wrapper that adds cancellation scope support to an existing signal.
    /// </summary>
    private sealed class ScopedSignalWrapper : IScopedIgnitionSignal
    {
        private readonly IIgnitionSignal _inner;

        public ScopedSignalWrapper(IIgnitionSignal inner, ICancellationScope scope, bool cancelScopeOnFailure)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            CancellationScope = scope ?? throw new ArgumentNullException(nameof(scope));
            CancelScopeOnFailure = cancelScopeOnFailure;
        }

        public string Name => _inner.Name;
        public TimeSpan? Timeout => _inner.Timeout;
        public ICancellationScope? CancellationScope { get; }
        public bool CancelScopeOnFailure { get; }

        public Task WaitAsync(CancellationToken cancellationToken = default)
            => _inner.WaitAsync(cancellationToken);
    }
}
