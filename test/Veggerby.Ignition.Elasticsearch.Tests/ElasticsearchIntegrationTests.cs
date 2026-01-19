using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using Testcontainers.Elasticsearch;
using Veggerby.Ignition.Elasticsearch;

namespace Veggerby.Ignition.Elasticsearch.Tests;

public class ElasticsearchIntegrationTests : IAsyncLifetime
{
    private ElasticsearchContainer? _elasticsearchContainer;
    private ElasticsearchClient? _client;

    public async Task InitializeAsync()
    {
        _elasticsearchContainer = new ElasticsearchBuilder()
            .WithImage("elasticsearch:8.17.0")
            .Build();

        await _elasticsearchContainer.StartAsync();

        var settings = new ElasticsearchClientSettings(new Uri(_elasticsearchContainer.GetConnectionString()))
            .ServerCertificateValidationCallback((o, cert, chain, errors) => true);

        _client = new ElasticsearchClient(settings);

        // Wait a bit for cluster to be fully ready
        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        if (_elasticsearchContainer != null)
        {
            await _elasticsearchContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ClusterHealth_Succeeds()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IndexExists_SucceedsWhenIndexExists()
    {
        // arrange
        // Create test index
        var indexName = $"test-index-{Guid.NewGuid():N}";
        await _client!.Indices.CreateAsync(indexName);

        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        options.VerifyIndices.Add(indexName);

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        try
        {
            // act & assert
            await signal.WaitAsync();
        }
        finally
        {
            // cleanup
            await _client!.Indices.DeleteAsync(indexName);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IndexExists_FailsWhenIndexMissing()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists,
            FailOnMissingIndices = true,
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromMilliseconds(100)
        };
        options.VerifyIndices.Add("nonexistent-index");

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IndexExists_SucceedsWhenIndexMissingAndFailOnMissingFalse()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists,
            FailOnMissingIndices = false,
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromMilliseconds(100)
        };
        options.VerifyIndices.Add("nonexistent-index");

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TemplateValidation_SucceedsWhenTemplateExists()
    {
        // arrange
        var templateName = $"test-template-{Guid.NewGuid():N}";

        // Create index template
        await _client!.Indices.PutIndexTemplateAsync(templateName, d => d
            .IndexPatterns("test-*")
            .Template(t => t
                .Settings(s => s.NumberOfShards(1))));

        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.TemplateValidation,
            VerifyTemplate = templateName,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        try
        {
            // act & assert
            await signal.WaitAsync();
        }
        finally
        {
            // cleanup
            await _client!.Indices.DeleteIndexTemplateAsync(templateName);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TemplateValidation_FailsWhenTemplateMissing()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.TemplateValidation,
            VerifyTemplate = "nonexistent-template",
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromMilliseconds(100)
        };

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task QueryTest_SucceedsWithValidIndex()
    {
        // arrange
        var indexName = $"test-query-index-{Guid.NewGuid():N}";

        // Create index and add a test document
        await _client!.Indices.CreateAsync(indexName);
        await _client!.IndexAsync(new { message = "test" }, idx => idx.Index(indexName));
        await _client!.Indices.RefreshAsync(indexName);

        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.QueryTest,
            TestQueryIndex = indexName,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        try
        {
            // act & assert
            await signal.WaitAsync();
        }
        finally
        {
            // cleanup
            await _client!.Indices.DeleteAsync(indexName);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task QueryTest_FailsWithNonexistentIndex()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.QueryTest,
            TestQueryIndex = "nonexistent-query-index",
            MaxRetries = 3,
            RetryDelay = TimeSpan.FromMilliseconds(100)
        };

        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Idempotency_MultipleCallsReturnSameTask()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };
        var logger = Substitute.For<ILogger<ElasticsearchReadinessSignal>>();
        var signal = new ElasticsearchReadinessSignal(_client!, options, logger);

        // act
        var task1 = signal.WaitAsync();
        var task2 = signal.WaitAsync();
        var task3 = signal.WaitAsync();

        await Task.WhenAll(task1, task2, task3);

        // assert
        task1.Should().BeSameAs(task2);
        task2.Should().BeSameAs(task3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Factory_CreatesWorkingSignal()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions
        {
            VerificationStrategy = ElasticsearchVerificationStrategy.ClusterHealth,
            MaxRetries = 10,
            RetryDelay = TimeSpan.FromMilliseconds(500)
        };

        var factory = new ElasticsearchReadinessSignalFactory(
            sp =>
            {
                return new ElasticsearchClientSettings(new Uri(_elasticsearchContainer!.GetConnectionString()))
                    .ServerCertificateValidationCallback((o, cert, chain, errors) => true);
            },
            options);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ILogger<ElasticsearchReadinessSignal>))
            .Returns(Substitute.For<ILogger<ElasticsearchReadinessSignal>>());

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Name.Should().Be("elasticsearch-readiness");
        await signal.WaitAsync();
    }
}
