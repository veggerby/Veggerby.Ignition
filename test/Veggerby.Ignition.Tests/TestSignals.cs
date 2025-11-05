namespace Veggerby.Ignition.Tests;

internal sealed class FakeSignal : IIgnitionSignal
{
    private readonly Func<CancellationToken, Task> _action;
    public string Name { get; }
    public TimeSpan? Timeout { get; }

    public FakeSignal(string name, Func<CancellationToken, Task> action, TimeSpan? timeout = null)
    {
        Name = name;
        _action = action;
        Timeout = timeout;
    }

    public Task WaitAsync(CancellationToken cancellationToken = default) => _action(cancellationToken);
}

internal sealed class CountingSignal : IIgnitionSignal
{
    private int _count;
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public string Name { get; }
    public TimeSpan? Timeout { get; }
    public int InvocationCount => _count;

    public CountingSignal(string name, TimeSpan? timeout = null)
    {
        Name = name;
        Timeout = timeout;
    }

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

internal sealed class FaultingSignal : IIgnitionSignal
{
    public string Name { get; }
    public TimeSpan? Timeout { get; }
    private readonly Exception _ex;

    public FaultingSignal(string name, Exception ex, TimeSpan? timeout = null)
    {
        Name = name;
        _ex = ex;
        Timeout = timeout;
    }

    public Task WaitAsync(CancellationToken cancellationToken = default) => Task.FromException(_ex);
}