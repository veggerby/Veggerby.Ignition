using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Prometheus;

using Veggerby.Ignition;
using Veggerby.Ignition.Metrics;

namespace Metrics;

/// <summary>
/// Custom IIgnitionMetrics implementation using OpenTelemetry.
/// </summary>
public class OpenTelemetryIgnitionMetrics : IIgnitionMetrics
{
    private readonly Meter _meter;
    private readonly Histogram<double> _signalDuration;
    private readonly Counter<long> _signalStatus;
    private readonly Histogram<double> _totalDuration;

    public OpenTelemetryIgnitionMetrics()
    {
        _meter = new Meter("Veggerby.Ignition", "1.0.0");
        _signalDuration = _meter.CreateHistogram<double>(
            "ignition.signal.duration",
            unit: "ms",
            description: "Duration of individual ignition signals");
        _signalStatus = _meter.CreateCounter<long>(
            "ignition.signal.status",
            description: "Count of signal statuses");
        _totalDuration = _meter.CreateHistogram<double>(
            "ignition.total.duration",
            unit: "ms",
            description: "Total ignition process duration");
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.Record(
            duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("signal.name", name));
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatus.Add(
            1,
            new KeyValuePair<string, object?>("signal.name", name),
            new KeyValuePair<string, object?>("status", status.ToString()));
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Record(duration.TotalMilliseconds);
    }
}

/// <summary>
/// Custom IIgnitionMetrics implementation using Prometheus.NET.
/// </summary>
public class PrometheusIgnitionMetrics : IIgnitionMetrics
{
    private readonly Histogram _signalDuration;
    private readonly Counter _signalStatus;
    private readonly Gauge _totalDuration;

    public PrometheusIgnitionMetrics()
    {
        _signalDuration = Prometheus.Metrics.CreateHistogram(
            "ignition_signal_duration_milliseconds",
            "Duration of individual ignition signals in milliseconds",
            new Prometheus.HistogramConfiguration
            {
                LabelNames = new[] { "signal_name" },
                Buckets = Histogram.ExponentialBuckets(start: 10, factor: 2, count: 10)
            });

        _signalStatus = Prometheus.Metrics.CreateCounter(
            "ignition_signal_status_total",
            "Count of signal statuses",
            new Prometheus.CounterConfiguration
            {
                LabelNames = new[] { "signal_name", "status" }
            });

        _totalDuration = Prometheus.Metrics.CreateGauge(
            "ignition_total_duration_milliseconds",
            "Total ignition process duration in milliseconds");
    }

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        _signalDuration.WithLabels(name).Observe(duration.TotalMilliseconds);
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        _signalStatus.WithLabels(name, status.ToString()).Inc();
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration.Set(duration.TotalMilliseconds);
    }
}

/// <summary>
/// Custom IIgnitionMetrics implementation for in-memory collection and console reporting.
/// </summary>
public class ConsoleIgnitionMetrics : IIgnitionMetrics
{
    private readonly List<(string Name, TimeSpan Duration)> _signalDurations = new();
    private readonly List<(string Name, IgnitionSignalStatus Status)> _signalStatuses = new();
    private TimeSpan? _totalDuration;

    public void RecordSignalDuration(string name, TimeSpan duration)
    {
        lock (_signalDurations)
        {
            _signalDurations.Add((name, duration));
        }
    }

    public void RecordSignalStatus(string name, IgnitionSignalStatus status)
    {
        lock (_signalStatuses)
        {
            _signalStatuses.Add((name, status));
        }
    }

    public void RecordTotalDuration(TimeSpan duration)
    {
        _totalDuration = duration;
    }

    public void PrintMetrics()
    {
        Console.WriteLine("\n📊 Custom Metrics Report:");
        Console.WriteLine("   Signal Durations:");

        lock (_signalDurations)
        {
            foreach (var (name, duration) in _signalDurations.OrderBy(x => x.Name))
            {
                Console.WriteLine($"      • {name}: {duration.TotalMilliseconds:F0}ms");
            }
        }

        Console.WriteLine("\n   Signal Statuses:");
        lock (_signalStatuses)
        {
            var grouped = _signalStatuses.GroupBy(x => x.Status);
            foreach (var group in grouped)
            {
                Console.WriteLine($"      • {group.Key}: {group.Count()}");
            }
        }

        if (_totalDuration.HasValue)
        {
            Console.WriteLine($"\n   Total Duration: {_totalDuration.Value.TotalMilliseconds:F0}ms");
        }
    }
}

