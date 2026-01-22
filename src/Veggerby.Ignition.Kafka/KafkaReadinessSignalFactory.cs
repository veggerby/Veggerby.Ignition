using System;

using Confluent.Kafka;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Kafka;

/// <summary>
/// Factory for creating Kafka readiness signals with configurable bootstrap servers.
/// </summary>
public sealed class KafkaReadinessSignalFactory : IIgnitionSignalFactory
{
    private readonly Func<IServiceProvider, string> _bootstrapServersFactory;
    private readonly KafkaReadinessOptions _options;

    /// <summary>
    /// Creates a new Kafka readiness signal factory.
    /// </summary>
    /// <param name="bootstrapServersFactory">Factory that produces the bootstrap servers string using the service provider.</param>
    /// <param name="options">Kafka readiness options.</param>
    public KafkaReadinessSignalFactory(
        Func<IServiceProvider, string> bootstrapServersFactory,
        KafkaReadinessOptions options)
    {
        _bootstrapServersFactory = bootstrapServersFactory ?? throw new ArgumentNullException(nameof(bootstrapServersFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public string Name => "kafka-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public int? Stage => _options.Stage;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var bootstrapServers = _bootstrapServersFactory(serviceProvider);
        var producerConfig = new ProducerConfig { BootstrapServers = bootstrapServers };
        var logger = serviceProvider.GetRequiredService<ILogger<KafkaReadinessSignal>>();
        
        return new KafkaReadinessSignal(producerConfig, _options, logger);
    }
}
