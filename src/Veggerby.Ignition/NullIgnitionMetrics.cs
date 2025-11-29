using System;

namespace Veggerby.Ignition;

/// <summary>
/// Default no-operation implementation of <see cref="IIgnitionMetrics"/> that discards all recorded metrics.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is designed to add zero overhead when metrics are not required.
/// All methods are empty and will be inlined by the JIT compiler.
/// </para>
/// <para>
/// Use this as the default when no custom metrics implementation is configured.
/// </para>
/// </remarks>
public sealed class NullIgnitionMetrics : IIgnitionMetrics
{
    /// <summary>
    /// Singleton instance of the null metrics implementation.
    /// </summary>
    public static readonly NullIgnitionMetrics Instance = new();

    private NullIgnitionMetrics()
    {
    }

    /// <inheritdoc/>
    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        // No-op: intentionally empty for zero overhead.
    }

    /// <inheritdoc/>
    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        // No-op: intentionally empty for zero overhead.
    }

    /// <inheritdoc/>
    public void RecordTotalDuration(TimeSpan duration)
    {
        // No-op: intentionally empty for zero overhead.
    }
}
