using Microsoft.Extensions.Logging;
using Npgsql;
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
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal((string)null!, options, logger));
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
    public void Constructor_NullDataSource_ThrowsArgumentNullException()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new PostgresReadinessSignal((NpgsqlDataSource)null!, options, logger));
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

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var options = new PostgresReadinessOptions
        {
            Timeout = TimeSpan.FromSeconds(2) // Short timeout to fail quickly
        };
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=invalid-host-that-does-not-exist;Database=test;Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // act & assert - Connection failures can throw various exception types
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=invalid-host;Database=test;Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // act - first call should fail and cache the result
        try
        {
            await signal.WaitAsync(cts.Token);
        }
        catch (Exception)
        {
            // Expected
        }

        // act - subsequent calls should return the same cached exception
        var exception1 = await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
        var exception2 = await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));

        // assert - both exceptions should be the same instance (cached)
        exception1.Should().BeSameAs(exception2);
    }

    [Fact]
    public async Task WaitAsync_WithCancellationToken_RespectsCancellation()
    {
        // arrange
        var options = new PostgresReadinessOptions();
        var logger = Substitute.For<ILogger<PostgresReadinessSignal>>();
        var signal = new PostgresReadinessSignal("Host=localhost;Database=test;Timeout=30;", options, logger);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }
}
