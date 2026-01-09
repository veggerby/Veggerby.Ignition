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

    [Fact]
    public async Task WaitAsync_VerifyDisabled_CompletesSuccessfully()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var options = new MartenReadinessOptions { VerifyDocumentStore = false };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);

        // act
        await signal.WaitAsync();

        // assert - should complete without calling the document store
        documentStore.DidNotReceive().LightweightSession();
    }

    [Fact]
    public async Task WaitAsync_DocumentStoreFailure_ThrowsException()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        documentStore.LightweightSession().Returns(x => throw new InvalidOperationException("Store connection failed"));
        
        var options = new MartenReadinessOptions { VerifyDocumentStore = true };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => signal.WaitAsync());
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        documentStore.LightweightSession().Returns(session);
        
        session.QueryAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<int>>(new List<int> { 1 }));

        var options = new MartenReadinessOptions { VerifyDocumentStore = true };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);

        // act
        await signal.WaitAsync();
        await signal.WaitAsync();
        await signal.WaitAsync();

        // assert - session created only once
        documentStore.Received(1).LightweightSession();
    }

    [Fact]
    public async Task WaitAsync_WithCancellationToken_RespectsCancellation()
    {
        // arrange
        var documentStore = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        
        documentStore.LightweightSession().Returns(session);
        
        // Make the QueryAsync throw OperationCanceledException when cancellation token is used
        session.QueryAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var token = callInfo.ArgAt<CancellationToken>(1);
                token.ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<int>>(new List<int> { 1 });
            });

        var options = new MartenReadinessOptions { VerifyDocumentStore = true };
        var logger = Substitute.For<ILogger<MartenReadinessSignal>>();
        var signal = new MartenReadinessSignal(documentStore, options, logger);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }
}
