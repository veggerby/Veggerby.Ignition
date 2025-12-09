using System.Collections.Generic;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Veggerby.Ignition;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents a directed acyclic graph (DAG) of ignition signals with dependencies.
/// Enables dependency-aware execution where signals can declare prerequisites.
/// </summary>
/// <remarks>
/// The graph must be acyclic; attempting to build a graph with cycles will result in an exception during construction.
/// Independent branches (signals with no dependency relationship) can execute in parallel automatically.
/// </remarks>
public interface IIgnitionGraph
{
    /// <summary>
    /// Gets all signals in the graph in topological order (dependencies before dependents).
    /// </summary>
    IReadOnlyList<IIgnitionSignal> Signals { get; }

    /// <summary>
    /// Gets the dependencies for a specific signal.
    /// </summary>
    /// <param name="signal">The signal to query dependencies for.</param>
    /// <returns>The set of signals that must complete before the specified signal can start.</returns>
    IReadOnlySet<IIgnitionSignal> GetDependencies(IIgnitionSignal signal);

    /// <summary>
    /// Gets the dependents for a specific signal (signals that depend on it).
    /// </summary>
    /// <param name="signal">The signal to query dependents for.</param>
    /// <returns>The set of signals that depend on the specified signal.</returns>
    IReadOnlySet<IIgnitionSignal> GetDependents(IIgnitionSignal signal);

    /// <summary>
    /// Gets all signals that have no dependencies (root signals).
    /// These can be executed immediately.
    /// </summary>
    IReadOnlyList<IIgnitionSignal> GetRootSignals();

    /// <summary>
    /// Gets all signals that depend on no other signals (leaf signals).
    /// </summary>
    IReadOnlyList<IIgnitionSignal> GetLeafSignals();
}
