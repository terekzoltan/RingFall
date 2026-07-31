namespace Ringfall.Core.State;

public sealed record ActorLocalObservation
{
    public required string ObservationId { get; init; }
    public required string Kind { get; init; }
    public required string Signal { get; init; }
    public required string SourceRef { get; init; }
}
