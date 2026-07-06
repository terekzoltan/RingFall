namespace Ringfall.Core.State;

public sealed record CrewState
{
    public required string CrewId { get; init; }
    public required string DisplayName { get; init; }
    public required string HomeSectorId { get; init; }
    public required string Status { get; init; }
    public required string AssignedActorId { get; init; }
    public required IReadOnlyList<string> SystemRefs { get; init; }
}
