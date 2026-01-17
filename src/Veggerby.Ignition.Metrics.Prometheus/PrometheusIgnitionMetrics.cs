using Prometheus;

namespace Veggerby.Ignition.Metrics.Prometheus;

/// <summary>
/// Prometheus implementation of <see cref="IIgnitionMetrics"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation records ignition signal metrics using the prometheus-net library.
/// Metrics are exposed through the standard Prometheus registry and can be scraped via
/// the /metrics endpoint when configured with <c>app.MapMetrics()</c>.
/// </para>
/// <para>
/// The following metrics are exposed:
/// <list type="bullet">
/// <item><description><c>ignition_signal_duration_seconds</c> - Histogram of signal execution durations with labels: signal_name, status</description></item>
/// <item><description><c>ignition_signal_total</c> - Counter of signal executions by status with labels: signal_name, status</description></item>
/// <item><description><c>ignition_total_duration_seconds</c> - Histogram of total ignition execution duration</description></item>
/// </list>
/// </para>
/// <para>
/// This implementation is thread-safe and optimized for low overhead in high-throughput scenarios.
/// </para>
/// </remarks>
public sealed class PrometheusIgnitionMetrics : IIgnitionMetrics
{
    private static readonly Histogram SignalDuration = global::Prometheus.Metrics.CreateHistogram(
        "ignition_signal_duration_seconds",
        "Duration of ignition signal execution in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "signal_name", "status" },
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 15) // 1ms to ~32s
        });

    private static readonly Counter SignalStatus = global::Prometheus.Metrics.CreateCounter(
        "ignition_signal_total",
        "Total number of ignition signals by status",
        "signal_name", "status");

    private static readonly Histogram TotalDuration = global::Prometheus.Metrics.CreateHistogram(
        "ignition_total_duration_seconds",
        "Total duration of ignition execution in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 15) // 100ms to ~3276s
        });

    /// <inheritdoc/>
    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        SignalDuration.WithLabels(name, "completed").Observe(duration.TotalSeconds);
    }

    /// <inheritdoc/>
    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        SignalStatus.WithLabels(name, status.ToString()).Inc();
    }

    /// <inheritdoc/>
    public void RecordTotalDuration(TimeSpan duration)
    {
        TotalDuration.Observe(duration.TotalSeconds);
    }
}
