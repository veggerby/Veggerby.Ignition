namespace Veggerby.Ignition.Tests;

public class IgnitionSignalFactoryTests
{
    [Fact]
    public async Task FromTask_Succeeds()
    {
        // arrange
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var signal = IgnitionSignal.FromTask("task", tcs.Task);

        // act
        tcs.SetResult();
        await signal.WaitAsync();

        // assert
        // (No exception thrown)
        true.Should().BeTrue();
    }

    [Fact]
    public async Task FromTaskFactory_InvokesOnlyOnce()
    {
        // arrange
        int calls = 0;
        var signal = IgnitionSignal.FromTaskFactory("factory", ct =>
        {
            Interlocked.Increment(ref calls);
            return Task.Delay(10, ct);
        });

        // act
        await Task.WhenAll(signal.WaitAsync(), signal.WaitAsync());

        // assert
        calls.Should().Be(1);
    }

    [Fact]
    public async Task FromTaskFactory_PropagatesCancellation()
    {
        // arrange
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cts = new CancellationTokenSource();
        
        var signal = IgnitionSignal.FromTaskFactory("cancel", async ct =>
        {
            // Use TaskCompletionSource to ensure the task doesn't complete before cancellation
            using var registration = ct.Register(() => tcs.TrySetCanceled(ct));
            await tcs.Task;
        });

        // act
        cts.Cancel(); // Cancel immediately
        
        var exception = await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await signal.WaitAsync(cts.Token);
        });

        // assert
        cts.IsCancellationRequested.Should().BeTrue();
    }
}
