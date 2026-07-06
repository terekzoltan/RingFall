namespace Ringfall.Core.State;

public sealed record SystemState
{
    public required string SystemId { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required IReadOnlyList<SystemMetric> Metrics { get; init; }
}
