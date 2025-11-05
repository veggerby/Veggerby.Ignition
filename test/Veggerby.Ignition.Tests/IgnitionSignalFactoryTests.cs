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
        var signal = IgnitionSignal.FromTaskFactory("factory", _ =>
        {
            Interlocked.Increment(ref calls);
            return Task.Delay(10);
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
        var cts = new CancellationTokenSource();
        var signal = IgnitionSignal.FromTaskFactory("cancel", ct => Task.Delay(200, ct));

        // act
        cts.CancelAfter(20);
        try
        {
            await signal.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // assert
            cts.IsCancellationRequested.Should().BeTrue();
            return;
        }
        false.Should().BeTrue("Expected cancellation but task completed successfully");
    }
}