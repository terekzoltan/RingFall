using System.Text.Json;
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

    // The P1 path consumes the executor's decision; the legacy Export overloads remain report-only.
    internal static AsterF1P1Facts ExportP1(WorldState state, AsterL1ToolActionCandidate candidate,
        AsterL1ActionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var actor = ResolveActor(state, candidate.IssuerId);
        var tool = ResolveTool(state, candidate.ToolId);
        var unsupported = new HashSet<string>(StringComparer.Ordinal);
        var actorReferencesTool = actor?.ToolRefs.Contains(candidate.ToolId, StringComparer.Ordinal) == true;
        if (!actorReferencesTool) unsupported.Add("tool_surface");
        _ = AsterF1Policy.TryGetActionRule(candidate.Action, out var rule);
        if (actor is null || tool is null || rule is null
            || rule?.ToolId != candidate.ToolId || !tool.SupportedActions.Contains(candidate.Action, StringComparer.Ordinal))
        {
            unsupported.Add("tool_surface");
        }
        if (actor?.ActorId != "A1" || actor.Layer != "L1" || actor.HomeSectorId != AsterF1Policy.SectorId
            || tool is not null && (tool.ToolId is not ("local_grid_panel" or "maintenance_console")
                || tool.SystemRefs.Any(value => !KnownSystem(value))
                || tool.SupportedActions.Any(value => !AsterF1Policy.TryGetActionRule(value, out var supported)
                    || supported?.ToolId != tool.ToolId))) unsupported.Add("tool_surface");
        if (candidate.VisibilityIntent is not null) unsupported.Add("visibility_claim");

        var names = new[] { "branch_id", "from_branch", "to_branch", "asset_id", "load_fraction" };
        var rows = new List<AsterF1P1Field>();
        foreach (var name in names)
        {
            if (candidate.Arguments is null || !candidate.Arguments.TryGetValue(name, out var value))
            {
                rows.Add(new AsterF1P1Field(name, "absent"));
                continue;
            }
            var relevant = rule is not null && rule.RequiredArguments.Contains(name, StringComparer.Ordinal);
            if (!relevant)
                unsupported.Add("tool_argument");
            if (value.ValueKind == JsonValueKind.Null)
            {
                rows.Add(new AsterF1P1Field(name, "null", Coverage: "unsupported"));
                unsupported.Add("tool_argument");
            }
            else if (name == "load_fraction" && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out var fraction) && double.IsFinite(fraction))
            {
                rows.Add(new AsterF1P1Field(name, "value", fraction,
                    relevant ? "guarded_by_core_validator" : "unsupported"));
            }
            else if (name != "load_fraction" && value.ValueKind == JsonValueKind.String
                && AllowedArgument(name, value.GetString()))
            {
                rows.Add(new AsterF1P1Field(name, "value", value.GetString(),
                    relevant ? "guarded_by_core_validator" : "unsupported"));
            }
            else
            {
                rows.Add(new AsterF1P1Field(name, "wrong_type", Coverage: "unsupported"));
                unsupported.Add("tool_argument");
            }
        }
        if (candidate.Arguments is not null && candidate.Arguments.Keys.Any(key => !names.Contains(key, StringComparer.Ordinal)))
            unsupported.Add("tool_argument");

        var knownMode = candidate.Mode is "dry_run" or "execute" ? candidate.Mode : null;
        if (knownMode is null) unsupported.Add("tool_surface");
        var toolFacts = new
        {
            tool_id = actorReferencesTool && tool?.ToolId is ("local_grid_panel" or "maintenance_console") ? tool.ToolId : null,
            status = actorReferencesTool && tool?.Status is ("available" or "unavailable") ? tool.Status : null,
            system_refs = actorReferencesTool ? tool?.SystemRefs.Where(KnownSystem).ToArray() : null,
            supported_actions = actorReferencesTool ? tool?.SupportedActions.Where(value => AsterF1Policy.TryGetActionRule(value, out var supported)
                && supported?.ToolId == tool.ToolId).ToArray() : null,
            actor_tool_referenced = actorReferencesTool,
            action = rule?.Action == candidate.Action ? rule.Action : null,
            mode = knownMode,
            arguments_presence = candidate.Arguments is null ? "absent" : "value",
            requires_dry_run = new AsterF1P1Field("requires_dry_run",
                candidate.RequiresDryRun is null ? "absent" : "value", candidate.RequiresDryRun)
        };
        return new AsterF1P1Facts("ToolActionRequest", actor?.ActorId == "A1" ? "A1" : null,
            actor?.Layer == "L1" ? "L1" : null, actor?.HomeSectorId == AsterF1Policy.SectorId ? AsterF1Policy.SectorId : null,
            toolFacts, null, rows, OrderUnsupported(unsupported));
    }

    internal static AsterF1P1Facts ExportP1(WorldState state, AsterL1WorkOrderCandidate candidate,
        AsterL1ActionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var actor = ResolveActor(state, candidate.IssuerId);
        var crew = candidate.TargetCrewId is null ? null : ResolveCrew(state, candidate.TargetCrewId);
        var unsupported = new HashSet<string>(StringComparer.Ordinal);
        var actorReferencesCrew = actor?.CrewRefs.Contains(candidate.TargetCrewId ?? string.Empty, StringComparer.Ordinal) == true;
        if (!actorReferencesCrew) unsupported.Add("crew_target");
        if (actor?.ActorId != "A1" || actor.Layer != "L1" || actor.HomeSectorId != AsterF1Policy.SectorId
            || crew is not null && (crew.CrewId != "crew_aster_repair_02" || crew.AssignedActorId != "A1"
                || crew.HomeSectorId != AsterF1Policy.SectorId || crew.SystemRefs.Any(value => !KnownSystem(value))))
            unsupported.Add("crew_target");
        if (actor is null || crew is null || candidate.TargetCrewPoolId is not null
            || candidate.TargetCrewId is null || candidate.TargetCrewId != crew.CrewId)
            unsupported.Add("crew_target");
        if (candidate.TargetLocation != AsterF1Policy.TargetLocation || candidate.TaskType != AsterF1Policy.WorkOrderTask
            || candidate.Priority is not ("low" or "normal" or "medium" or "high" or "critical")
            || candidate.RiskTolerance is not null || candidate.AuthorizedResources is { Count: > 0 }
            || candidate.Constraints is { Count: > 0 } || candidate.ExpectedReport is not (null or "short_structured")
            || !string.IsNullOrEmpty(candidate.FallbackProtocol) || candidate.StealthLevel is not (null or "normal"))
            unsupported.Add("work_order_option");
        if (candidate.VisibilityIntent is not null || candidate.PublicVisibility is not null)
            unsupported.Add("visibility_claim");
        var workFacts = new
        {
            crew_id = actorReferencesCrew && crew?.CrewId == "crew_aster_repair_02" ? crew.CrewId : null,
            crew_status = actorReferencesCrew && crew?.Status is ("available" or "unavailable") ? crew.Status : null,
            assigned_actor_id = actorReferencesCrew && crew?.AssignedActorId == "A1" ? "A1" : null,
            crew_home_sector_id = actorReferencesCrew && crew?.HomeSectorId == AsterF1Policy.SectorId ? AsterF1Policy.SectorId : null,
            crew_system_refs = actorReferencesCrew ? crew?.SystemRefs.Where(KnownSystem).ToArray() : null,
            actor_crew_referenced = actorReferencesCrew,
            target_crew_id = new AsterF1P1Field("target_crew_id", candidate.TargetCrewId is null ? "absent" : "value",
                actorReferencesCrew && crew?.CrewId == "crew_aster_repair_02" ? crew.CrewId : null,
                actorReferencesCrew && crew?.CrewId == "crew_aster_repair_02" ? "guarded_by_core_validator" : "unsupported"),
            target_crew_pool_id = new AsterF1P1Field("target_crew_pool_id", candidate.TargetCrewPoolId is null ? "absent" : "value",
                Coverage: candidate.TargetCrewPoolId is null ? "guarded_by_core_validator" : "unsupported"),
            target_location = candidate.TargetLocation == AsterF1Policy.TargetLocation ? candidate.TargetLocation : null,
            task_type = candidate.TaskType == AsterF1Policy.WorkOrderTask ? candidate.TaskType : null,
            options = new[]
            {
                new AsterF1P1Field("priority", "value", candidate.Priority is "low" or "normal" or "medium" or "high" or "critical" ? candidate.Priority : null,
                    candidate.Priority is "low" or "normal" or "medium" or "high" or "critical" ? "guarded_by_core_validator" : "unsupported"),
                new AsterF1P1Field("risk_tolerance", candidate.RiskTolerance is null ? "absent" : "value",
                    Coverage: candidate.RiskTolerance is null ? "guarded_by_core_validator" : "unsupported"),
                new AsterF1P1Field("authorized_resources", candidate.AuthorizedResources is null ? "absent" : candidate.AuthorizedResources.Count == 0 ? "empty" : "value",
                    Coverage: candidate.AuthorizedResources is { Count: > 0 } ? "unsupported" : "guarded_by_core_validator"),
                new AsterF1P1Field("constraints", candidate.Constraints is null ? "absent" : candidate.Constraints.Count == 0 ? "empty" : "value",
                    Coverage: candidate.Constraints is { Count: > 0 } ? "unsupported" : "guarded_by_core_validator"),
                new AsterF1P1Field("expected_report", candidate.ExpectedReport is null ? "absent" : "value",
                    candidate.ExpectedReport == "short_structured" ? candidate.ExpectedReport : null,
                    candidate.ExpectedReport is null or "short_structured" ? "observability_only" : "unsupported"),
                new AsterF1P1Field("fallback_protocol", candidate.FallbackProtocol is null ? "absent" : candidate.FallbackProtocol.Length == 0 ? "empty" : "value",
                    Coverage: string.IsNullOrEmpty(candidate.FallbackProtocol) ? "guarded_by_core_validator" : "unsupported"),
                new AsterF1P1Field("stealth_level", candidate.StealthLevel is null ? "absent" : "value", candidate.StealthLevel == "normal" ? "normal" : null,
                    candidate.StealthLevel is null or "normal" ? "guarded_by_core_validator" : "unsupported"),
                new AsterF1P1Field("public_visibility", candidate.PublicVisibility is null ? "absent" : "value",
                    Coverage: candidate.PublicVisibility is null ? "guarded_by_core_validator" : "unsupported")
            }
        };
        return new AsterF1P1Facts("WorkOrderRequest", actor?.ActorId == "A1" ? "A1" : null,
            actor?.Layer == "L1" ? "L1" : null, actor?.HomeSectorId == AsterF1Policy.SectorId ? AsterF1Policy.SectorId : null,
            null, workFacts, [], OrderUnsupported(unsupported));
    }

    private static bool AllowedArgument(string name, string? value) => name switch
    {
        "branch_id" => value is AsterF1Policy.SourceBranch or AsterF1Policy.TargetBranch,
        "from_branch" => value == AsterF1Policy.SourceBranch,
        "to_branch" => value == AsterF1Policy.TargetBranch,
        "asset_id" => value == AsterF1Policy.CouplerAsset,
        _ => false
    };

    private static bool KnownSystem(string value) => value is "R2" or "R5" or "R6" or "R10";

    private static IReadOnlyList<string> OrderUnsupported(HashSet<string> values) =>
        new[] { "tool_argument", "tool_surface", "crew_target", "work_order_option", "current_location", "visibility_claim" }
            .Where(values.Contains).ToArray();
}
