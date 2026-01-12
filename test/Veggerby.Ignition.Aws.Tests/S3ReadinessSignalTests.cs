using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Aws;

namespace Veggerby.Ignition.Aws.Tests;

public class S3ReadinessSignalTests
{
    [Fact]
    public void Constructor_NullS3Client_ThrowsArgumentNullException()
    {
        // arrange
        var options = new S3ReadinessOptions();
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new S3ReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new S3ReadinessSignal(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new S3ReadinessSignal(client, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions();
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act & assert
        signal.Name.Should().Be("s3-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var client = Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        var options = new S3ReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionOnly_SucceedsWhenListBucketsAvailable()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ListBucketsResponse { HttpStatusCode = HttpStatusCode.OK }));

        var options = new S3ReadinessOptions { BucketName = null };
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).ListBucketsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_VerifyBucketAccess_ChecksBucketLocation()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        client.GetBucketLocationAsync(Arg.Any<GetBucketLocationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetBucketLocationResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Location = Amazon.S3.S3Region.USEast1
            }));

        var options = new S3ReadinessOptions
        {
            BucketName = "test-bucket",
            VerifyBucketAccess = true
        };
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).GetBucketLocationAsync(
            Arg.Is<GetBucketLocationRequest>(r => r.BucketName == "test-bucket"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_BucketDoesNotExist_ThrowsException()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        client.GetBucketLocationAsync(Arg.Any<GetBucketLocationRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<GetBucketLocationResponse>>(_ => throw new AmazonS3Exception("Not Found")
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var options = new S3ReadinessOptions
        {
            BucketName = "missing-bucket",
            VerifyBucketAccess = true
        };
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("missing-bucket");
        ex.Message.Should().Contain("does not exist");
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_CachesResult()
    {
        // arrange
        var client = Substitute.For<IAmazonS3>();
        client.GetBucketLocationAsync(Arg.Any<GetBucketLocationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetBucketLocationResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Location = Amazon.S3.S3Region.USEast1
            }));

        var options = new S3ReadinessOptions
        {
            BucketName = "test-bucket",
            VerifyBucketAccess = true
        };
        var logger = Substitute.For<ILogger<S3ReadinessSignal>>();
        var signal = new S3ReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert
        await client.Received(1).GetBucketLocationAsync(
            Arg.Any<GetBucketLocationRequest>(),
            Arg.Any<CancellationToken>());
    }
}
