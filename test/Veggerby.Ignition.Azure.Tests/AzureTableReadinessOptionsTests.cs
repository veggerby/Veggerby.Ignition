namespace Veggerby.Ignition.Azure.Tests;

public class AzureTableReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();
        var timeout = TimeSpan.FromSeconds(30);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void TableName_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.TableName.Should().BeNull();
    }

    [Fact]
    public void TableName_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();

        // act
        options.TableName = "TestTable";

        // assert
        options.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void VerifyTableExists_DefaultsToTrue()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.VerifyTableExists.Should().BeTrue();
    }

    [Fact]
    public void VerifyTableExists_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();

        // act
        options.VerifyTableExists = false;

        // assert
        options.VerifyTableExists.Should().BeFalse();
    }

    [Fact]
    public void CreateIfNotExists_DefaultsToFalse()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.CreateIfNotExists.Should().BeFalse();
    }

    [Fact]
    public void CreateIfNotExists_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();

        // act
        options.CreateIfNotExists = true;

        // assert
        options.CreateIfNotExists.Should().BeTrue();
    }

    [Fact]
    public void MaxRetries_DefaultsTo3()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(3);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo100Milliseconds()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new AzureTableReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new AzureTableReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }
}
