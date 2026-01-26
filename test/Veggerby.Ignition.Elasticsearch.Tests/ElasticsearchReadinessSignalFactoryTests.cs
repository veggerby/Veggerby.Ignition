using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Elasticsearch.Tests;

public class ElasticsearchReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory = 
            _ => new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var options = new ElasticsearchReadinessOptions();

        // act
        var factory = new ElasticsearchReadinessSignalFactory(settingsFactory, options);

        // assert
        factory.Name.Should().Be("elasticsearch-readiness");
        factory.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullSettingsFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => 
            new ElasticsearchReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory = 
            _ => new ElasticsearchClientSettings(new Uri("http://localhost:9200"));

        // act & assert
        Assert.Throws<ArgumentNullException>(() => 
            new ElasticsearchReadinessSignalFactory(settingsFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory = 
            _ => new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var options = new ElasticsearchReadinessOptions { Timeout = timeout };

        // act
        var factory = new ElasticsearchReadinessSignalFactory(settingsFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory = 
            _ => new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var options = new ElasticsearchReadinessOptions { Stage = 2 };

        // act
        var factory = new ElasticsearchReadinessSignalFactory(settingsFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSignal()
    {
        // arrange
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory = 
            _ => new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var options = new ElasticsearchReadinessOptions();
        var factory = new ElasticsearchReadinessSignalFactory(settingsFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ElasticsearchReadinessSignal>>(_ => 
            Substitute.For<ILogger<ElasticsearchReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<ElasticsearchReadinessSignal>();
        signal.Name.Should().Be("elasticsearch-readiness");
    }

    [Fact]
    public void CreateSignal_UsesSettingsFactoryToResolveSettings()
    {
        // arrange
        var expectedUri = new Uri("http://custom.elasticsearch.local:9200");
        Func<IServiceProvider, ElasticsearchClientSettings> settingsFactory = 
            _ => new ElasticsearchClientSettings(expectedUri);
        var options = new ElasticsearchReadinessOptions();
        var factory = new ElasticsearchReadinessSignalFactory(settingsFactory, options);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ElasticsearchReadinessSignal>>(_ => 
            Substitute.For<ILogger<ElasticsearchReadinessSignal>>());
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
    }
}
