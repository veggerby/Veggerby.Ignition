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