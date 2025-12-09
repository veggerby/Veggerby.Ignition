namespace Veggerby.Ignition.Stages;

/// <summary>
/// Extended interface for ignition signals that belong to a specific startup stage/phase.
/// Signals implementing this interface can be grouped into sequential stages where
/// all signals in a stage execute in parallel, but stages execute sequentially.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IIgnitionSignal"/> to support staged (multi-phase) execution.
/// When using <see cref="IgnitionExecutionMode.Staged"/>:
/// <list type="bullet">
///   <item>Signals are grouped by their <see cref="Stage"/> number</item>
///   <item>Stage 0 executes first, then Stage 1, then Stage 2, etc.</item>
///   <item>Within each stage, signals execute in parallel</item>
///   <item>A stage must complete (or meet policy thresholds) before the next stage starts</item>
/// </list>
/// </para>
/// <para>
/// Signals not implementing this interface are treated as Stage 0 (default stage) when using staged execution.
/// </para>
/// </remarks>
public interface IStagedIgnitionSignal : IIgnitionSignal
{
    /// <summary>
    /// Gets the stage/phase number for this signal.
    /// Stage 0 is the first stage (infrastructure), Stage 1 is the second (services), etc.
    /// Lower numbered stages execute before higher numbered stages.
    /// </summary>
    int Stage { get; }
}
