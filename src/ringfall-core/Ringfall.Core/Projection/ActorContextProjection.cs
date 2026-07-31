using Ringfall.Core.State;

namespace Ringfall.Core.Projection;

internal sealed record ActorContextProjection
{
    public required string ActorId { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public required string Layer { get; init; }
    public required IReadOnlyList<ActorLocalObservation> Observations { get; init; }
    public required IReadOnlyList<string> CrewRefs { get; init; }
    public required IReadOnlyList<string> ToolRefs { get; init; }
}
