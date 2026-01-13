using System;
using Veggerby.Ignition.Stages;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Factory for creating staged ignition signals that wrap an inner signal with stage metadata.
/// </summary>
public sealed class StagedIgnitionSignalFactory : IIgnitionSignalFactory
{
    private readonly IIgnitionSignalFactory _innerFactory;
    private readonly int _stage;

    /// <summary>
    /// Creates a new staged signal factory.
    /// </summary>
    /// <param name="innerFactory">The factory that creates the actual signal.</param>
    /// <param name="stage">The stage/phase number for this signal.</param>
    public StagedIgnitionSignalFactory(IIgnitionSignalFactory innerFactory, int stage)
    {
        ArgumentNullException.ThrowIfNull(innerFactory);
        
        if (stage < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stage), "Stage number cannot be negative.");
        }

        _innerFactory = innerFactory;
        _stage = stage;
    }

    /// <inheritdoc/>
    public string Name => _innerFactory.Name;

    /// <inheritdoc/>
    public TimeSpan? Timeout => _innerFactory.Timeout;

    /// <inheritdoc/>
    public IIgnitionSignal CreateSignal(IServiceProvider serviceProvider)
    {
        var signal = _innerFactory.CreateSignal(serviceProvider);
        
        // If already staged, return as-is
        if (signal is IStagedIgnitionSignal)
        {
            return signal;
        }

        return new StagedSignalWrapper(signal, _stage);
    }

    /// <summary>
    /// Wrapper that adds stage/phase support to an existing signal.
    /// </summary>
    private sealed class StagedSignalWrapper : IStagedIgnitionSignal
    {
        private readonly IIgnitionSignal _inner;

        public StagedSignalWrapper(IIgnitionSignal inner, int stage)
        {
            _inner = inner;
            Stage = stage;
        }

        public string Name => _inner.Name;
        public TimeSpan? Timeout => _inner.Timeout;
        public int Stage { get; }

        public System.Threading.Tasks.Task WaitAsync(System.Threading.CancellationToken cancellationToken = default)
            => _inner.WaitAsync(cancellationToken);
    }
}
