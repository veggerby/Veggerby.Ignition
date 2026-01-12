using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Azure;

namespace Veggerby.Ignition.Azure.Tests;

public class AzureBlobReadinessSignalTests
{
    [Fact]
    public void Constructor_NullBlobServiceClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<BlobServiceClient>();
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobReadinessSignal(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<BlobServiceClient>();
        var options = new AzureBlobReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobReadinessSignal(client, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var client = Substitute.For<BlobServiceClient>();
        var options = new AzureBlobReadinessOptions();
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act & assert
        signal.Name.Should().Be("azure-blob-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var client = Substitute.For<BlobServiceClient>();
        var options = new AzureBlobReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var client = Substitute.For<BlobServiceClient>();
        var options = new AzureBlobReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }

    [Fact]
    public async Task WaitAsync_ConnectionOnly_SucceedsWhenAccountInfoAvailable()
    {
        // arrange
        var client = Substitute.For<BlobServiceClient>();
        var mockResponse = Substitute.For<Response<AccountInfo>>();
        client.GetAccountInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var options = new AzureBlobReadinessOptions { ContainerName = null };
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).GetAccountInfoAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_VerifyContainerExists_ChecksContainerExistence()
    {
        // arrange
        var containerClient = Substitute.For<BlobContainerClient>();
        containerClient.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(true, Substitute.For<Response>())));

        var client = Substitute.For<BlobServiceClient>();
        var mockResponse = Substitute.For<Response<AccountInfo>>();
        client.GetAccountInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));
        client.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);

        var options = new AzureBlobReadinessOptions
        {
            ContainerName = "test-container",
            VerifyContainerExists = true
        };
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await containerClient.Received(1).ExistsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_ContainerDoesNotExist_ThrowsException()
    {
        // arrange
        var containerClient = Substitute.For<BlobContainerClient>();
        containerClient.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

        var client = Substitute.For<BlobServiceClient>();
        var mockResponse = Substitute.For<Response<AccountInfo>>();
        client.GetAccountInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));
        client.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);

        var options = new AzureBlobReadinessOptions
        {
            ContainerName = "missing-container",
            VerifyContainerExists = true,
            CreateIfNotExists = false
        };
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("missing-container");
        ex.Message.Should().Contain("does not exist");
    }

    [Fact]
    public async Task WaitAsync_CreateIfNotExists_CreatesContainer()
    {
        // arrange
        var containerClient = Substitute.For<BlobContainerClient>();
        containerClient.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));
        containerClient.CreateAsync(Arg.Any<PublicAccessType>(), Arg.Any<IDictionary<string, string>>(), Arg.Any<BlobContainerEncryptionScopeOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Substitute.For<Response<BlobContainerInfo>>()));

        var client = Substitute.For<BlobServiceClient>();
        var mockResponse = Substitute.For<Response<AccountInfo>>();
        client.GetAccountInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));
        client.GetBlobContainerClient(Arg.Any<string>()).Returns(containerClient);

        var options = new AzureBlobReadinessOptions
        {
            ContainerName = "new-container",
            VerifyContainerExists = true,
            CreateIfNotExists = true
        };
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await containerClient.Received(1).CreateAsync(
            Arg.Any<PublicAccessType>(),
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<BlobContainerEncryptionScopeOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_CachesResult()
    {
        // arrange
        var client = Substitute.For<BlobServiceClient>();
        var mockResponse = Substitute.For<Response<AccountInfo>>();
        client.GetAccountInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var options = new AzureBlobReadinessOptions();
        var logger = Substitute.For<ILogger<AzureBlobReadinessSignal>>();
        var signal = new AzureBlobReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert
        await client.Received(1).GetAccountInfoAsync(Arg.Any<CancellationToken>());
    }
}
