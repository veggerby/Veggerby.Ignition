using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a single startup readiness signal ("ignition signal").
/// An application registers one or more signals that the <see cref="IIgnitionCoordinator"/> awaits
/// before declaring startup readiness.
/// </summary>
/// <remarks>
/// Implementations should complete their <see cref="WaitAsync"/> task when the underlying component
/// has finished its initialization phase (e.g. a background connection established, warm cache populated, etc.).
/// If <see cref="Timeout"/> elapses before completion, the coordinator will treat the signal as timed out.
/// </remarks>
public interface IIgnitionSignal
{
    /// <summary>
    /// Human-friendly name used for logging, diagnostics and health reporting.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Optional per-signal timeout. If <c>null</c>, the global timeout configured via <see cref="IgnitionOptions"/> applies.
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// Gets whether this signal is critical for startup success.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c> (the default), a failure or timeout of this signal contributes to the overall startup
    /// failure outcome and is treated as a blocking dependency.
    /// </para>
    /// <para>
    /// When <c>false</c>, the signal is advisory: its outcome is recorded in diagnostics and the health check,
    /// but a failure or timeout does not block startup from being declared successful. This is useful for
    /// optional features like warm caches, background enrichment services, or telemetry collectors.
    /// </para>
    /// <para>
    /// Existing implementations that do not override this property automatically inherit <c>true</c>
    /// (fully required), preserving backward compatibility.
    /// </para>
    /// </remarks>
    bool IsRequired => true;

    /// <summary>
    /// Await the readiness of this signal. Should complete successfully when ready or throw to indicate failure.
    /// The provided <paramref name="cancellationToken"/> is cooperative and should be honored if supported.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token signifying the caller no longer wishes to wait.</param>
    /// <returns>A task that completes when the component is ready or faults on error.</returns>
    Task WaitAsync(CancellationToken cancellationToken = default);
}
