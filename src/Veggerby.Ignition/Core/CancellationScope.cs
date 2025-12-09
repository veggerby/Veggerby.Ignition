using System.Threading;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Default implementation of <see cref="ICancellationScope"/> providing hierarchical cancellation management.
/// </summary>
/// <remarks>
/// <para>
/// This implementation creates a <see cref="CancellationTokenSource"/> that is linked to the parent scope's token
/// (if a parent exists). When the parent is cancelled, this scope automatically receives the cancellation.
/// When this scope is cancelled directly, the cancellation reason and triggering signal are recorded.
/// </para>
/// <para>
/// Thread safety: All state mutations are protected by a lock to ensure thread-safe access from multiple signals.
/// </para>
/// </remarks>
public sealed class CancellationScope : ICancellationScope
{
    private readonly CancellationTokenSource _cts;
    private readonly CancellationTokenRegistration? _parentRegistration;
    private readonly object _lock = new();
    private CancellationReason _cancellationReason = CancellationReason.None;
    private string? _triggeringSignalName;
    private bool _disposed;

    /// <summary>
    /// Creates a new root cancellation scope with no parent.
    /// </summary>
    /// <param name="name">The name identifying this scope.</param>
    public CancellationScope(string name)
        : this(name, parent: null, linkedToken: CancellationToken.None)
    {
    }

    /// <summary>
    /// Creates a new cancellation scope with an optional parent.
    /// </summary>
    /// <param name="name">The name identifying this scope.</param>
    /// <param name="parent">The parent scope whose cancellation will propagate to this scope.</param>
    public CancellationScope(string name, ICancellationScope? parent)
        : this(name, parent, parent?.Token ?? CancellationToken.None)
    {
    }

    /// <summary>
    /// Creates a new cancellation scope linked to a specific cancellation token.
    /// </summary>
    /// <param name="name">The name identifying this scope.</param>
    /// <param name="parent">The parent scope, or <c>null</c> for a root scope.</param>
    /// <param name="linkedToken">A cancellation token to link to this scope.</param>
    public CancellationScope(string name, ICancellationScope? parent, CancellationToken linkedToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Parent = parent;
        _cts = linkedToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(linkedToken)
            : new CancellationTokenSource();

        // If linked to a parent, register to capture reason when parent is cancelled
        if (parent is not null && parent.Token.CanBeCanceled)
        {
            _parentRegistration = parent.Token.Register(() =>
            {
                lock (_lock)
                {
                    if (_cancellationReason == CancellationReason.None)
                    {
                        _cancellationReason = CancellationReason.ScopeCancelled;
                        _triggeringSignalName = parent.TriggeringSignalName;
                    }
                }
            });
        }
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public ICancellationScope? Parent { get; }

    /// <inheritdoc/>
    public CancellationToken Token => _cts.Token;

    /// <inheritdoc/>
    public bool IsCancelled => _cts.IsCancellationRequested;

    /// <inheritdoc/>
    public CancellationReason CancellationReason
    {
        get
        {
            lock (_lock)
            {
                return _cancellationReason;
            }
        }
    }

    /// <inheritdoc/>
    public string? TriggeringSignalName
    {
        get
        {
            lock (_lock)
            {
                return _triggeringSignalName;
            }
        }
    }

    /// <inheritdoc/>
    public void Cancel(CancellationReason reason, string? triggeringSignalName = null)
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_cancellationReason == CancellationReason.None)
            {
                _cancellationReason = reason;
                _triggeringSignalName = triggeringSignalName;
            }
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS was already disposed
        }
    }

    /// <inheritdoc/>
    public ICancellationScope CreateChildScope(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CancellationScope), "Cannot create child scope from a disposed scope.");
        }

        return new CancellationScope(name, this);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _parentRegistration?.Dispose();
        _cts.Dispose();
    }
}
