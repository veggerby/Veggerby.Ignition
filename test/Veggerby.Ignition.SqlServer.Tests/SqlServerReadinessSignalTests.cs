using Microsoft.Data.SqlClient;
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
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal((string)null!, options, logger));
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
    public void Constructor_NullConnectionFactory_ThrowsArgumentNullException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();

        // act & assert
        Assert.Throws<ArgumentNullException>(() => new SqlServerReadinessSignal((Func<SqlConnection>)null!, options, logger));
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

    [Fact]
    public async Task WaitAsync_ConnectionFailure_ThrowsException()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=invalid-server-that-does-not-exist;Database=test;Connection Timeout=1;", options, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        // act & assert
        await Assert.ThrowsAnyAsync<Exception>(() => signal.WaitAsync(cts.Token));
    }

    [Fact]
    public async Task WaitAsync_IdempotentExecution_UsesCachedResult()
    {
        // arrange
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=invalid-server;Database=test;Connection Timeout=1;", options, logger);

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
        var options = new SqlServerReadinessOptions();
        var logger = Substitute.For<ILogger<SqlServerReadinessSignal>>();
        var signal = new SqlServerReadinessSignal("Server=localhost;Database=test;Connection Timeout=30;", options, logger);
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert - TaskCanceledException is a subclass of OperationCanceledException
        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => signal.WaitAsync(cts.Token));
        exception.Should().NotBeNull();
    }
}
