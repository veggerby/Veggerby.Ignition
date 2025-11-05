using System.Threading;
using System.Threading.Tasks;

namespace Veggerby.Ignition;

/// <summary>
/// Factory helpers for constructing <see cref="IIgnitionSignal"/> instances from existing tasks or task factories.
/// </summary>
public static class IgnitionSignal
{
    /// <summary>
    /// Wraps an already created readiness <see cref="Task"/> as an ignition signal.
    /// </summary>
    /// <param name="name">Human-friendly name for diagnostics.</param>
    /// <param name="task">Task that completes when the component is ready.</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global setting.</param>
    /// <returns>An ignition signal representing the provided task.</returns>
    public static IIgnitionSignal FromTask(string name, Task task, TimeSpan? timeout = null)
        => new TaskWaitHandle(name, task, timeout);

    /// <summary>
    /// Creates an ignition signal from a cancellable task factory that is invoked at most once.
    /// Subsequent waits reuse the same underlying task result.
    /// </summary>
    /// <param name="name">Human-friendly name for diagnostics.</param>
    /// <param name="factory">Factory producing the readiness task. Receives a cancellation token.</param>
    /// <param name="timeout">Optional per-signal timeout overriding the global setting.</param>
    /// <returns>An ignition signal backed by the lazily created task.</returns>
    public static IIgnitionSignal FromTaskFactory(string name, Func<CancellationToken, Task> factory, TimeSpan? timeout = null)
        => new TaskFactoryHandle(name, factory, timeout);

    private sealed class TaskWaitHandle(string name, Task task, TimeSpan? timeout) : IIgnitionSignal
    {
        public string Name { get; } = name;
        public TimeSpan? Timeout { get; } = timeout;
        private readonly Task _task = task ?? throw new ArgumentNullException(nameof(task));

        public Task WaitAsync(CancellationToken cancellationToken = default)
            => _task.WaitAsync(cancellationToken);
    }

    private sealed class TaskFactoryHandle(string name, Func<CancellationToken, Task> factory, TimeSpan? timeout) : IIgnitionSignal
    {
        public string Name { get; } = name;
        public TimeSpan? Timeout { get; } = timeout;
        private readonly Func<CancellationToken, Task> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        private readonly object _sync = new();
        private Task? _createdTask;

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            // Ensure single invocation of factory (unless previous task faulted and caller wants to observe that again).
            if (_createdTask is null)
            {
                lock (_sync)
                {
                    _createdTask ??= _factory(cancellationToken);
                }
            }

            // If caller supplies a cancellation token, we still need to honor it for awaiting.
            return cancellationToken.CanBeCanceled ? _createdTask.WaitAsync(cancellationToken) : _createdTask;
        }
    }
}