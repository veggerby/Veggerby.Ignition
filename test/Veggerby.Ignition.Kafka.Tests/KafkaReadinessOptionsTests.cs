namespace Veggerby.Ignition.Kafka.Tests;

public class KafkaReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsTo30Seconds()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var timeout = TimeSpan.FromSeconds(60);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo8()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(8);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo500Milliseconds()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void VerificationStrategy_DefaultsToClusterMetadata()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.VerificationStrategy.Should().Be(KafkaVerificationStrategy.ClusterMetadata);
    }

    [Fact]
    public void VerificationStrategy_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.VerificationStrategy = KafkaVerificationStrategy.TopicMetadata;

        // assert
        options.VerificationStrategy.Should().Be(KafkaVerificationStrategy.TopicMetadata);
    }

    [Fact]
    public void VerifyTopics_DefaultsToEmpty()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.VerifyTopics.Should().BeEmpty();
    }

    [Fact]
    public void VerifyTopics_CanBePopulated()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.VerifyTopics.Add("topic1");
        options.VerifyTopics.Add("topic2");

        // assert
        options.VerifyTopics.Should().Contain("topic1");
        options.VerifyTopics.Should().Contain("topic2");
    }

    [Fact]
    public void FailOnMissingTopics_DefaultsToTrue()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.FailOnMissingTopics.Should().BeTrue();
    }

    [Fact]
    public void FailOnMissingTopics_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.FailOnMissingTopics = false;

        // assert
        options.FailOnMissingTopics.Should().BeFalse();
    }

    [Fact]
    public void VerifyConsumerGroup_DefaultsToNull()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.VerifyConsumerGroup.Should().BeNull();
    }

    [Fact]
    public void VerifyConsumerGroup_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.VerifyConsumerGroup = "my-consumer-group";

        // assert
        options.VerifyConsumerGroup.Should().Be("my-consumer-group");
    }

    [Fact]
    public void SchemaRegistryUrl_DefaultsToNull()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.SchemaRegistryUrl.Should().BeNull();
    }

    [Fact]
    public void SchemaRegistryUrl_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.SchemaRegistryUrl = "http://localhost:8081";

        // assert
        options.SchemaRegistryUrl.Should().Be("http://localhost:8081");
    }

    [Fact]
    public void VerifySchemaRegistry_DefaultsToFalse()
    {
        // arrange & act
        var options = new KafkaReadinessOptions();

        // assert
        options.VerifySchemaRegistry.Should().BeFalse();
    }

    [Fact]
    public void VerifySchemaRegistry_CanBeSet()
    {
        // arrange
        var options = new KafkaReadinessOptions();

        // act
        options.VerifySchemaRegistry = true;

        // assert
        options.VerifySchemaRegistry.Should().BeTrue();
    }
}
