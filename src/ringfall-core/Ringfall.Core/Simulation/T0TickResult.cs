using Ringfall.Core.Artifacts;
using Ringfall.Core.State;

namespace Ringfall.Core.Simulation;

public sealed record T0TickResult
{
    public required WorldState InitialState { get; init; }
    public required WorldState FinalState { get; init; }
    public required int Tick { get; init; }
    public required int DeterministicSeed { get; init; }
    public required IReadOnlyList<SimulationEventArtifact> Events { get; init; }
    public required StateDiffArtifact StateDiff { get; init; }
}
