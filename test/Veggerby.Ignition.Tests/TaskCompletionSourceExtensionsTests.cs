namespace Veggerby.Ignition.Tests;

public class TaskCompletionSourceExtensionsTests
{
    [Fact]
    public async Task Ignited_Void_SetsResult()
    {
        // arrange
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // act
        var transitioned = tcs.Ignited();

        // assert
        transitioned.Should().BeTrue();
        await tcs.Task; // should complete without exception
    }

    [Fact]
    public async Task IgnitionFailed_Void_SetsException()
    {
        // arrange
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ex = new InvalidOperationException("boom");

        // act
        var transitioned = tcs.IgnitionFailed(ex);

        // assert
        transitioned.Should().BeTrue();
        var agg = await Assert.ThrowsAsync<InvalidOperationException>(async () => await tcs.Task);
        agg.Message.Should().Be("boom");
    }

    [Fact]
    public async Task Ignited_Generic_SetsResult()
    {
        // arrange
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // act
        var transitioned = tcs.Ignited(42);

        // assert
        transitioned.Should().BeTrue();
        (await tcs.Task).Should().Be(42);
    }

    [Fact]
    public async Task IgnitionFailed_Generic_SetsException()
    {
        // arrange
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ex = new ApplicationException("failure");

        // act
        tcs.IgnitionFailed(ex).Should().BeTrue();

        // assert
        var thrown = await Assert.ThrowsAsync<ApplicationException>(async () => await tcs.Task);
        thrown.Message.Should().Be("failure");
    }

    [Fact]
    public async Task IgnitionCanceled_Generic_SetsCanceled()
    {
        // arrange
        var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // act
        var transitioned = tcs.IgnitionCanceled();

        // assert
        transitioned.Should().BeTrue();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await tcs.Task);
    }
}