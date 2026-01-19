using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Veggerby.Ignition.Tests;

public class RetryPolicyTests
{
    // Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // arrange & act
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(200));

        // assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDefaultParameters_CreatesInstance()
    {
        // arrange & act
        var policy = new RetryPolicy();

        // assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithLogger_CreatesInstance()
    {
        // arrange
        var logger = Substitute.For<ILogger<RetryPolicy>>();

        // act
        var policy = new RetryPolicy(logger: logger);

        // assert
        policy.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithZeroMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(maxRetries: 0));
    }

    [Fact]
    public void Constructor_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        // arrange & act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(maxRetries: -1));
    }

    // ExecuteAsync (non-generic) - Basic Scenarios

    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt_NoRetries()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3);

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                await Task.CompletedTask;
            },
            "test-operation");

        // assert
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_FailsTwiceThenSucceeds_RetriesTwice()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new InvalidOperationException("Transient failure");
                }
                await Task.CompletedTask;
            },
            "test-operation");

        // assert
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_AlwaysFails_ThrowsAfterMaxRetries()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Permanent failure");
                },
                "test-operation");
        });

        callCount.Should().Be(3);
        ex.Message.Should().Be("Permanent failure");
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsImmediately()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));
        var cts = new CancellationTokenSource();

        // act & assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                },
                "test-operation",
                cts.Token);
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithResult_ReturnsValue()
    {
        // arrange
        var policy = new RetryPolicy(maxRetries: 3);

        // act
        var result = await policy.ExecuteAsync(
            async ct => await Task.FromResult(42),
            "test-operation");

        // assert
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_RespectsCondition()
    {
        // arrange
        var callCount = 0;
        var isReady = false;
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(10));

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                if (!isReady)
                {
                    throw new InvalidOperationException("Not ready");
                }
                await Task.CompletedTask;
            },
            attempt =>
            {
                if (attempt == 3)
                {
                    isReady = true;
                }
                return true;
            },
            "test-operation");

        // assert
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_ExponentialBackoff_DelaysIncrease()
    {
        // arrange
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var startTime = DateTime.UtcNow;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(50));

        // act
        try
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    if (callCount > 0)
                    {
                        delays.Add(DateTime.UtcNow - startTime);
                    }
                    callCount++;
                    startTime = DateTime.UtcNow;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                "test-operation");
        }
        catch
        {
            // Expected
        }

        // assert
        callCount.Should().Be(3);
        delays.Should().HaveCount(2);
        
        // First retry: ~50ms
        delays[0].TotalMilliseconds.Should().BeGreaterThanOrEqualTo(40);
        
        // Second retry: ~100ms (50 * 2)
        delays[1].TotalMilliseconds.Should().BeGreaterThanOrEqualTo(90);
    }

    // Argument Validation Tests

    [Fact]
    public async Task ExecuteAsync_NullOperation_ThrowsArgumentNullException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await policy.ExecuteAsync(null!, "test"));
    }

    [Fact]
    public async Task ExecuteAsync_NullOperationName_ThrowsArgumentNullException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await policy.ExecuteAsync(async ct => await Task.CompletedTask, null!));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyOperationName_ThrowsArgumentException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await policy.ExecuteAsync(async ct => await Task.CompletedTask, ""));
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceOperationName_ThrowsArgumentException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await policy.ExecuteAsync(async ct => await Task.CompletedTask, "   "));
    }

    // Timeout Tests

    [Fact]
    public async Task ExecuteAsync_WithTimeout_SucceedsBeforeTimeout()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                await Task.Delay(5, ct);
            },
            "test-operation",
            timeout: TimeSpan.FromSeconds(1));

        // assert
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ThrowsTimeoutException()
    {
        // arrange
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act & assert
        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    await Task.Delay(500, ct);
                },
                "test-operation",
                timeout: TimeSpan.FromMilliseconds(50));
        });

        ex.Message.Should().Contain("test-operation");
        ex.Message.Should().Contain("timed out");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_IncludesRetryDelaysInTimeout()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(100));

        // act & assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.Delay(10, ct);
                    throw new InvalidOperationException("Fail");
                },
                "test-operation",
                timeout: TimeSpan.FromMilliseconds(200));
        });

        // Should timeout before all retries complete
        callCount.Should().BeLessThan(5);
    }

    // Generic ExecuteAsync<T> Tests

    [Fact]
    public async Task ExecuteAsyncT_SucceedsOnFirstAttempt_ReturnsValue()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3);

        // act
        var result = await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                await Task.CompletedTask;
                return 42;
            },
            "test-operation");

        // assert
        result.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsyncT_FailsOnceThenSucceeds_RetriesAndReturnsValue()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act
        var result = await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new InvalidOperationException("Transient failure");
                }
                await Task.CompletedTask;
                return "success";
            },
            "test-operation");

        // assert
        result.Should().Be("success");
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsyncT_AlwaysFails_ThrowsAfterMaxRetries()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act & assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync<int>(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Permanent failure");
                },
                "test-operation");
        });

        callCount.Should().Be(3);
        ex.Message.Should().Be("Permanent failure");
    }

    [Fact]
    public async Task ExecuteAsyncT_WithTimeout_ReturnsValueBeforeTimeout()
    {
        // arrange
        var policy = new RetryPolicy(maxRetries: 3);

        // act
        var result = await policy.ExecuteAsync(
            async ct => await Task.FromResult(100),
            "test-operation",
            timeout: TimeSpan.FromSeconds(1));

        // assert
        result.Should().Be(100);
    }

    [Fact]
    public async Task ExecuteAsyncT_WithTimeout_ThrowsTimeoutException()
    {
        // arrange
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act & assert
        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync<string>(
                async ct =>
                {
                    await Task.Delay(500, ct);
                    return "never";
                },
                "test-operation",
                timeout: TimeSpan.FromMilliseconds(50));
        });

        ex.Message.Should().Contain("test-operation");
    }

    [Fact]
    public async Task ExecuteAsyncT_NullOperation_ThrowsArgumentNullException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await policy.ExecuteAsync<int>(null!, "test"));
    }

    // ShouldRetry Overload Tests

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_NullOperation_ThrowsArgumentNullException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await policy.ExecuteAsync(null!, _ => true, "test"));
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_NullShouldRetry_ThrowsArgumentNullException()
    {
        // arrange
        var policy = new RetryPolicy();

        // act & assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await policy.ExecuteAsync(async ct => await Task.CompletedTask, null!, "test"));
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_ReturnsFalseOnFirstAttempt_ExecutesOnce()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(10));

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                attempt => false,
                "test-operation");
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_ReturnsFalseAfterRetries_StopsRetrying()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(10));

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                attempt => attempt <= 2,
                "test-operation");
        });

        // When shouldRetry returns false on attempt 3, operation still executes attempt 3
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_SucceedsAfterPredicate_Succeeds()
    {
        // arrange
        var callCount = 0;
        var externalReady = false;
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(10));

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                if (!externalReady)
                {
                    throw new InvalidOperationException("Not ready");
                }
                await Task.CompletedTask;
            },
            attempt =>
            {
                if (attempt == 3)
                {
                    externalReady = true;
                }
                return true;
            },
            "test-operation");

        // assert
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WithShouldRetry_WithTimeout_ThrowsTimeoutException()
    {
        // arrange
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(100));

        // act & assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    await Task.Delay(100, ct);
                    throw new InvalidOperationException("Fail");
                },
                _ => true,
                "test-operation",
                timeout: TimeSpan.FromMilliseconds(150));
        });
    }

    // Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeFirstAttempt_ThrowsOperationCanceledException()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    ct.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                },
                "test-operation",
                cts.Token);
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledDuringRetry_StopsRetrying()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 5, TimeSpan.FromMilliseconds(50));
        using var cts = new CancellationTokenSource();

        // act & assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    if (callCount == 2)
                    {
                        cts.Cancel();
                    }
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                "test-operation",
                cts.Token);
        });

        // Should stop after cancellation during delay
        callCount.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsyncT_CancellationRequested_ThrowsOperationCanceledException()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // act & assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync<int>(
                async ct =>
                {
                    callCount++;
                    ct.ThrowIfCancellationRequested();
                    await Task.CompletedTask;
                    return 42;
                },
                "test-operation",
                cts.Token);
        });

        callCount.Should().Be(1);
    }

    // Edge Cases

    [Fact]
    public async Task ExecuteAsync_MaxRetriesOne_OnlyRetriesOnce()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 1, TimeSpan.FromMilliseconds(10));

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                "test-operation");
        });

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_CustomInitialDelay_UsesSpecifiedDelay()
    {
        // arrange
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var startTime = DateTime.UtcNow;
        var policy = new RetryPolicy(maxRetries: 2, TimeSpan.FromMilliseconds(200));

        // act
        try
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    if (callCount > 0)
                    {
                        delays.Add(DateTime.UtcNow - startTime);
                    }
                    callCount++;
                    startTime = DateTime.UtcNow;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                "test-operation");
        }
        catch
        {
            // Expected
        }

        // assert
        callCount.Should().Be(2);
        delays.Should().HaveCount(1);
        delays[0].TotalMilliseconds.Should().BeGreaterThanOrEqualTo(180);
    }

    [Fact]
    public async Task ExecuteAsync_ZeroDelay_RetriesImmediately()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.Zero);

        // act
        var startTime = DateTime.UtcNow;
        try
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                "test-operation");
        }
        catch
        {
            // Expected
        }
        var elapsed = DateTime.UtcNow - startTime;

        // assert
        callCount.Should().Be(3);
        elapsed.TotalMilliseconds.Should().BeLessThan(100); // Should be very fast with zero delay
    }

    [Fact]
    public async Task ExecuteAsync_DifferentExceptionTypes_RetriesAll()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 4, TimeSpan.FromMilliseconds(10));

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                await Task.CompletedTask;
                
                switch (callCount)
                {
                    case 1:
                        throw new InvalidOperationException("First");
                    case 2:
                        throw new ArgumentException("Second");
                    case 3:
                        throw new TimeoutException("Third");
                    default:
                        return; // Success
                }
            },
            "test-operation");

        // assert
        callCount.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_DoesNotRetry()
    {
        // arrange
        var callCount = 0;
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10));

        // act & assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    callCount++;
                    await Task.CompletedTask;
                    throw new OperationCanceledException("Cancelled");
                },
                "test-operation");
        });

        callCount.Should().Be(1);
    }

    // Logging Tests

    [Fact]
    public async Task ExecuteAsync_WithLogger_LogsSuccessAfterRetry()
    {
        // arrange
        var callCount = 0;
        var logger = Substitute.For<ILogger<RetryPolicy>>();
        var policy = new RetryPolicy(maxRetries: 3, TimeSpan.FromMilliseconds(10), logger);

        // act
        await policy.ExecuteAsync(
            async ct =>
            {
                callCount++;
                if (callCount < 2)
                {
                    throw new InvalidOperationException("Transient");
                }
                await Task.CompletedTask;
            },
            "test-operation");

        // assert - verify logger was called (simplified check)
        callCount.Should().Be(2);
        logger.ReceivedCalls().Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithLogger_LogsFailureAfterMaxRetries()
    {
        // arrange
        var logger = Substitute.For<ILogger<RetryPolicy>>();
        var policy = new RetryPolicy(maxRetries: 2, TimeSpan.FromMilliseconds(10), logger);

        // act
        try
        {
            await policy.ExecuteAsync(
                async ct =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("Fail");
                },
                "test-operation");
        }
        catch
        {
            // Expected
        }

        // assert - verify logger was called
        logger.ReceivedCalls().Should().NotBeEmpty();
    }
}
