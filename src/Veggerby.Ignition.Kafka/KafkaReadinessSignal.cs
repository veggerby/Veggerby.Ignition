using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.Extensions.Logging;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition.Kafka;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Ignition signal for verifying Kafka cluster readiness.
/// Validates broker connectivity, topic metadata, producer functionality, and consumer group registration.
/// </summary>
internal sealed class KafkaReadinessSignal : IIgnitionSignal
{
    private readonly ProducerConfig? _producerConfig;
    private readonly Func<ProducerConfig>? _producerConfigFactory;
    private readonly KafkaReadinessOptions _options;
    private readonly ILogger<KafkaReadinessSignal> _logger;
    private readonly object _sync = new object();
    private Task? _cachedTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaReadinessSignal"/> class.
    /// </summary>
    /// <param name="producerConfig">Kafka producer configuration.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public KafkaReadinessSignal(
        ProducerConfig producerConfig,
        KafkaReadinessOptions options,
        ILogger<KafkaReadinessSignal> logger)
    {
        _producerConfig = producerConfig ?? throw new ArgumentNullException(nameof(producerConfig));
        _producerConfigFactory = null;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaReadinessSignal"/> class
    /// using a factory function for lazy producer config creation.
    /// </summary>
    /// <param name="producerConfigFactory">Factory function that creates a producer config when invoked.</param>
    /// <param name="options">Configuration options for readiness verification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public KafkaReadinessSignal(
        Func<ProducerConfig> producerConfigFactory,
        KafkaReadinessOptions options,
        ILogger<KafkaReadinessSignal> logger)
    {
        _producerConfig = null;
        _producerConfigFactory = producerConfigFactory ?? throw new ArgumentNullException(nameof(producerConfigFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string Name => "kafka-readiness";

    /// <inheritdoc/>
    public TimeSpan? Timeout => _options.Timeout;

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedTask is null)
        {
            lock (_sync)
            {
                _cachedTask ??= ExecuteAsync(cancellationToken);
            }
        }

        return cancellationToken.CanBeCanceled && !_cachedTask.IsCompleted
            ? _cachedTask.WaitAsync(cancellationToken)
            : _cachedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var producerConfig = _producerConfig ?? _producerConfigFactory!();

        var activity = Activity.Current;
        activity?.SetTag("kafka.bootstrap.servers", producerConfig.BootstrapServers);
        activity?.SetTag("kafka.verification.strategy", _options.VerificationStrategy.ToString());

        _logger.LogInformation(
            "Kafka readiness check starting for {BootstrapServers} using strategy {Strategy}",
            producerConfig.BootstrapServers,
            _options.VerificationStrategy);

        // Validate configuration before attempting connection
        ValidateConfiguration();

        var retryPolicy = new RetryPolicy(_options.MaxRetries, _options.RetryDelay, _logger);

        await retryPolicy.ExecuteAsync(
            async ct => await PerformVerificationAsync(producerConfig, ct),
            "Kafka verification",
            cancellationToken,
            _options.Timeout);

        _logger.LogInformation("Kafka readiness check completed successfully");
    }

    private void ValidateConfiguration()
    {
        if (_options.VerificationStrategy == KafkaVerificationStrategy.ConsumerGroupCheck
            && string.IsNullOrWhiteSpace(_options.VerifyConsumerGroup))
        {
            throw new InvalidOperationException(
                "ConsumerGroupCheck strategy requires VerifyConsumerGroup to be set");
        }
    }

    private async Task PerformVerificationAsync(ProducerConfig producerConfig, CancellationToken cancellationToken)
    {
        switch (_options.VerificationStrategy)
        {
            case KafkaVerificationStrategy.ClusterMetadata:
                await VerifyClusterMetadataAsync(producerConfig, cancellationToken);
                break;

            case KafkaVerificationStrategy.TopicMetadata:
                await VerifyTopicMetadataAsync(producerConfig, cancellationToken);
                break;

            case KafkaVerificationStrategy.ProducerTest:
                await PerformProducerTestAsync(producerConfig, cancellationToken);
                break;

            case KafkaVerificationStrategy.ConsumerGroupCheck:
                await VerifyConsumerGroupAsync(producerConfig, cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported verification strategy: {_options.VerificationStrategy}");
        }

        if (_options.VerifySchemaRegistry && !string.IsNullOrWhiteSpace(_options.SchemaRegistryUrl))
        {
            await VerifySchemaRegistryAsync(cancellationToken);
        }
    }

    private Task VerifyClusterMetadataAsync(ProducerConfig producerConfig, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Verifying cluster metadata");

        using var adminClient = new AdminClientBuilder(producerConfig).Build();

        var metadataTimeout = _options.Timeout ?? TimeSpan.FromSeconds(15);
        var metadata = adminClient.GetMetadata(metadataTimeout);

        if (metadata.Brokers == null || metadata.Brokers.Count == 0)
        {
            throw new InvalidOperationException("No brokers found in cluster metadata");
        }

        _logger.LogDebug(
            "Cluster metadata retrieved successfully: {BrokerCount} brokers, {TopicCount} topics",
            metadata.Brokers.Count,
            metadata.Topics?.Count ?? 0);

        return Task.CompletedTask;
    }

    private async Task VerifyTopicMetadataAsync(ProducerConfig producerConfig, CancellationToken cancellationToken)
    {
        if (_options.VerifyTopics.Count == 0)
        {
            _logger.LogWarning("TopicMetadata strategy selected but no topics specified to verify");
            await VerifyClusterMetadataAsync(producerConfig, cancellationToken);
            return;
        }

        _logger.LogDebug("Verifying {Count} topics", _options.VerifyTopics.Count);

        using var adminClient = new AdminClientBuilder(producerConfig).Build();

        var metadataTimeout = _options.Timeout ?? TimeSpan.FromSeconds(15);
        var metadata = adminClient.GetMetadata(metadataTimeout);

        foreach (var topicName in _options.VerifyTopics)
        {
            var topicMetadata = metadata.Topics?.FirstOrDefault(t => t.Topic == topicName);

            if (topicMetadata == null)
            {
                var message = $"Topic '{topicName}' not found in cluster";

                if (_options.FailOnMissingTopics)
                {
                    _logger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                _logger.LogWarning("{Message} (continuing due to FailOnMissingTopics=false)", message);
            }
            else
            {
                _logger.LogDebug(
                    "Topic '{TopicName}' verified: {PartitionCount} partitions",
                    topicName,
                    topicMetadata.Partitions?.Count ?? 0);
            }
        }
    }

    private async Task PerformProducerTestAsync(ProducerConfig producerConfig, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing producer test");

        var testTopic = $"__ignition_test_{Guid.NewGuid():N}";

        using var adminClient = new AdminClientBuilder(producerConfig).Build();

        try
        {
            await adminClient.CreateTopicsAsync(new[]
            {
                new TopicSpecification
                {
                    Name = testTopic,
                    NumPartitions = 1,
                    ReplicationFactor = 1
                }
            });

            _logger.LogDebug("Temporary test topic '{TopicName}' created", testTopic);

            using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

            var testMessage = new Message<string, string>
            {
                Key = "test-key",
                Value = $"ignition-test-{Guid.NewGuid()}"
            };

            var deliveryResult = await producer.ProduceAsync(testTopic, testMessage, cancellationToken);

            if (deliveryResult.Status != PersistenceStatus.Persisted)
            {
                throw new InvalidOperationException($"Producer test failed: message not persisted (status: {deliveryResult.Status})");
            }

            _logger.LogDebug(
                "Producer test completed successfully: message delivered to partition {Partition} at offset {Offset}",
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value);
        }
        finally
        {
            try
            {
                await adminClient.DeleteTopicsAsync(new[] { testTopic });
                _logger.LogDebug("Temporary test topic '{TopicName}' deleted", testTopic);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary test topic '{TopicName}'", testTopic);
            }
        }
    }

    private Task VerifyConsumerGroupAsync(ProducerConfig producerConfig, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Verifying consumer group '{GroupId}'", _options.VerifyConsumerGroup);

        using var adminClient = new AdminClientBuilder(producerConfig).Build();

        var metadataTimeout = _options.Timeout ?? TimeSpan.FromSeconds(15);
        var groups = adminClient.ListGroups(metadataTimeout);

        var group = groups.FirstOrDefault(g => g.Group == _options.VerifyConsumerGroup);

        if (group == null)
        {
            var message = $"Consumer group '{_options.VerifyConsumerGroup}' not found";
            _logger.LogWarning(message);
        }
        else
        {
            _logger.LogDebug(
                "Consumer group '{GroupId}' verified: protocol type {ProtocolType}",
                _options.VerifyConsumerGroup,
                group.ProtocolType);
        }

        return Task.CompletedTask;
    }

    private async Task VerifySchemaRegistryAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.SchemaRegistryUrl, nameof(_options.SchemaRegistryUrl));

        _logger.LogDebug("Verifying Schema Registry at {Url}", _options.SchemaRegistryUrl);

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        try
        {
            var response = await httpClient.GetAsync(_options.SchemaRegistryUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Schema Registry verification failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            _logger.LogDebug("Schema Registry verified successfully");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Schema Registry verification failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Schema Registry verification timed out: {_options.SchemaRegistryUrl}", ex);
        }
    }
}
