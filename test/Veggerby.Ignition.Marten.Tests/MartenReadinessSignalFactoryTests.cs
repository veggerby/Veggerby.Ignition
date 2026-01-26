namespace Veggerby.Ignition.Marten.Tests;

public class MartenReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        var options = new MartenReadinessOptions();

        // act
        var factory = new MartenReadinessSignalFactory(options);

        // assert
        factory.Name.Should().Be("marten-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MartenReadinessSignalFactory(null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        var options = new MartenReadinessOptions { Timeout = timeout };

        // act
        var factory = new MartenReadinessSignalFactory(options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        var options = new MartenReadinessOptions { Stage = 2 };

        // act
        var factory = new MartenReadinessSignalFactory(options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsMartenReadiness()
    {
        // arrange
        var options = new MartenReadinessOptions();

        // act
        var factory = new MartenReadinessSignalFactory(options);

        // assert
        factory.Name.Should().Be("marten-readiness");
    }
}
