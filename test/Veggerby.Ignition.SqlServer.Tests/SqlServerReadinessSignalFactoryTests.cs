using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.SqlServer.Tests;

public class SqlServerReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new SqlServerReadinessOptions();

        // act
        var factory = new SqlServerReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("sqlserver-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new SqlServerReadinessOptions { Timeout = timeout };

        // act
        var factory = new SqlServerReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new SqlServerReadinessOptions { Stage = 2 };

        // act
        var factory = new SqlServerReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsSqlServerReadiness()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Server=localhost;Database=test;";
        var options = new SqlServerReadinessOptions();

        // act
        var factory = new SqlServerReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("sqlserver-readiness");
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsSqlServerReadinessSignal()
    {
        // arrange
        const string connectionString = "Server=localhost;Database=test;";
        string ConnectionStringFactory(IServiceProvider sp) => connectionString;
        var options = new SqlServerReadinessOptions();
        var factory = new SqlServerReadinessSignalFactory(ConnectionStringFactory, options);

        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        var services = new ServiceCollection();
        services.AddSingleton(logger);
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<SqlServerReadinessSignal>();
        signal.Name.Should().Be("sqlserver-readiness");
    }
}
