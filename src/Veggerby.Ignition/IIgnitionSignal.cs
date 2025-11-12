using System.Threading;
using System.Threading.Tasks;

namespace Veggerby.Ignition;

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
    /// Await the readiness of this signal. Should complete successfully when ready or throw to indicate failure.
    /// The provided <paramref name="cancellationToken"/> is cooperative and should be honored if supported.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token signifying the caller no longer wishes to wait.</param>
    /// <returns>A task that completes when the component is ready or faults on error.</returns>
    Task WaitAsync(CancellationToken cancellationToken = default);
}
