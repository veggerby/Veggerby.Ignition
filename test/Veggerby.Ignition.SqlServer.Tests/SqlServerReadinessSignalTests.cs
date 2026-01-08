using Microsoft.Extensions.Logging;
using Veggerby.Ignition.SqlServer;

namespace Veggerby.Ignition.SqlServer.Tests;

public class SqlServerReadinessSignalTests
{
    [Fact]
    public void Constructor_NullConnectionString_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal(null!, options, logger));
    }

    [Fact]
    public void Constructor_EmptyConnectionString_ThrowsArgumentException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new SqlServerReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // arrange
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal("Server=localhost;", null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal("Server=localhost;", options, null!));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Name.Should().Be("sqlserver-readiness");
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(10);
        var options = new SqlServerReadinessOptions { Timeout = timeout };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Timeout_NullOptionsTimeout_ReturnsNull()
    {
        // arrange
        var options = new SqlServerReadinessOptions { Timeout = null };
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;", options, logger);

        // act & assert
        signal.Timeout.Should().BeNull();
    }
}
