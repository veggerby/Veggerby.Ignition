namespace Veggerby.Ignition.Azure.Tests;

public class AzureQueueReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void QueueName_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.QueueName.Should().BeNull();
    }

    [Fact]
    public void QueueName_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();

        // act
        options.QueueName = "test-queue";

        // assert
        options.QueueName.Should().Be("test-queue");
    }

    [Fact]
    public void VerifyQueueExists_DefaultsToTrue()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.VerifyQueueExists.Should().BeTrue();
    }

    [Fact]
    public void VerifyQueueExists_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();

        // act
        options.VerifyQueueExists = false;

        // assert
        options.VerifyQueueExists.Should().BeFalse();
    }

    [Fact]
    public void CreateIfNotExists_DefaultsToFalse()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.CreateIfNotExists.Should().BeFalse();
    }

    [Fact]
    public void CreateIfNotExists_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();

        // act
        options.CreateIfNotExists = true;

        // assert
        options.CreateIfNotExists.Should().BeTrue();
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureQueueReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
