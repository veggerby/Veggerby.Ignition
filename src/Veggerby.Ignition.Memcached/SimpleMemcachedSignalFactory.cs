using System;

namespace Veggerby.Ignition.Memcached;

/// <summary>
/// Simple factory wrapper for a pre-created Memcached signal instance.
/// Returns the same signal instance on every CreateSignal call.
/// </summary>
internal sealed class SimpleMemcachedSignalFactory(MemcachedReadinessSignal signal) : IIgnitionSignalFactory
{
    private readonly MemcachedReadinessSignal _signal = signal ?? throw new ArgumentNullException(nameof(signal));

    public string Name => _signal.Name;

    public TimeSpan? Timeout => _signal.Timeout;

    public int? Stage => null;

    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        return _signal;
    }
}
