using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Fluent builder interface for simplified ignition configuration with sensible defaults.
/// Provides an opinionated, minimal-configuration API for 80-90% of use cases.
/// </summary>
/// <remarks>
/// The Simple Mode API focuses on developer productivity by:
/// <list type="bullet">
///   <item>Applying sensible defaults (parallel execution, BestEffort policy, safe timeouts)</item>
///   <item>Offering pre-configured profiles for common application types</item>
///   <item>Hiding advanced features behind an explicit opt-in model</item>
///   <item>Enabling production-ready setup in fewer than 10 lines</item>
/// </list>
/// For advanced scenarios (DAG execution, custom timeout strategies, staged execution), use the full API.
/// </remarks>
public interface IIgnitionBuilder
{
    /// <summary>
    /// Adds a named readiness signal using a task factory.
    /// </summary>
    /// <param name="name">Signal name for diagnostics and logging.</param>
    /// <param name="taskFactory">Factory producing the readiness task when first awaited.</param>
    /// <param name="timeout">Optional per-signal timeout (defaults to profile timeout if not specified).</param>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder AddSignal(string name, Func<CancellationToken, Task> taskFactory, TimeSpan? timeout = null);

    /// <summary>
    /// Adds a named readiness signal using an already-created task.
    /// </summary>
    /// <param name="name">Signal name for diagnostics and logging.</param>
    /// <param name="readyTask">Task that completes when ready.</param>
    /// <param name="timeout">Optional per-signal timeout (defaults to profile timeout if not specified).</param>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder AddSignal(string name, Task readyTask, TimeSpan? timeout = null);

    /// <summary>
    /// Adds a custom signal instance.
    /// </summary>
    /// <param name="signal">The signal to register.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder AddSignal(IIgnitionSignal signal);

    /// <summary>
    /// Adds a signal by type, allowing DI to construct it.
    /// </summary>
    /// <typeparam name="TSignal">Signal type implementing <see cref="IIgnitionSignal"/>.</typeparam>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder AddSignal<TSignal>() where TSignal : class, IIgnitionSignal;

    /// <summary>
    /// Applies a pre-configured profile optimized for Web API applications.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// Web API profile defaults:
    /// <list type="bullet">
    ///   <item>Global timeout: 30 seconds</item>
    ///   <item>Per-signal timeout: 10 seconds</item>
    ///   <item>Policy: BestEffort (startup continues even if non-critical signals fail)</item>
    ///   <item>Execution mode: Parallel</item>
    ///   <item>Tracing: Enabled (if available)</item>
    ///   <item>Health check: Enabled</item>
    /// </list>
    /// </remarks>
    IIgnitionBuilder UseWebApiProfile();

    /// <summary>
    /// Applies a pre-configured profile optimized for Worker Service applications.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// Worker profile defaults:
    /// <list type="bullet">
    ///   <item>Global timeout: 60 seconds</item>
    ///   <item>Per-signal timeout: 20 seconds</item>
    ///   <item>Policy: FailFast (startup stops immediately on any failure)</item>
    ///   <item>Execution mode: Parallel</item>
    ///   <item>Tracing: Enabled (if available)</item>
    ///   <item>Health check: Enabled</item>
    /// </list>
    /// </remarks>
    IIgnitionBuilder UseWorkerProfile();

    /// <summary>
    /// Applies a pre-configured profile optimized for CLI applications.
    /// </summary>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// CLI profile defaults:
    /// <list type="bullet">
    ///   <item>Global timeout: 15 seconds</item>
    ///   <item>Per-signal timeout: 5 seconds</item>
    ///   <item>Policy: FailFast (fail immediately on any error)</item>
    ///   <item>Execution mode: Sequential (for deterministic startup)</item>
    ///   <item>Tracing: Disabled</item>
    ///   <item>Health check: Disabled</item>
    /// </list>
    /// </remarks>
    IIgnitionBuilder UseCliProfile();

    /// <summary>
    /// Overrides the global timeout for all signals.
    /// </summary>
    /// <param name="timeout">Maximum total duration for startup readiness.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder WithGlobalTimeout(TimeSpan timeout);

    /// <summary>
    /// Overrides the default per-signal timeout applied when signals don't specify their own.
    /// </summary>
    /// <param name="timeout">Default timeout for signals without explicit timeout.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder WithDefaultSignalTimeout(TimeSpan timeout);

    /// <summary>
    /// Enables or disables distributed tracing for ignition execution.
    /// </summary>
    /// <param name="enabled">True to enable tracing (default), false to disable.</param>
    /// <returns>The builder for fluent chaining.</returns>
    IIgnitionBuilder WithTracing(bool enabled = true);

    /// <summary>
    /// Configures advanced options for power users who need fine-grained control.
    /// </summary>
    /// <param name="configure">Configuration delegate for <see cref="IgnitionOptions"/>.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// Use this to access advanced features like custom timeout strategies, metrics, staged execution, etc.
    /// Settings configured here override profile defaults.
    /// </remarks>
    IIgnitionBuilder ConfigureAdvanced(Action<IgnitionOptions> configure);

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
    IIgnitionBuilder WithLifecycleHooks<TLifecycleHooks>() where TLifecycleHooks : class, IIgnitionLifecycleHooks;

    /// <summary>
    /// Configures lifecycle hooks using a factory delegate.
    /// </summary>
    /// <param name="factory">Factory delegate that produces the lifecycle hooks using the service provider.</param>
    /// <returns>The builder for fluent chaining.</returns>
    /// <remarks>
    /// Use this overload when the lifecycle hooks require dependencies from the DI container.
    /// Common use cases include telemetry enrichment, logging, cleanup, and external system integration.
    /// </remarks>
    IIgnitionBuilder WithLifecycleHooks(Func<IServiceProvider, IIgnitionLifecycleHooks> factory);
}
