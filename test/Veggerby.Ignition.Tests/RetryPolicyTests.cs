using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Veggerby.Ignition.Tests;

public class RetryPolicyTests
{
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
}
