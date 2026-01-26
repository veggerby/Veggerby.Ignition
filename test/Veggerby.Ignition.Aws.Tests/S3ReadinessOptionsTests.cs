namespace Veggerby.Ignition.Aws.Tests;

public class S3ReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void BucketName_DefaultsToNull()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.BucketName.Should().BeNull();
    }

    [Fact]
    public void BucketName_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();

        // act
        options.BucketName = "my-bucket";

        // assert
        options.BucketName.Should().Be("my-bucket");
    }

    [Fact]
    public void Region_DefaultsToNull()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.Region.Should().BeNull();
    }

    [Fact]
    public void Region_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();

        // act
        options.Region = "us-west-2";

        // assert
        options.Region.Should().Be("us-west-2");
    }

    [Fact]
    public void VerifyBucketAccess_DefaultsToTrue()
    {
        // arrange & act
        var options = new S3ReadinessOptions();

        // assert
        options.VerifyBucketAccess.Should().BeTrue();
    }

    [Fact]
    public void VerifyBucketAccess_CanBeSet()
    {
        // arrange
        var options = new S3ReadinessOptions();

        // act
        options.VerifyBucketAccess = false;

        // assert
        options.VerifyBucketAccess.Should().BeFalse();
    }
}
