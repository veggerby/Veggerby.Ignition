using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Azure;

namespace Veggerby.Ignition.Azure.Tests;

public class AzureQueueReadinessSignalTests
{
    [Fact]
    public void Constructor_NullQueueServiceClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();
        var logger = Substitute.For<ILogger<AzureQueueReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureQueueReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var client = Substitute.For<QueueServiceClient>();
        var options = new AzureQueueReadinessOptions();
        var logger = Substitute.For<ILogger<AzureQueueReadinessSignal>>();
        var signal = new AzureQueueReadinessSignal(client, options, logger);

        // act & assert
        signal.Name.Should().Be("azure-queue-readiness");
    }

    [Fact]
    public async Task WaitAsync_ConnectionOnly_SucceedsWhenPropertiesAvailable()
    {
        // arrange
        var client = Substitute.For<QueueServiceClient>();
        var mockResponse = Substitute.For<Response<QueueServiceProperties>>();
        client.GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var options = new AzureQueueReadinessOptions { QueueName = null };
        var logger = Substitute.For<ILogger<AzureQueueReadinessSignal>>();
        var signal = new AzureQueueReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).GetPropertiesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_VerifyQueueExists_ChecksQueueExistence()
    {
        // arrange
        var queueClient = Substitute.For<QueueClient>();
        queueClient.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(true, Substitute.For<Response>())));

        var client = Substitute.For<QueueServiceClient>();
        var mockResponse = Substitute.For<Response<QueueServiceProperties>>();
        client.GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));
        client.GetQueueClient(Arg.Any<string>()).Returns(queueClient);

        var options = new AzureQueueReadinessOptions
        {
            QueueName = "test-queue",
            VerifyQueueExists = true
        };
        var logger = Substitute.For<ILogger<AzureQueueReadinessSignal>>();
        var signal = new AzureQueueReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await queueClient.Received(1).ExistsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WaitAsync_QueueDoesNotExist_ThrowsException()
    {
        // arrange
        var queueClient = Substitute.For<QueueClient>();
        queueClient.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));

        var client = Substitute.For<QueueServiceClient>();
        var mockResponse = Substitute.For<Response<QueueServiceProperties>>();
        client.GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));
        client.GetQueueClient(Arg.Any<string>()).Returns(queueClient);

        var options = new AzureQueueReadinessOptions
        {
            QueueName = "missing-queue",
            VerifyQueueExists = true,
            CreateIfNotExists = false
        };
        var logger = Substitute.For<ILogger<AzureQueueReadinessSignal>>();
        var signal = new AzureQueueReadinessSignal(client, options, logger);

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
        ex.Message.Should().Contain("missing-queue");
    }

    [Fact]
    public async Task WaitAsync_CreateIfNotExists_CreatesQueue()
    {
        // arrange
        var queueClient = Substitute.For<QueueClient>();
        queueClient.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Response.FromValue(false, Substitute.For<Response>())));
        queueClient.CreateAsync(Arg.Any<IDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Substitute.For<Response>()));

        var client = Substitute.For<QueueServiceClient>();
        var mockResponse = Substitute.For<Response<QueueServiceProperties>>();
        client.GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));
        client.GetQueueClient(Arg.Any<string>()).Returns(queueClient);

        var options = new AzureQueueReadinessOptions
        {
            QueueName = "new-queue",
            VerifyQueueExists = true,
            CreateIfNotExists = true
        };
        var logger = Substitute.For<ILogger<AzureQueueReadinessSignal>>();
        var signal = new AzureQueueReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await queueClient.Received(1).CreateAsync(
            Arg.Any<IDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }
}
