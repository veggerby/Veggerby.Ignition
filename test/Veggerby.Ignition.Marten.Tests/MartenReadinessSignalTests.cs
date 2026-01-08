using Marten;
using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Marten;

namespace Veggerby.Ignition.Marten.Tests;

public class MartenReadinessSignalTests
{
    [Fact]
    public void Constructor_NullDocumentStore_ThrowsArgumentNullException()
    {
        // arrange
        var options = new MartenReadinessOptions();
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MartenReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MartenReadinessSignal(documentStore, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var options = new MartenReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MartenReadinessSignal(documentStore, options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var options = new MartenReadinessOptions();
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);

        // act & assert
        signal.Name.Should().Be("marten-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var timeout = TimeSpan.FromSeconds(10);
        var options = new MartenReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var options = new MartenReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }
}
