namespace Veggerby.Ignition.MariaDb.Tests;

public class MariaDbReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsTo30Seconds()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var timeout = TimeSpan.FromSeconds(60);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo8()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(8);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo500Milliseconds()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();
        var delay = TimeSpan.FromSeconds(1);

        // act
        options.RetryDelay = delay;

        // assert
        options.RetryDelay.Should().Be(delay);
    }

    [Fact]
    public void VerificationStrategy_DefaultsToPing()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.VerificationStrategy.Should().Be(MariaDbVerificationStrategy.Ping);
    }

    [Fact]
    public void VerificationStrategy_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.VerificationStrategy = MariaDbVerificationStrategy.TableExists;

        // assert
        options.VerificationStrategy.Should().Be(MariaDbVerificationStrategy.TableExists);
    }

    [Fact]
    public void VerifyTables_DefaultsToEmpty()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.VerifyTables.Should().BeEmpty();
    }

    [Fact]
    public void VerifyTables_CanBePopulated()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.VerifyTables.Add("table1");
        options.VerifyTables.Add("table2");

        // assert
        options.VerifyTables.Should().Contain("table1");
        options.VerifyTables.Should().Contain("table2");
    }

    [Fact]
    public void FailOnMissingTables_DefaultsToTrue()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.FailOnMissingTables.Should().BeTrue();
    }

    [Fact]
    public void FailOnMissingTables_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.FailOnMissingTables = false;

        // assert
        options.FailOnMissingTables.Should().BeFalse();
    }

    [Fact]
    public void Schema_DefaultsToNull()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.Schema.Should().BeNull();
    }

    [Fact]
    public void Schema_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.Schema = "my_schema";

        // assert
        options.Schema.Should().Be("my_schema");
    }

    [Fact]
    public void TestQuery_DefaultsToNull()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.TestQuery.Should().BeNull();
    }

    [Fact]
    public void TestQuery_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.TestQuery = "SELECT 1";

        // assert
        options.TestQuery.Should().Be("SELECT 1");
    }

    [Fact]
    public void ExpectedMinimumRows_DefaultsToNull()
    {
        // arrange & act
        var options = new MariaDbReadinessOptions();

        // assert
        options.ExpectedMinimumRows.Should().BeNull();
    }

    [Fact]
    public void ExpectedMinimumRows_CanBeSet()
    {
        // arrange
        var options = new MariaDbReadinessOptions();

        // act
        options.ExpectedMinimumRows = 5;

        // assert
        options.ExpectedMinimumRows.Should().Be(5);
    }
}
