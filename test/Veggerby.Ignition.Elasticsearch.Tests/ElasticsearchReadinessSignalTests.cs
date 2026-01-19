using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Elasticsearch;

namespace Veggerby.Ignition.Elasticsearch.Tests;

public class ElasticsearchReadinessSignalTests
{
    [Fact]
    public void Constructor_WithClient_ThrowsOnNullClient()
    {
        // arrange
        ElasticsearchClient client = null!;
        var options = new ElasticsearchReadinessOptions();
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new ElasticsearchReadinessSignal(client, options, logger));
    }

    [Fact]
    public void Constructor_WithClient_ThrowsOnNullOptions()
    {
        // arrange
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        ElasticsearchReadinessOptions options = null!;
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new ElasticsearchReadinessSignal(client, options, logger));
    }

    [Fact]
    public void Constructor_WithClient_ThrowsOnNullLogger()
    {
        // arrange
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        var options = new ElasticsearchReadinessOptions();
        ILogger<ElasticsearchReadinessSignal> logger = null!;

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new ElasticsearchReadinessSignal(client, options, logger));
    }

    [Fact]
    public void Constructor_WithFactory_ThrowsOnNullFactory()
    {
        // arrange
        Func<ElasticsearchClient> factory = null!;
        var options = new ElasticsearchReadinessOptions();
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() =>
            new ElasticsearchReadinessSignal(factory, options, logger));
    }

    [Fact]
    public void Name_ReturnsCorrectValue()
    {
        // arrange
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        var options = new ElasticsearchReadinessOptions();
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(client, options, logger);

        // act
        var name = signal.Name;

        // assert
        name.Should().Be("elasticsearch-readiness");
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var settings = new ElasticsearchClientSettings(new Uri("http://localhost:9200"));
        var client = new ElasticsearchClient(settings);
        var options = new ElasticsearchReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(client, options, logger);

        // act
        var timeout = signal.Timeout;

        // assert
        timeout.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void DefaultOptions_HasCorrectDefaults()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        options.MaxRetries.Should().Be(3);
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        options.VerificationStrategy.Should().Be(ElasticsearchVerificationStrategy.ClusterHealth);
        options.VerifyIndices.Should().BeEmpty();
        options.FailOnMissingIndices.Should().BeTrue();
        options.VerifyTemplate.Should().BeNull();
        options.TestQueryIndex.Should().BeNull();
        options.Stage.Should().BeNull();
    }
}
