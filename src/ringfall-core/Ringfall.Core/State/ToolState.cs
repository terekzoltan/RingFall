namespace Ringfall.Core.State;

public sealed record ToolState
{
    public required string ToolId { get; init; }
    public required string DisplayName { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<string> SystemRefs { get; init; }
    public required IReadOnlyList<string> SupportedActions { get; init; }
}
