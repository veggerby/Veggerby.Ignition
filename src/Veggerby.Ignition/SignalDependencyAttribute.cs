using System;

namespace Veggerby.Ignition;

/// <summary>
/// Attribute for declaring dependencies between ignition signals in a declarative manner.
/// Specifies that the decorated signal requires other signals to complete successfully before it can start.
/// </summary>
/// <remarks>
/// This attribute provides a declarative way to define signal dependencies when using dependency-aware execution.
/// Dependencies can be specified by signal name or by signal type.
/// The graph builder will validate that all referenced dependencies exist and that no cycles are created.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class SignalDependencyAttribute : Attribute
{
    /// <summary>
    /// Creates a dependency on a signal identified by name.
    /// </summary>
    /// <param name="signalName">The name of the signal this signal depends on.</param>
    public SignalDependencyAttribute(string signalName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName, nameof(signalName));
        SignalName = signalName;
    }

    /// <summary>
    /// Creates a dependency on a signal identified by type.
    /// </summary>
    /// <param name="signalType">The type of the signal this signal depends on. Must implement <see cref="IIgnitionSignal"/>.</param>
    public SignalDependencyAttribute(Type signalType)
    {
        ArgumentNullException.ThrowIfNull(signalType);
        if (!typeof(IIgnitionSignal).IsAssignableFrom(signalType))
        {
            throw new ArgumentException($"Signal type must implement {nameof(IIgnitionSignal)}.", nameof(signalType));
        }

        SignalType = signalType;
    }

    /// <summary>
    /// Gets the name of the signal this signal depends on (when specified by name).
    /// </summary>
    public string? SignalName { get; }

    /// <summary>
    /// Gets the type of the signal this signal depends on (when specified by type).
    /// </summary>
    public Type? SignalType { get; }
}
