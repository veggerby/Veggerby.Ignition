using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace Veggerby.Ignition.Metrics.OpenTelemetry;

/// <summary>
/// OpenTelemetry implementation of <see cref="IIgnitionMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation records ignition signal metrics using the OpenTelemetry .NET SDK.
/// Metrics are exposed through a <see cref="Meter"/> and can be exported to any OTEL-compatible
/// backend (Prometheus, Jaeger, Zipkin, etc.) using standard OTEL exporters.
/// </para>
/// <para>
/// The following metrics are exposed:
/// <list type="bullet">
/// <item><description><c>ignition.signal.duration</c> - Histogram of signal execution durations (seconds) with tag: signal.name</description></item>
/// <item><description><c>ignition.signal.status</c> - Counter of signal executions by status with tags: signal.name, signal.status</description></item>
/// <item><description><c>ignition.total.duration</c> - Histogram of total ignition execution duration (seconds)</description></item>
/// </list>
/// </para>
/// <para>
/// This implementation is thread-safe and optimized for low overhead in high-throughput scenarios.
/// The <see cref="Meter"/> must be registered with the OpenTelemetry SDK using
/// <c>.AddMeter("Veggerby.Ignition")</c> in your metrics configuration.
/// </para>
/// </remarks>
public sealed class OpenTelemetryIgnitionMetrics : IIgnitionMetrics, IDisposable
{
    private readonly Meter _meter;
    private readonly Histogram<double> _signalDuration;
    private readonly Counter<long> _signalStatus;
    private readonly Histogram<double> _totalDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenTelemetryIgnitionMetrics"/> class.
    /// </summary>
    public OpenTelemetryIgnitionMetrics()
    {
        _meter = new Meter("Veggerby.Ignition", "1.0.0");

        _signalDuration = _meter.CreateHistogram<double>(
            "ignition.signal.duration",
            unit: "s",
            description: "Duration of ignition signal execution");

        _signalStatus = _meter.CreateCounter<long>(
            "ignition.signal.status",
            description: "Total number of ignition signals by status");

        _totalDuration = _meter.CreateHistogram<double>(
            "ignition.total.duration",
            unit: "s",
            description: "Total duration of ignition execution");
    }

    /// <inheritdoc/>
    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        _signalDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("signal.name", name));
    }

    /// <inheritdoc/>
    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        _signalStatus.Add(1,
            new KeyValuePair<string, object?>("signal.name", name),
            new KeyValuePair<string, object?>("signal.status", status.ToString()));
    }

    /// <inheritdoc/>
    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Record(duration.TotalSeconds);
    }

    /// <summary>
    /// Disposes the underlying <see cref="Meter"/>.
    /// </summary>
    public void Dispose()
    {
        _meter.Dispose();
    }
}
