using Ringfall.Core.Actions;

namespace Ringfall.Core.Formal;

internal sealed record AsterF1FactSet
{
    private IReadOnlyList<string> _toolSystemRefs = [];
    private IReadOnlyList<string> _toolSupportedActions = [];
    private IReadOnlyList<string> _crewSystemRefs = [];
    private IReadOnlyList<string> _coreIssueCodes = [];
    private IReadOnlyList<string> _coverageClassifications = [];

    public required string FamilyId { get; init; }
    public required string FamilyVersion { get; init; }
    public required string PacketId { get; init; }
    public required string PacketKind { get; init; }
    public required string SourceContextId { get; init; }
    public required string IssuerId { get; init; }
    public required string IssuerLayer { get; init; }
    public string? IssuerSectorId { get; init; }
    public string? ToolId { get; init; }
    public string? ToolStatus { get; init; }

    public required IReadOnlyList<string> ToolSystemRefs
    {
        get => _toolSystemRefs;
        init => _toolSystemRefs = Array.AsReadOnly(value.ToArray());
    }

    public required IReadOnlyList<string> ToolSupportedActions
    {
        get => _toolSupportedActions;
        init => _toolSupportedActions = Array.AsReadOnly(value.ToArray());
    }

    public string? CrewId { get; init; }
    public string? CrewStatus { get; init; }
    public string? CrewAssignedActorId { get; init; }
    public string? CrewHomeSectorId { get; init; }

    public required IReadOnlyList<string> CrewSystemRefs
    {
        get => _crewSystemRefs;
        init => _crewSystemRefs = Array.AsReadOnly(value.ToArray());
    }

    public string? CandidateAction { get; init; }
    public string? CandidateMode { get; init; }
    public string? CandidateTaskType { get; init; }
    public string? CandidateTargetLocation { get; init; }
    public required AsterL1DecisionStatus CoreStatus { get; init; }

    public required IReadOnlyList<string> CoreIssueCodes
    {
        get => _coreIssueCodes;
        init => _coreIssueCodes = Array.AsReadOnly(value.ToArray());
    }

    public required IReadOnlyList<string> CoverageClassifications
    {
        get => _coverageClassifications;
        init => _coverageClassifications = Array.AsReadOnly(value.ToArray());
    }
}
