using Microsoft.Extensions.Logging;
using Veggerby.Ignition.Postgres;

namespace Veggerby.Ignition.Postgres.Tests;

public class PostgresReadinessSignalTests
{
    [Fact]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyConnectionString_ThrowsArgumentException()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new PostgresReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal("Host=localhost;", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal("Host=localhost;", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;", options, logger);

        // act & assert
        signal.Name.Should().Be("postgres-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var options = new PostgresReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var options = new PostgresReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }
}