/// <summary>
/// Demonstrates IIgnitionMetrics integration with various observability backends.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== Metrics Integration Sample ===\n");
        Console.WriteLine("This sample demonstrates integrating Ignition metrics with");
        Console.WriteLine("OpenTelemetry, Prometheus, and custom metrics backends.\n");

        await RunOpenTelemetryExample();
        Console.WriteLine();
        await RunPrometheusExample();
        Console.WriteLine();
        await RunCustomMetricsExample();
    }

    /// <summary>
    /// Example using OpenTelemetry metrics.
    /// </summary>
    private static async Task RunOpenTelemetryExample()
    {
        Console.WriteLine("🏗️  Example 1: OpenTelemetry Metrics");
        Console.WriteLine(new string('=', 60));

        var otelMetrics = new OpenTelemetryIgnitionMetrics();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("Veggerby.Ignition")
            .AddConsoleExporter()
            .Build();

        var host = CreateHost(otelMetrics);
        await ExecuteAndReport(host, "OpenTelemetry");
    }

    /// <summary>
    /// Example using Prometheus.NET metrics.
    /// </summary>
    private static async Task RunPrometheusExample()
    {
        Console.WriteLine("🏗️  Example 2: Prometheus.NET Metrics");
        Console.WriteLine(new string('=', 60));

        var prometheusMetrics = new PrometheusIgnitionMetrics();
        var host = CreateHost(prometheusMetrics);
        await ExecuteAndReport(host, "Prometheus");

        Console.WriteLine("\n📊 Prometheus Metrics (text format):");
        Console.WriteLine(new string('-', 60));

        using var stream = new System.IO.MemoryStream();
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        stream.Position = 0;
        using var reader = new System.IO.StreamReader(stream);
        var metricsText = await reader.ReadToEndAsync();

        // Filter to only show ignition metrics
        var ignitionMetrics = metricsText.Split('\n')
            .Where(line => line.Contains("ignition_") || line.StartsWith("# "))
            .Take(20)
            .Where(line => !string.IsNullOrWhiteSpace(line));

        foreach (var line in ignitionMetrics)
        {
            Console.WriteLine(line);
        }
    }

    /// <summary>
    /// Example using custom in-memory metrics.
    /// </summary>
    private static async Task RunCustomMetricsExample()
    {
        Console.WriteLine("🏗️  Example 3: Custom Metrics Backend");
        Console.WriteLine(new string('=', 60));

        var customMetrics = new ConsoleIgnitionMetrics();
        var host = CreateHost(customMetrics);
        await ExecuteAndReport(host, "Custom");
        customMetrics.PrintMetrics();
    }

    private static IHost CreateHost(IIgnitionMetrics metrics)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddIgnition(options =>
                {
                    options.Policy = IgnitionPolicy.BestEffort;
                    options.GlobalTimeout = TimeSpan.FromSeconds(30);
                    options.Metrics = metrics;
                });

                // Register some sample signals
                services.AddIgnitionFromTask(
                    "database-connection",
                    async ct =>
                    {
                        await Task.Delay(500, ct);
                    },
                    TimeSpan.FromSeconds(10));

                services.AddIgnitionFromTask(
                    "cache-warmup",
                    async ct =>
                    {
                        await Task.Delay(800, ct);
                    },
                    TimeSpan.FromSeconds(10));

                services.AddIgnitionFromTask(
                    "config-validation",
                    async ct =>
                    {
                        await Task.Delay(300, ct);
                    },
                    TimeSpan.FromSeconds(10));
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();
    }

    private static async Task ExecuteAndReport(IHost host, string exampleName)
    {
        var coordinator = host.Services.GetRequiredService<IIgnitionCoordinator>();

        await coordinator.WaitAllAsync();
        var result = await coordinator.GetResultAsync();

        Console.WriteLine($"\n✅ {exampleName} - Initialization completed in {result.TotalDuration.TotalMilliseconds:F0}ms");
        Console.WriteLine($"   Signals: {result.Results.Count(r => r.Status == IgnitionSignalStatus.Succeeded)}/{result.Results.Count} succeeded");
    }
}
