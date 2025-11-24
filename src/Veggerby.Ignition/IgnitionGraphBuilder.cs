using System;
using System.Collections.Generic;
using System.Linq;

namespace Veggerby.Ignition;

/// <summary>
/// Builder for constructing dependency-aware ignition signal graphs.
/// Provides fluent API for defining signal dependencies and validates graph structure.
/// </summary>
public sealed class IgnitionGraphBuilder
{
    private readonly List<IIgnitionSignal> _signals = new();
    private readonly Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>> _dependencies = new();

    /// <summary>
    /// Adds a signal to the graph with no explicit dependencies.
    /// Dependencies can be added later using <see cref="DependsOn(IIgnitionSignal, IIgnitionSignal[])"/>.
    /// </summary>
    /// <param name="signal">The signal to add.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public IgnitionGraphBuilder AddSignal(IIgnitionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (!_signals.Contains(signal))
        {
            _signals.Add(signal);
            _dependencies[signal] = new HashSet<IIgnitionSignal>();
        }

        return this;
    }

    /// <summary>
    /// Adds multiple signals to the graph.
    /// </summary>
    /// <param name="signals">The signals to add.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public IgnitionGraphBuilder AddSignals(IEnumerable<IIgnitionSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);
        foreach (var signal in signals)
        {
            AddSignal(signal);
        }

        return this;
    }

    /// <summary>
    /// Declares that a signal depends on one or more prerequisite signals.
    /// </summary>
    /// <param name="dependent">The signal that has dependencies.</param>
    /// <param name="dependencies">The signals that must complete before the dependent can start.</param>
    /// <returns>This builder instance for fluent chaining.</returns>
    public IgnitionGraphBuilder DependsOn(IIgnitionSignal dependent, params IIgnitionSignal[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependent);
        ArgumentNullException.ThrowIfNull(dependencies);

        AddSignal(dependent);

        foreach (var dependency in dependencies)
        {
            ArgumentNullException.ThrowIfNull(dependency, nameof(dependencies));
            AddSignal(dependency);
            _dependencies[dependent].Add(dependency);
        }

        return this;
    }

    /// <summary>
    /// Automatically discovers and applies dependencies based on <see cref="SignalDependencyAttribute"/> decorations.
    /// </summary>
    /// <returns>This builder instance for fluent chaining.</returns>
    public IgnitionGraphBuilder ApplyAttributeDependencies()
    {
        foreach (var signal in _signals.ToList())
        {
            var signalType = signal.GetType();
            var attributes = signalType.GetCustomAttributes(typeof(SignalDependencyAttribute), inherit: false)
                .Cast<SignalDependencyAttribute>();

            foreach (var attr in attributes)
            {
                IIgnitionSignal? dependency = null;

                if (attr.SignalName is not null)
                {
                    dependency = _signals.FirstOrDefault(s => s.Name == attr.SignalName);
                    if (dependency is null)
                    {
                        throw new InvalidOperationException(
                            $"Signal '{signal.Name}' declares dependency on signal named '{attr.SignalName}', but no such signal exists in the graph.");
                    }
                }
                else if (attr.SignalType is not null)
                {
                    dependency = _signals.FirstOrDefault(s => attr.SignalType.IsAssignableFrom(s.GetType()));
                    if (dependency is null)
                    {
                        throw new InvalidOperationException(
                            $"Signal '{signal.Name}' declares dependency on signal type '{attr.SignalType.Name}', but no such signal exists in the graph.");
                    }
                }

                if (dependency is not null)
                {
                    _dependencies[signal].Add(dependency);
                }
            }
        }

        return this;
    }

    /// <summary>
    /// Builds the ignition graph, performing topological sort and cycle detection.
    /// </summary>
    /// <returns>The constructed graph.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph contains cycles.</exception>
    public IIgnitionGraph Build()
    {
        if (_signals.Count == 0)
        {
            return new IgnitionGraph(Array.Empty<IIgnitionSignal>(), new Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>>());
        }

        // Perform topological sort using Kahn's algorithm
        var sortedSignals = TopologicalSort();
        return new IgnitionGraph(sortedSignals, _dependencies);
    }

    private List<IIgnitionSignal> TopologicalSort()
    {
        // Calculate in-degree for each signal
        // In-degree = number of dependencies (signals that must complete before this one)
        var inDegree = new Dictionary<IIgnitionSignal, int>();
        foreach (var signal in _signals)
        {
            inDegree[signal] = _dependencies[signal].Count;
        }

        // Build reverse graph (dependents)
        var dependents = new Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>>();
        foreach (var signal in _signals)
        {
            dependents[signal] = new HashSet<IIgnitionSignal>();
        }

        foreach (var kvp in _dependencies)
        {
            foreach (var dependency in kvp.Value)
            {
                dependents[dependency].Add(kvp.Key);
            }
        }

        // Kahn's algorithm
        var queue = new Queue<IIgnitionSignal>();
        foreach (var signal in _signals)
        {
            if (inDegree[signal] == 0)
            {
                queue.Enqueue(signal);
            }
        }

        var sorted = new List<IIgnitionSignal>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            foreach (var dependent in dependents[current])
            {
                inDegree[dependent]--;
                if (inDegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        // Check for cycles
        if (sorted.Count != _signals.Count)
        {
            var remaining = _signals.Except(sorted).ToList();
            var cycle = DetectCycle(remaining);
            throw new InvalidOperationException(
                $"Ignition graph contains a cycle: {string.Join(" -> ", cycle.Select(s => s.Name))} -> {cycle[0].Name}. " +
                "Dependency-aware execution requires an acyclic graph.");
        }

        return sorted;
    }

    private List<IIgnitionSignal> DetectCycle(List<IIgnitionSignal> candidates)
    {
        // Use DFS to find a cycle for better error reporting
        var visited = new HashSet<IIgnitionSignal>();
        var recursionStack = new HashSet<IIgnitionSignal>();
        var path = new List<IIgnitionSignal>();

        foreach (var signal in candidates)
        {
            if (DfsDetectCycle(signal, visited, recursionStack, path))
            {
                // Extract the cycle from the path
                var cycleStart = path.Last();
                var cycleIndex = path.IndexOf(cycleStart);
                return path.Skip(cycleIndex).ToList();
            }
        }

        // Fallback if DFS doesn't find cycle in remaining nodes
        return candidates;
    }

    private bool DfsDetectCycle(IIgnitionSignal signal, HashSet<IIgnitionSignal> visited, HashSet<IIgnitionSignal> recursionStack, List<IIgnitionSignal> path)
    {
        if (recursionStack.Contains(signal))
        {
            path.Add(signal);
            return true;
        }

        if (visited.Contains(signal))
        {
            return false;
        }

        visited.Add(signal);
        recursionStack.Add(signal);
        path.Add(signal);

        if (_dependencies.TryGetValue(signal, out var deps))
        {
            foreach (var dep in deps)
            {
                if (DfsDetectCycle(dep, visited, recursionStack, path))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(signal);
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private sealed class IgnitionGraph : IIgnitionGraph
    {
        private static readonly IReadOnlySet<IIgnitionSignal> EmptySet = new HashSet<IIgnitionSignal>();
        
        private readonly IReadOnlyList<IIgnitionSignal> _sortedSignals;
        private readonly Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>> _dependencies;
        private readonly Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>> _dependents;

        public IgnitionGraph(IReadOnlyList<IIgnitionSignal> sortedSignals, Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>> dependencies)
        {
            _sortedSignals = sortedSignals;
            _dependencies = new Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>>(dependencies);
            
            // Build reverse mapping
            _dependents = new Dictionary<IIgnitionSignal, HashSet<IIgnitionSignal>>();
            foreach (var signal in _sortedSignals)
            {
                _dependents[signal] = new HashSet<IIgnitionSignal>();
            }

            foreach (var kvp in _dependencies)
            {
                foreach (var dep in kvp.Value)
                {
                    _dependents[dep].Add(kvp.Key);
                }
            }
        }

        public IReadOnlyList<IIgnitionSignal> Signals => _sortedSignals;

        public IReadOnlySet<IIgnitionSignal> GetDependencies(IIgnitionSignal signal)
        {
            ArgumentNullException.ThrowIfNull(signal);
            return _dependencies.TryGetValue(signal, out var deps) ? deps : EmptySet;
        }

        public IReadOnlySet<IIgnitionSignal> GetDependents(IIgnitionSignal signal)
        {
            ArgumentNullException.ThrowIfNull(signal);
            return _dependents.TryGetValue(signal, out var deps) ? deps : EmptySet;
        }

        public IReadOnlyList<IIgnitionSignal> GetRootSignals()
        {
            var roots = new List<IIgnitionSignal>();
            foreach (var signal in _sortedSignals)
            {
                if (_dependencies[signal].Count == 0)
                {
                    roots.Add(signal);
                }
            }

            return roots;
        }

        public IReadOnlyList<IIgnitionSignal> GetLeafSignals()
        {
            var leaves = new List<IIgnitionSignal>();
            foreach (var signal in _sortedSignals)
            {
                if (_dependents[signal].Count == 0)
                {
                    leaves.Add(signal);
                }
            }

            return leaves;
        }
    }
}
