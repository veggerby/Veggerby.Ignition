using Veggerby.Ignition.Stages;

namespace Veggerby.Ignition.Tests;

/// <summary>
/// Test helper factory that wraps an IIgnitionSignal for testing purposes.
/// </summary>
internal class TestSignalFactory : IIgnitionSignalFactory
{
    private readonly IIgnitionSignal _signal;

    public TestSignalFactory(IIgnitionSignal signal, int? stage = null)
    {
        _signal = signal ?? throw new ArgumentNullException(nameof(signal));
        // If stage is explicitly provided, use it
        // Otherwise, try to extract from IStagedIgnitionSignal
        // Otherwise, default to null (stage 0)
        Stage = stage ?? (signal as IStagedIgnitionSignal)?.Stage;
    }

    public string Name => _signal.Name;

    public TimeSpan? Timeout => _signal.Timeout;

    public int? Stage { get; }

    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        return _signal;
    }
}
