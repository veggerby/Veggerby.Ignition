namespace Veggerby.Ignition.Azure.Tests;

public class AzureBlobReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureBlobReadinessOptions();

        // act
        var factory = new AzureBlobReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("azure-blob-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new AzureBlobReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureBlobReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureBlobReadinessOptions { Timeout = timeout };

        // act
        var factory = new AzureBlobReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureBlobReadinessOptions { Stage = 2 };

        // act
        var factory = new AzureBlobReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsAzureBlobReadiness()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureBlobReadinessOptions();

        // act
        var factory = new AzureBlobReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("azure-blob-readiness");
    }
}
