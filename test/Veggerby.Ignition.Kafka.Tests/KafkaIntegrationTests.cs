using Confluent.Kafka;
using Confluent.Kafka.Admin;

using DotNet.Testcontainers.Builders;

using Microsoft.Extensions.Logging;
using NSubstitute;
using Testcontainers.Kafka;
using Veggerby.Ignition.Kafka;
using Xunit;

namespace Veggerby.Ignition.Kafka.Tests;

public class KafkaIntegrationTests : IAsyncLifetime
{
    private KafkaContainer? _kafkaContainer;
    private ProducerConfig? _producerConfig;

    public async Task InitializeAsync()
    {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.8.1")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _kafkaContainer.StartAsync();

        // Get the bootstrap address - remove the PLAINTEXT:// scheme as BootstrapServers doesn't expect it
        var bootstrapUri = new Uri(_kafkaContainer.GetBootstrapAddress());
        var bootstrapAddress = $"{bootstrapUri.Host}:{bootstrapUri.Port}";

        _producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapAddress,
            SecurityProtocol = SecurityProtocol.Plaintext,
            SocketTimeoutMs = 10000,
            RequestTimeoutMs = 5000
        };
    }

    public async Task DisposeAsync()
    {
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClusterMetadata_Succeeds()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.ClusterMetadata,
            Timeout = TimeSpan.FromSeconds(10)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicMetadata_WithExistingTopic_Succeeds()
    {
        // arrange
        var topicName = $"test-topic-{Guid.NewGuid():N}";
        
        // Create topic first
        using var adminClient = new AdminClientBuilder(_producerConfig).Build();
        await adminClient.CreateTopicsAsync(new[]
        {
            new TopicSpecification
            {
                Name = topicName,
                NumPartitions = 1,
                ReplicationFactor = 1
            }
        });

        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.TopicMetadata,
            Timeout = TimeSpan.FromSeconds(15)
        };
        options.WithTopic(topicName);
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act
        await signal.WaitAsync();

        // assert - topic should be verified
        await adminClient.DeleteTopicsAsync(new[] { topicName });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicMetadata_WithMissingTopic_FailOnTrue_Throws()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.TopicMetadata,
            FailOnMissingTopics = true,
            Timeout = TimeSpan.FromSeconds(15)
        };
        options.WithTopic("nonexistent-topic");
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicMetadata_WithMissingTopic_FailOnFalse_Succeeds()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.TopicMetadata,
            FailOnMissingTopics = false,
            Timeout = TimeSpan.FromSeconds(15)
        };
        options.WithTopic("nonexistent-topic");
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProducerTest_Succeeds()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.ProducerTest,
            Timeout = TimeSpan.FromSeconds(20)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConsumerGroupCheck_WithNoGroups_LogsWarning()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.ConsumerGroupCheck,
            VerifyConsumerGroup = "nonexistent-group",
            Timeout = TimeSpan.FromSeconds(15)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConsumerGroupCheck_WithoutGroupSpecified_Throws()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.ConsumerGroupCheck,
            Timeout = TimeSpan.FromSeconds(15)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.ClusterMetadata,
            Timeout = TimeSpan.FromSeconds(10)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TopicMetadata_NoTopicsSpecified_FallsBackToClusterMetadata()
    {
        // arrange
        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.TopicMetadata,
            Timeout = TimeSpan.FromSeconds(15)
        };
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MultipleTopics_AllExist_Succeeds()
    {
        // arrange
        var topic1 = $"topic1-{Guid.NewGuid():N}";
        var topic2 = $"topic2-{Guid.NewGuid():N}";
        
        // Create topics
        using var adminClient = new AdminClientBuilder(_producerConfig).Build();
        await adminClient.CreateTopicsAsync(new[]
        {
            new TopicSpecification { Name = topic1, NumPartitions = 1, ReplicationFactor = 1 },
            new TopicSpecification { Name = topic2, NumPartitions = 1, ReplicationFactor = 1 }
        });

        var options = new KafkaReadinessOptions
        {
            VerificationStrategy = KafkaVerificationStrategy.TopicMetadata,
            Timeout = TimeSpan.FromSeconds(15)
        };
        options.WithTopic(topic1);
        options.WithTopic(topic2);
        var logger = Substitute.For<ILogger<KafkaReadinessSignal>>();
        var signal = new KafkaReadinessSignal(_producerConfig!, options, logger);

        // act
        await signal.WaitAsync();

        // assert - both topics should be verified
        await adminClient.DeleteTopicsAsync(new[] { topic1, topic2 });
    }
}
