using System.Collections.ObjectModel;
using System.Text.Json;

namespace Ringfall.Core.Actions;

internal sealed record AsterL1ToolActionCandidate
{
    private IReadOnlyList<string>? _evidenceRefs;
    private IReadOnlyDictionary<string, JsonElement>? _arguments;

    public required string PacketId { get; init; }
    public required string PacketType { get; init; }
    public required string SchemaVersion { get; init; }
    public required string IssuerId { get; init; }
    public required string IssuerLayer { get; init; }
    public required int IssuedAtTick { get; init; }
    public string? IssuedAtTurn { get; init; }
    public required string SourceContextId { get; init; }
    public string? Rationale { get; init; }
    public double? Confidence { get; init; }

    public IReadOnlyList<string>? EvidenceRefs
    {
        get => _evidenceRefs;
        init => _evidenceRefs = value is null ? null : Array.AsReadOnly(value.ToArray());
    }

    public string? VisibilityIntent { get; init; }
    public string? Urgency { get; init; }
    public required string ToolId { get; init; }
    public required string Action { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? Arguments
    {
        get => _arguments;
        init => _arguments = value is null
            ? null
            : new ReadOnlyDictionary<string, JsonElement>(
                value.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ValueKind == JsonValueKind.Undefined
                        ? default
                        : pair.Value.Clone(),
                    StringComparer.Ordinal));
    }

    public required string Mode { get; init; }
    public bool? RequiresDryRun { get; init; }
}

internal sealed record AsterL1WorkOrderCandidate
{
    private IReadOnlyList<string>? _evidenceRefs;
    private IReadOnlyList<string>? _authorizedResources;
    private IReadOnlyList<string>? _constraints;

    public required string PacketId { get; init; }
    public required string PacketType { get; init; }
    public required string SchemaVersion { get; init; }
    public required string IssuerId { get; init; }
    public required string IssuerLayer { get; init; }
    public required int IssuedAtTick { get; init; }
    public string? IssuedAtTurn { get; init; }
    public required string SourceContextId { get; init; }
    public string? Rationale { get; init; }
    public double? Confidence { get; init; }

    public IReadOnlyList<string>? EvidenceRefs
    {
        get => _evidenceRefs;
        init => _evidenceRefs = value is null ? null : Array.AsReadOnly(value.ToArray());
    }

    public string? VisibilityIntent { get; init; }
    public string? Urgency { get; init; }
    public string? TargetCrewId { get; init; }
    public string? TargetCrewPoolId { get; init; }
    public required string TargetLocation { get; init; }
    public required string TaskType { get; init; }
    public required string Priority { get; init; }
    public string? RiskTolerance { get; init; }

    public IReadOnlyList<string>? AuthorizedResources
    {
        get => _authorizedResources;
        init => _authorizedResources = value is null ? null : Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<string>? Constraints
    {
        get => _constraints;
        init => _constraints = value is null ? null : Array.AsReadOnly(value.ToArray());
    }

    public string? ExpectedReport { get; init; }
    public string? FallbackProtocol { get; init; }
    public string? StealthLevel { get; init; }
    public string? PublicVisibility { get; init; }
}
