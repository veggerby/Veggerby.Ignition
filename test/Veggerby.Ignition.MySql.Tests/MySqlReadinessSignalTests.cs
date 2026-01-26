using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Veggerby.Ignition.MySql;

namespace Veggerby.Ignition.MySql.Tests;

public class MySqlReadinessSignalTests
{
    [Fact]
    public void Constructor_WithConnectionString_ThrowsOnNullConnectionString()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentException>(() => new MySqlReadinessSignal(string.Empty, options, logger));
    }

    [Fact]
    public void Constructor_WithConnectionString_ThrowsOnNullOptions()
    {
        // arrange
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignal("Server=localhost", null!, logger));
    }

    [Fact]
    public void Constructor_WithConnectionString_ThrowsOnNullLogger()
    {
        // arrange
        var options = new MySqlReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignal("Server=localhost", options, null!));
    }

    [Fact]
    public void Constructor_WithConnectionFactory_ThrowsOnNullFactory()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new MySqlReadinessSignal((Func<MySqlConnection>)null!, options, logger));
    }

    [Fact]
    public void Name_ReturnsExpectedValue()
    {
        // arrange
        var options = new MySqlReadinessOptions();
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal("Server=localhost", options, logger);

        // act
        var name = signal.Name;

        // assert
        name.Should().Be("mysql-readiness");
    }

    [Fact]
    public void Timeout_ReturnsConfiguredValue()
    {
        // arrange
        var expectedTimeout = TimeSpan.FromSeconds(45);
        var options = new MySqlReadinessOptions
        {
            Timeout = expectedTimeout
        };
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal("Server=localhost", options, logger);

        // act
        var timeout = signal.Timeout;

        // assert
        timeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void Timeout_ReturnsDefaultValue()
    {
        // arrange
        var options = new MySqlReadinessOptions(); // Default is 30 seconds
        var logger = Substitute.For<ILogger<MySqlReadinessSignal>>();
        var signal = new MySqlReadinessSignal("Server=localhost", options, logger);

        // act
        var timeout = signal.Timeout;

        // assert
        timeout.Should().Be(TimeSpan.FromSeconds(30));
    }
}
