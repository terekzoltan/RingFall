namespace Ringfall.Core.State;

public sealed record SystemMetric
{
    public required string Name { get; init; }
    public required double Value { get; init; }
    public required MetricVisibility Visibility { get; init; }
}
