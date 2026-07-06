namespace Ringfall.Core.State;

public sealed record WorldState
{
    public required string SchemaVersion { get; init; }
    public required string ScenarioId { get; init; }
    public required int SimulationSeed { get; init; }
    public required IReadOnlyList<SectorState> Sectors { get; init; }
    public required IReadOnlyList<ActorState> Actors { get; init; }
    public required IReadOnlyList<CrewState> Crews { get; init; }
}
