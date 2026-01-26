namespace Veggerby.Ignition.Azure.Tests;

public class AzureTableReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureTableReadinessOptions();

        // act
        var factory = new AzureTableReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("azure-table-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new AzureTableReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureTableReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new AzureTableReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureTableReadinessOptions { Timeout = timeout };

        // act
        var factory = new AzureTableReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureTableReadinessOptions { Stage = 2 };

        // act
        var factory = new AzureTableReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsAzureTableReadiness()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net";
        var options = new AzureTableReadinessOptions();

        // act
        var factory = new AzureTableReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("azure-table-readiness");
    }
}
