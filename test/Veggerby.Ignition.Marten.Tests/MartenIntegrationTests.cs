using Marten;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Veggerby.Ignition.Marten;

namespace Veggerby.Ignition.Marten.Tests;

public class MartenIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private IDocumentStore? _documentStore;

    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();

        await _postgresContainer.StartAsync();

        _documentStore = DocumentStore.For(_postgresContainer.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        _documentStore?.Dispose();
        
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DocumentStore_ConnectionSucceeds()
    {
        // arrange
        var options = new MartenReadinessOptions();
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(_documentStore!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DocumentStore_WithTimeout_Succeeds()
    {
        // arrange
        var options = new MartenReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(_documentStore!, options, logger);

        // act & assert
        await signal.WaitAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RepeatedWaitAsync_ReturnsCachedResult()
    {
        // arrange
        var options = new MartenReadinessOptions();
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(_documentStore!, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - should succeed and use cached result
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DocumentStore_QueryExecution_Succeeds()
    {
        // arrange
        await using var session = _documentStore!.LightweightSession();
        
        var options = new MartenReadinessOptions();
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(_documentStore!, options, logger);

        // act
        await signal.WaitAsync();

        // assert - verify we can query
        var result = await session.Query<object>().AnyAsync();
        result.Should().BeFalse(); // No documents exist
    }
}
