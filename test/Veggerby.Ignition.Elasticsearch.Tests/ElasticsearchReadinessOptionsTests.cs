namespace Veggerby.Ignition.Elasticsearch.Tests;

public class ElasticsearchReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsTo10Seconds()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo200Milliseconds()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void VerificationStrategy_DefaultsToClusterHealth()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.VerificationStrategy.Should().Be(ElasticsearchVerificationStrategy.ClusterHealth);
    }

    [Fact]
    public void VerificationStrategy_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.VerificationStrategy = ElasticsearchVerificationStrategy.IndexExists;

        // assert
        options.VerificationStrategy.Should().Be(ElasticsearchVerificationStrategy.IndexExists);
    }

    [Fact]
    public void VerifyIndices_DefaultsToEmpty()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.VerifyIndices.Should().BeEmpty();
    }

    [Fact]
    public void VerifyIndices_CanBePopulated()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.VerifyIndices.Add("index1");
        options.VerifyIndices.Add("index2");

        // assert
        options.VerifyIndices.Should().Contain("index1");
        options.VerifyIndices.Should().Contain("index2");
    }

    [Fact]
    public void FailOnMissingIndices_DefaultsToTrue()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.FailOnMissingIndices.Should().BeTrue();
    }

    [Fact]
    public void FailOnMissingIndices_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.FailOnMissingIndices = false;

        // assert
        options.FailOnMissingIndices.Should().BeFalse();
    }

    [Fact]
    public void VerifyTemplate_DefaultsToNull()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.VerifyTemplate.Should().BeNull();
    }

    [Fact]
    public void VerifyTemplate_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.VerifyTemplate = "my-template";

        // assert
        options.VerifyTemplate.Should().Be("my-template");
    }

    [Fact]
    public void TestQueryIndex_DefaultsToNull()
    {
        // arrange & act
        var options = new ElasticsearchReadinessOptions();

        // assert
        options.TestQueryIndex.Should().BeNull();
    }

    [Fact]
    public void TestQueryIndex_CanBeSet()
    {
        // arrange
        var options = new ElasticsearchReadinessOptions();

        // act
        options.TestQueryIndex = "test-index";

        // assert
        options.TestQueryIndex.Should().Be("test-index");
    }
}
