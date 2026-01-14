namespace Veggerby.Ignition.Tests;

/// <summary>
/// Test helper factory that wraps an IIgnitionSignal for testing purposes.
/// </summary>
internal class TestSignalFactory : IIgnitionSignalFactory
{
    private readonly IIgnitionSignal _signal;

    public TestSignalFactory(IIgnitionSignal signal, int stage = 0)
    {
        _signal = signal ?? throw new ArgumentNullException(nameof(signal));
        Stage = stage;
    }

    public string Name => _signal.Name;

    public TimeSpan? Timeout => _signal.Timeout;

    public int? Stage { get; }

    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        return _signal;
    }
}
