using Confluent.Kafka;

using DotNet.Testcontainers.Builders;

using Testcontainers.Kafka;
using Xunit;

namespace Veggerby.Ignition.Kafka.Tests;

/// <summary>
/// Shared Kafka container fixture for integration tests.
/// This ensures all tests in the collection share a single Kafka container instance,
/// significantly reducing test execution time.
/// </summary>
public class KafkaContainerFixture : IAsyncLifetime
{
    private KafkaContainer? _kafkaContainer;

    public ProducerConfig? ProducerConfig { get; private set; }

    public async Task InitializeAsync()
    {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/confluent-local:7.7.1")
            .WithWaitStrategy(Wait.ForUnixContainer())
            .Build();

        await _kafkaContainer.StartAsync();

        ProducerConfig = new ProducerConfig
        {
            BootstrapServers = _kafkaContainer.GetBootstrapAddress()
        };
    }

    public async Task DisposeAsync()
    {
        if (_kafkaContainer != null)
        {
            await _kafkaContainer.DisposeAsync();
        }
    }
}

/// <summary>
/// Collection definition for Kafka integration tests.
/// All test classes in this collection will share the same Kafka container instance.
/// </summary>
[CollectionDefinition("Kafka Integration Tests")]
public class KafkaIntegrationTestCollection : ICollectionFixture<KafkaContainerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
