namespace Veggerby.Ignition.Tests;

internal sealed class FakeSignal(string name, Func<CancellationToken, Task> action, TimeSpan? timeout = null) : IIgnitionSignal
{
    private readonly Func<CancellationToken, Task> _action = action;
    public string Name { get; } = name;
    public TimeSpan? Timeout { get; } = timeout;

    public Task WaitAsync(CancellationToken cancellationToken = default) => _action(cancellationToken);
}

internal sealed class CountingSignal(string name, TimeSpan? timeout = null) : IIgnitionSignal
{
    private int _count;
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string Name { get; } = name;
    public TimeSpan? Timeout { get; } = timeout;
    public int InvocationCount => _count;

    public void Complete() => _tcs.TrySetResult();

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _count);
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => _tcs.TrySetCanceled(cancellationToken));
        }
        return _tcs.Task;
    }
}

internal sealed class FaultingSignal(string name, Exception ex, TimeSpan? timeout = null) : IIgnitionSignal
{
    public string Name { get; } = name;
    public TimeSpan? Timeout { get; } = timeout;
    private readonly Exception _ex = ex;

    public Task WaitAsync(CancellationToken cancellationToken = default) => Task.FromException(_ex);
}

internal sealed class TrackingService(string name, TimeSpan? timeout = null) : IIgnitionSignal
{
    private int _invocations;
    public string Name { get; } = name;
    public TimeSpan? Timeout { get; } = timeout;
    public int InvocationCount => _invocations;
    public bool CancellationObserved { get; private set; }

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _invocations);
        // We intentionally never complete the underlying delay; cancellation drives classification.
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                CancellationObserved = true;
            });
        }
        // Use an infinite cancellable delay to rely solely on coordinator-driven cancellation / timeout.
        return Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, cancellationToken);
    }

    // Retained for API surface parity with CountingSignal (unused in cancellation tests).
    public void Complete() { /* no-op for tracking */ }
}
