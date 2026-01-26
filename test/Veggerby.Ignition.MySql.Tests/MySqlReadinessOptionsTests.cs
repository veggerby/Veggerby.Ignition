namespace Veggerby.Ignition.MySql.Tests;

public class MySqlReadinessOptionsTests
{
    [Fact]
    public void Timeout_DefaultsTo30Seconds()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var timeout = TimeSpan.FromSeconds(45);

        // act
        options.Timeout = timeout;

        // assert
        options.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_DefaultsToNull()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void MaxRetries_DefaultsTo8()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.MaxRetries.Should().Be(8);
    }

    [Fact]
    public void MaxRetries_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.MaxRetries = 5;

        // assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void RetryDelay_DefaultsTo500Milliseconds()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.RetryDelay.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void RetryDelay_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();
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
        var options = new MySqlReadinessOptions();

        // assert
        options.VerificationStrategy.Should().Be(MySqlVerificationStrategy.Ping);
    }

    [Fact]
    public void VerificationStrategy_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.VerificationStrategy = MySqlVerificationStrategy.TableExists;

        // assert
        options.VerificationStrategy.Should().Be(MySqlVerificationStrategy.TableExists);
    }

    [Fact]
    public void VerifyTables_DefaultsToEmpty()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.VerifyTables.Should().BeEmpty();
    }

    [Fact]
    public void VerifyTables_CanBePopulated()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.VerifyTables.Add("users");
        options.VerifyTables.Add("products");

        // assert
        options.VerifyTables.Should().Contain("users");
        options.VerifyTables.Should().Contain("products");
    }

    [Fact]
    public void FailOnMissingTables_DefaultsToTrue()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.FailOnMissingTables.Should().BeTrue();
    }

    [Fact]
    public void FailOnMissingTables_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.FailOnMissingTables = false;

        // assert
        options.FailOnMissingTables.Should().BeFalse();
    }

    [Fact]
    public void Schema_DefaultsToNull()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.Schema.Should().BeNull();
    }

    [Fact]
    public void Schema_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.Schema = "my_database";

        // assert
        options.Schema.Should().Be("my_database");
    }

    [Fact]
    public void TestQuery_DefaultsToNull()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.TestQuery.Should().BeNull();
    }

    [Fact]
    public void TestQuery_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.TestQuery = "SELECT 1";

        // assert
        options.TestQuery.Should().Be("SELECT 1");
    }

    [Fact]
    public void ExpectedMinimumRows_DefaultsToNull()
    {
        // arrange & act
        var options = new MySqlReadinessOptions();

        // assert
        options.ExpectedMinimumRows.Should().BeNull();
    }

    [Fact]
    public void ExpectedMinimumRows_CanBeSet()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act
        options.ExpectedMinimumRows = 10;

        // assert
        options.ExpectedMinimumRows.Should().Be(10);
    }
}
