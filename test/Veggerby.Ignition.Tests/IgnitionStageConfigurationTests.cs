namespace Veggerby.Ignition.Tests;

public class IgnitionStageConfigurationTests
{
    [Fact]
    public void EnsureStage_WithNewStage_AddsStage()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        config.EnsureStage(0, IgnitionExecutionMode.Parallel);

        // assert
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Parallel);
    }

    [Fact]
    public void EnsureStage_WithExistingStage_PreservesExistingMode()
    {
        // arrange
        var config = new IgnitionStageConfiguration();
        config.EnsureStage(0, IgnitionExecutionMode.Sequential);

        // act - try to change to Parallel
        config.EnsureStage(0, IgnitionExecutionMode.Parallel);

        // assert - should still be Sequential
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void EnsureStage_WithMultipleStages_StoresAllStages()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        config.EnsureStage(0, IgnitionExecutionMode.Sequential);
        config.EnsureStage(1, IgnitionExecutionMode.Parallel);
        config.EnsureStage(2, IgnitionExecutionMode.DependencyAware);

        // assert
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Sequential);
        config.GetExecutionMode(1).Should().Be(IgnitionExecutionMode.Parallel);
        config.GetExecutionMode(2).Should().Be(IgnitionExecutionMode.DependencyAware);
    }

    [Fact]
    public void GetExecutionMode_WithNonExistentStage_ReturnsNull()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        var mode = config.GetExecutionMode(99);

        // assert
        mode.Should().BeNull();
    }

    [Fact]
    public void StageExecutionModes_ReturnsAllConfiguredStages()
    {
        // arrange
        var config = new IgnitionStageConfiguration();
        config.EnsureStage(0, IgnitionExecutionMode.Sequential);
        config.EnsureStage(1, IgnitionExecutionMode.Parallel);
        config.EnsureStage(2, IgnitionExecutionMode.DependencyAware);

        // act
        var modes = config.StageExecutionModes;

        // assert
        modes.Should().HaveCount(3);
        modes[0].Should().Be(IgnitionExecutionMode.Sequential);
        modes[1].Should().Be(IgnitionExecutionMode.Parallel);
        modes[2].Should().Be(IgnitionExecutionMode.DependencyAware);
    }

    [Fact]
    public void StageExecutionModes_WithNoStages_ReturnsEmpty()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        var modes = config.StageExecutionModes;

        // assert
        modes.Should().BeEmpty();
    }

    [Fact]
    public void EnsureStage_WithNonSequentialStageNumbers_Works()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        config.EnsureStage(10, IgnitionExecutionMode.Parallel);
        config.EnsureStage(5, IgnitionExecutionMode.Sequential);
        config.EnsureStage(1, IgnitionExecutionMode.DependencyAware);

        // assert
        config.GetExecutionMode(1).Should().Be(IgnitionExecutionMode.DependencyAware);
        config.GetExecutionMode(5).Should().Be(IgnitionExecutionMode.Sequential);
        config.GetExecutionMode(10).Should().Be(IgnitionExecutionMode.Parallel);
    }

    [Fact]
    public void EnsureStage_WithZeroStage_Works()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        config.EnsureStage(0, IgnitionExecutionMode.Sequential);

        // assert
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Sequential);
    }

    [Fact]
    public void EnsureStage_WithAllExecutionModes_StoresCorrectly()
    {
        // arrange
        var config = new IgnitionStageConfiguration();

        // act
        config.EnsureStage(0, IgnitionExecutionMode.Parallel);
        config.EnsureStage(1, IgnitionExecutionMode.Sequential);
        config.EnsureStage(2, IgnitionExecutionMode.DependencyAware);
        config.EnsureStage(3, IgnitionExecutionMode.Staged);

        // assert
        config.GetExecutionMode(0).Should().Be(IgnitionExecutionMode.Parallel);
        config.GetExecutionMode(1).Should().Be(IgnitionExecutionMode.Sequential);
        config.GetExecutionMode(2).Should().Be(IgnitionExecutionMode.DependencyAware);
        config.GetExecutionMode(3).Should().Be(IgnitionExecutionMode.Staged);
    }
}
