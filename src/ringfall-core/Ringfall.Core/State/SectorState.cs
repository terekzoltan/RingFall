namespace Ringfall.Core.State;

public sealed record SectorState
{
    public required string SectorId { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<SystemState> Systems { get; init; }
}
