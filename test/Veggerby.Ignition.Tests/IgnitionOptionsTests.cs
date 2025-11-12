namespace Veggerby.Ignition.Tests;

public class IgnitionOptionsTests
{
    [Fact]
    public void GlobalTimeout_SetNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var options = new IgnitionOptions();

        // act & assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.GlobalTimeout = TimeSpan.FromSeconds(-1));
        ex.ParamName.Should().Be("value");
        ex.Message.Should().Contain("Global timeout cannot be negative");
    }

    [Fact]
    public void GlobalTimeout_SetZeroValue_Succeeds()
    {
        // arrange
        var options = new IgnitionOptions
        {
            // act
            GlobalTimeout = TimeSpan.Zero
        };

        // assert
        options.GlobalTimeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void SlowHandleLogCount_SetNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var options = new IgnitionOptions();

        // act & assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.SlowHandleLogCount = -1);
        ex.ParamName.Should().Be("value");
        ex.Message.Should().Contain("Slow handle log count cannot be negative");
    }

    [Fact]
    public void SlowHandleLogCount_SetZeroValue_Succeeds()
    {
        // arrange
        var options = new IgnitionOptions
        {
            // act
            SlowHandleLogCount = 0
        };

        // assert
        options.SlowHandleLogCount.Should().Be(0);
    }

    [Fact]
    public void MaxDegreeOfParallelism_SetZero_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var options = new IgnitionOptions();

        // act & assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDegreeOfParallelism = 0);
        ex.ParamName.Should().Be("value");
        ex.Message.Should().Contain("Max degree of parallelism must be greater than zero");
    }

    [Fact]
    public void MaxDegreeOfParallelism_SetNegativeValue_ThrowsArgumentOutOfRangeException()
    {
        // arrange
        var options = new IgnitionOptions();

        // act & assert
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxDegreeOfParallelism = -1);
        ex.ParamName.Should().Be("value");
        ex.Message.Should().Contain("Max degree of parallelism must be greater than zero");
    }

    [Fact]
    public void MaxDegreeOfParallelism_SetNull_Succeeds()
    {
        // arrange
        var options = new IgnitionOptions
        {
            // act
            MaxDegreeOfParallelism = null
        };

        // assert
        options.MaxDegreeOfParallelism.Should().BeNull();
    }

    [Fact]
    public void MaxDegreeOfParallelism_SetPositiveValue_Succeeds()
    {
        // arrange
        var options = new IgnitionOptions
        {
            // act
            MaxDegreeOfParallelism = 4
        };

        // assert
        options.MaxDegreeOfParallelism.Should().Be(4);
    }
}
