namespace Veggerby.Ignition.Azure.Tests;

public class AzureQueueReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureQueueReadinessOptions();

        // act
        var factory = new AzureQueueReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("azure-queue-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new AzureQueueReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureQueueReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureQueueReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureQueueReadinessOptions { Timeout = timeout };

        // act
        var factory = new AzureQueueReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureQueueReadinessOptions { Stage = 2 };

        // act
        var factory = new AzureQueueReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsAzureQueueReadiness()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureQueueReadinessOptions();

        // act
        var factory = new AzureQueueReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("azure-queue-readiness");
    }
}
