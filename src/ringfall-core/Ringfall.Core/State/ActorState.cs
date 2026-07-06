namespace Ringfall.Core.State;

public sealed record ActorState
{
    public required string ActorId { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required string Layer { get; init; }
    public required string HomeSectorId { get; init; }
    public required IReadOnlyList<string> SystemRefs { get; init; }
}
