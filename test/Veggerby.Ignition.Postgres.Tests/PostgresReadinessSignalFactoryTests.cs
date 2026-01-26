using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Postgres.Tests;

public class PostgresReadinessSignalFactoryTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Host=localhost;Database=test;";
        var options = new PostgresReadinessOptions();

        // act
        var factory = new PostgresReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("postgres-readiness");
        factory.Timeout.Should().BeNull();
        factory.Stage.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionStringFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignalFactory(null!, options));
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Host=localhost;Database=test;";

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignalFactory(ConnectionStringFactory, null!));
    }

    [Fact]
    public void Timeout_ReturnsOptionsTimeout()
    {
        // arrange
        var timeout = TimeSpan.FromSeconds(30);
        string ConnectionStringFactory(IServiceProvider sp) => "Host=localhost;Database=test;";
        var options = new PostgresReadinessOptions { Timeout = timeout };

        // act
        var factory = new PostgresReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Timeout.Should().Be(timeout);
    }

    [Fact]
    public void Stage_ReturnsOptionsStage()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Host=localhost;Database=test;";
        var options = new PostgresReadinessOptions { Stage = 2 };

        // act
        var factory = new PostgresReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Stage.Should().Be(2);
    }

    [Fact]
    public void Name_ReturnsPostgresReadiness()
    {
        // arrange
        string ConnectionStringFactory(IServiceProvider sp) => "Host=localhost;Database=test;";
        var options = new PostgresReadinessOptions();

        // act
        var factory = new PostgresReadinessSignalFactory(ConnectionStringFactory, options);

        // assert
        factory.Name.Should().Be("postgres-readiness");
    }

    [Fact]
    public void CreateSignal_WithValidServiceProvider_ReturnsPostgresReadinessSignal()
    {
        // arrange
        const string connectionString = "Host=localhost;Database=test;";
        string ConnectionStringFactory(IServiceProvider sp) => connectionString;
        var options = new PostgresReadinessOptions();
        var factory = new PostgresReadinessSignalFactory(ConnectionStringFactory, options);

        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        var services = new ServiceCollection();
        services.AddSingleton(logger);
        var serviceProvider = services.BuildServiceProvider();

        // act
        var signal = factory.CreateSignal(serviceProvider);

        // assert
        signal.Should().NotBeNull();
        signal.Should().BeOfType<PostgresReadinessSignal>();
        signal.Name.Should().Be("postgres-readiness");
    }
}
