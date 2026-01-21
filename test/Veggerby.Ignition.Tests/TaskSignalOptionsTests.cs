namespace Veggerby.Ignition.Tests;

public class TaskSignalOptionsTests
{
    [Fact]
    public void Timeout_DefaultsToNull()
    {
        // arrange & act
        var options = new TaskSignalOptions();

        // assert
        options.Timeout.Should().BeNull();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // arrange
        var options = new TaskSignalOptions();
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
        var options = new TaskSignalOptions();

        // assert
        options.Stage.Should().BeNull();
    }

    [Fact]
    public void Stage_CanBeSet()
    {
        // arrange
        var options = new TaskSignalOptions();

        // act
        options.Stage = 2;

        // assert
        options.Stage.Should().Be(2);
    }

    [Fact]
    public void ExecutionMode_DefaultsToParallel()
    {
        // arrange & act
        var options = new TaskSignalOptions();

        // assert
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Parallel);
    }

    [Fact]
    public void ExecutionMode_CanBeSet()
    {
        // arrange
        var options = new TaskSignalOptions();

        // act
        options.ExecutionMode = IgnitionExecutionMode.Sequential;

        // assert
        options.ExecutionMode.Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // arrange
        var timeout = TimeSpan.FromMinutes(5);
        var stage = 3;
        var executionMode = IgnitionExecutionMode.DependencyAware;

        // act
        var options = new TaskSignalOptions
        {
            Timeout = timeout,
            Stage = stage,
            ExecutionMode = executionMode
        };

        // assert
        options.Timeout.Should().Be(timeout);
        options.Stage.Should().Be(stage);
        options.ExecutionMode.Should().Be(executionMode);
    }

    [Fact]
    public void Stage_CanBeSetToZero()
    {
        // arrange
        var options = new TaskSignalOptions();

        // act
        options.Stage = 0;

        // assert
        options.Stage.Should().Be(0);
    }
}
