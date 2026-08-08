using Ringfall.Core.Actions;
using Ringfall.Core.State;

namespace Ringfall.Core.Formal;

internal static class AsterF1FactExporter
{
    private static readonly IReadOnlyList<string> BaseCoverage = Array.AsReadOnly(new[]
    {
        "schema_only",
        "guarded_by_core_validator",
        "unsupported"
    });

    public static AsterF1FactSet Export(WorldState state, AsterL1ToolActionCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidate);

        var decision = AsterL1ActionValidator.Validate(state, candidate);
        var actor = ResolveActor(state, candidate.IssuerId);
        var tool = ResolveTool(state, candidate.ToolId);

        return new AsterF1FactSet
        {
            FamilyId = AsterF1Policy.FamilyId,
            FamilyVersion = AsterF1Policy.FamilyVersion,
            PacketId = candidate.PacketId,
            PacketKind = candidate.PacketType,
            SourceContextId = candidate.SourceContextId,
            IssuerId = candidate.IssuerId,
            IssuerLayer = candidate.IssuerLayer,
            IssuerSectorId = actor?.HomeSectorId,
            ToolId = candidate.ToolId,
            ToolStatus = tool?.Status,
            ToolSystemRefs = tool?.SystemRefs ?? [],
            ToolSupportedActions = tool?.SupportedActions ?? [],
            CrewId = null,
            CrewStatus = null,
            CrewAssignedActorId = null,
            CrewHomeSectorId = null,
            CrewSystemRefs = [],
            CandidateAction = candidate.Action,
            CandidateMode = candidate.Mode,
            CandidateTaskType = null,
            CandidateTargetLocation = null,
            CoreStatus = decision.Status,
            CoreIssueCodes = decision.Issues.Select(issue => issue.Code).ToArray(),
            CoverageClassifications = BaseCoverage
        };
    }

    public static AsterF1FactSet Export(WorldState state, AsterL1WorkOrderCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(candidate);

        var decision = AsterL1ActionValidator.Validate(state, candidate);
        var actor = ResolveActor(state, candidate.IssuerId);
        var crew = candidate.TargetCrewId is null ? null : ResolveCrew(state, candidate.TargetCrewId);
        var coverage = candidate.ExpectedReport is null
            ? BaseCoverage
            : Array.AsReadOnly(BaseCoverage.Concat(["observability_only"]).ToArray());

        return new AsterF1FactSet
        {
            FamilyId = AsterF1Policy.FamilyId,
            FamilyVersion = AsterF1Policy.FamilyVersion,
            PacketId = candidate.PacketId,
            PacketKind = candidate.PacketType,
            SourceContextId = candidate.SourceContextId,
            IssuerId = candidate.IssuerId,
            IssuerLayer = candidate.IssuerLayer,
            IssuerSectorId = actor?.HomeSectorId,
            ToolId = null,
            ToolStatus = null,
            ToolSystemRefs = [],
            ToolSupportedActions = [],
            CrewId = candidate.TargetCrewId,
            CrewStatus = crew?.Status,
            CrewAssignedActorId = crew?.AssignedActorId,
            CrewHomeSectorId = crew?.HomeSectorId,
            CrewSystemRefs = crew?.SystemRefs ?? [],
            CandidateAction = null,
            CandidateMode = null,
            CandidateTaskType = candidate.TaskType,
            CandidateTargetLocation = candidate.TargetLocation,
            CoreStatus = decision.Status,
            CoreIssueCodes = decision.Issues.Select(issue => issue.Code).ToArray(),
            CoverageClassifications = coverage
        };
    }

    private static ActorState? ResolveActor(WorldState state, string actorId)
    {
        if (state.Actors is null)
        {
            return null;
        }

        var matches = state.Actors.Where(actor => actor is not null
            && string.Equals(actor.ActorId, actorId, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static ToolState? ResolveTool(WorldState state, string toolId)
    {
        if (state.Tools is null)
        {
            return null;
        }

        var matches = state.Tools.Where(tool => tool is not null
            && string.Equals(tool.ToolId, toolId, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static CrewState? ResolveCrew(WorldState state, string crewId)
    {
        if (state.Crews is null)
        {
            return null;
        }

        var matches = state.Crews.Where(crew => crew is not null
            && string.Equals(crew.CrewId, crewId, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
