namespace Veggerby.Ignition.Pulsar.DotPulsar.Tests;

public class PulsarReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsTo30Seconds()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();
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
        var options = new PulsarReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo8()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(8);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo500Milliseconds()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void VerificationStrategy_DefaultsToClusterHealth()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.VerificationStrategy.Should().Be(PulsarVerificationStrategy.ClusterHealth);
    }

    [Fact]
    public void VerificationStrategy_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.VerificationStrategy = PulsarVerificationStrategy.TopicMetadata;

        // assert
        options.VerificationStrategy.Should().Be(PulsarVerificationStrategy.TopicMetadata);
    }

    [Fact]
    public void VerifyTopics_DefaultsToEmpty()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.VerifyTopics.Should().BeEmpty();
    }

    [Fact]
    public void VerifyTopics_CanBePopulated()
    {
        // arrange
        var options = new PulsarReadinessOptions();

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
        var options = new PulsarReadinessOptions();

        // assert
        options.FailOnMissingTopics.Should().BeTrue();
    }

    [Fact]
    public void FailOnMissingTopics_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.FailOnMissingTopics = false;

        // assert
        options.FailOnMissingTopics.Should().BeFalse();
    }

    [Fact]
    public void VerifySubscription_DefaultsToNull()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.VerifySubscription.Should().BeNull();
    }

    [Fact]
    public void VerifySubscription_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.VerifySubscription = "my-subscription";

        // assert
        options.VerifySubscription.Should().Be("my-subscription");
    }

    [Fact]
    public void SubscriptionTopic_DefaultsToNull()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.SubscriptionTopic.Should().BeNull();
    }

    [Fact]
    public void SubscriptionTopic_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.SubscriptionTopic = "my-topic";

        // assert
        options.SubscriptionTopic.Should().Be("my-topic");
    }

    [Fact]
    public void AdminServiceUrl_DefaultsToNull()
    {
        // arrange & act
        var options = new PulsarReadinessOptions();

        // assert
        options.AdminServiceUrl.Should().BeNull();
    }

    [Fact]
    public void AdminServiceUrl_CanBeSet()
    {
        // arrange
        var options = new PulsarReadinessOptions();

        // act
        options.AdminServiceUrl = "http://localhost:8080";

        // assert
        options.AdminServiceUrl.Should().Be("http://localhost:8080");
    }
}
