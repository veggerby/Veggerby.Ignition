namespace Veggerby.Ignition.Azure.Tests;

public class AzureBlobReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureBlobReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void ContainerName_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureBlobReadinessOptions();

        // assert
        options.ContainerName.Should().BeNull();
    }

    [Fact]
    public void ContainerName_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();

        // act
        options.ContainerName = "test-container";

        // assert
        options.ContainerName.Should().Be("test-container");
    }

    [Fact]
    public void VerifyContainerExists_DefaultsToTrue()
    {
        // arrange & act
        var options = new AzureBlobReadinessOptions();

        // assert
        options.VerifyContainerExists.Should().BeTrue();
    }

    [Fact]
    public void VerifyContainerExists_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();

        // act
        options.VerifyContainerExists = false;

        // assert
        options.VerifyContainerExists.Should().BeFalse();
    }

    [Fact]
    public void CreateIfNotExists_DefaultsToFalse()
    {
        // arrange & act
        var options = new AzureBlobReadinessOptions();

        // assert
        options.CreateIfNotExists.Should().BeFalse();
    }

    [Fact]
    public void CreateIfNotExists_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();

        // act
        options.CreateIfNotExists = true;

        // assert
        options.CreateIfNotExists.Should().BeTrue();
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new AzureBlobReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new AzureBlobReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();
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
        var options = new AzureBlobReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
