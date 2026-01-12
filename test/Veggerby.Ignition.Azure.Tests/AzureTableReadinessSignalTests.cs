using Azure;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Azure;

namespace Veggerby.Ignition.Azure.Tests;

public class AzureTableReadinessSignalTests
{
    [Fact]
    public void Constructor_NullTableServiceClient_ThrowsArgumentNullException()
    {
        // arrange
        var options = new AzureTableReadinessOptions();
        var logger = Substitute.For<ILogger<AzureTableReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureTableReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<TableServiceClient>();
        var logger = Substitute.For<ILogger<AzureTableReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureTableReadinessSignal(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var client = Substitute.For<TableServiceClient>();
        var options = new AzureTableReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureTableReadinessSignal(client, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var client = Substitute.For<TableServiceClient>();
        var options = new AzureTableReadinessOptions();
        var logger = Substitute.For<ILogger<AzureTableReadinessSignal>>();
        var signal = new AzureTableReadinessSignal(client, options, logger);

        // act & assert
        signal.Name.Should().Be("azure-table-readiness");
    }

    [Fact]
    public async Task WaitAsync_ConnectionOnly_SucceedsWhenPropertiesAvailable()
    {
        // arrange
        var client = Substitute.For<TableServiceClient>();
        var mockResponse = Substitute.For<Response<TableServiceProperties>>();
        client.GetPropertiesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockResponse));

        var options = new AzureTableReadinessOptions { TableName = null };
        var logger = Substitute.For<ILogger<AzureTableReadinessSignal>>();
        var signal = new AzureTableReadinessSignal(client, options, logger);

        // act
        await signal.WaitAsync();

        // assert
        await client.Received(1).GetPropertiesAsync(Arg.Any<CancellationToken>());
    }
}
