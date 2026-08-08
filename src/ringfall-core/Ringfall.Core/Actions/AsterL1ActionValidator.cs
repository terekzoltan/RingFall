using System.Text.Json;
using Ringfall.Core.State;

namespace Ringfall.Core.Actions;

internal static class AsterL1ActionValidator
{
    private static readonly IReadOnlySet<string> IssuerLayers = new HashSet<string>(StringComparer.Ordinal)
    {
        "L1", "L2", "L3", "sim", "system"
    };

    private static readonly IReadOnlySet<string> VisibilityValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "private", "internal", "institutional", "public"
    };

    private static readonly IReadOnlySet<string> UrgencyValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "low", "normal", "medium", "high", "critical"
    };

    private static readonly IReadOnlySet<string> PriorityValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "low", "normal", "medium", "high", "critical"
    };

    private static readonly IReadOnlySet<string> RiskToleranceValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "low", "moderate", "high"
    };

    private static readonly IReadOnlySet<string> StealthValues = new HashSet<string>(StringComparer.Ordinal)
    {
        "normal", "quiet", "covert"
    };

    public static AsterL1ActionDecision Validate(
        WorldState? state,
        AsterL1ToolActionCandidate? candidate)
    {
        if (!IsWorldStateUsable(state))
        {
            return Decision(AsterL1IssueCatalog.InvalidWorldState);
        }

        var issues = ValidateCommonCandidate(
            candidate?.PacketId,
            candidate?.PacketType,
            "ToolActionRequest",
            candidate?.SchemaVersion,
            candidate?.IssuerId,
            candidate?.IssuerLayer,
            candidate?.IssuedAtTick,
            candidate?.IssuedAtTurn,
            candidate?.SourceContextId,
            candidate?.Confidence,
            candidate?.EvidenceRefs,
            candidate?.VisibilityIntent,
            candidate?.Urgency);

        if (candidate is null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate));
        }
        else
        {
            RequireNonBlank(candidate.ToolId, nameof(candidate.ToolId), issues);
            RequireNonBlank(candidate.Action, nameof(candidate.Action), issues);
            RequireNonBlank(candidate.Mode, nameof(candidate.Mode), issues);
            if (candidate.Mode is not ("dry_run" or "execute"))
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolModeInvalid, nameof(candidate.Mode)));
            }
            if (candidate.Arguments is not null
                && candidate.Arguments.Any(pair => pair.Value.ValueKind == JsonValueKind.Undefined))
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentsInvalid, nameof(candidate.Arguments)));
            }
        }

        if (HasInvalidIssue(issues))
        {
            return AsterL1ActionDecision.Create(issues);
        }

        var actor = ResolveIssuer(state!, candidate!.IssuerId, issues);
        if (actor is null)
        {
            return AsterL1ActionDecision.Create(issues);
        }

        ValidateIssuerAndCommonAuthority(actor, candidate.IssuerLayer, candidate.VisibilityIntent, issues);

        var matchingTools = state!.Tools.Where(tool => tool is not null
            && string.Equals(tool.ToolId, candidate.ToolId, StringComparison.Ordinal)).ToArray();
        if (matchingTools.Length == 0)
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolNotFound, nameof(candidate.ToolId)));
            return AsterL1ActionDecision.Create(issues);
        }
        if (matchingTools.Length != 1)
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolAmbiguous, nameof(candidate.ToolId)));
            return AsterL1ActionDecision.Create(issues);
        }

        var tool = matchingTools[0];
        if (actor.ToolRefs is null
            || !actor.ToolRefs.Contains(candidate.ToolId, StringComparer.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolNotReferenced, nameof(candidate.ToolId)));
        }
        if (!string.Equals(tool.Status, "available", StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolUnavailable, nameof(tool.Status)));
        }
        if (tool.SupportedActions is null
            || !tool.SupportedActions.Contains(candidate.Action, StringComparer.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolActionNotSupported, nameof(candidate.Action)));
        }

        _ = AsterF1Policy.TryGetActionRule(candidate.Action, out var rule);
        if (rule is null
            || !string.Equals(rule.ToolId, candidate.ToolId, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolActionOutsideFamily, nameof(candidate.Action)));
        }

        if (string.Equals(candidate.Mode, "execute", StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolExecuteDenied, nameof(candidate.Mode)));
        }
        if (rule is not null
            && rule.RejectFalseRequiresDryRun
            && candidate.RequiresDryRun == false)
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolRequiresDryRunConflict, nameof(candidate.RequiresDryRun)));
        }

        ValidateArguments(rule, candidate.Arguments, issues);
        return AsterL1ActionDecision.Create(issues);
    }

    public static AsterL1ActionDecision Validate(
        WorldState? state,
        AsterL1WorkOrderCandidate? candidate)
    {
        if (!IsWorldStateUsable(state))
        {
            return Decision(AsterL1IssueCatalog.InvalidWorldState);
        }

        var issues = ValidateCommonCandidate(
            candidate?.PacketId,
            candidate?.PacketType,
            "WorkOrderRequest",
            candidate?.SchemaVersion,
            candidate?.IssuerId,
            candidate?.IssuerLayer,
            candidate?.IssuedAtTick,
            candidate?.IssuedAtTurn,
            candidate?.SourceContextId,
            candidate?.Confidence,
            candidate?.EvidenceRefs,
            candidate?.VisibilityIntent,
            candidate?.Urgency);

        if (candidate is null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate));
        }
        else
        {
            RequireNonBlank(candidate.TargetLocation, nameof(candidate.TargetLocation), issues);
            RequireNonBlank(candidate.TaskType, nameof(candidate.TaskType), issues);
            RequireEnum(candidate.Priority, PriorityValues, nameof(candidate.Priority), issues);
            RequireOptionalEnum(candidate.RiskTolerance, RiskToleranceValues, nameof(candidate.RiskTolerance), issues);
            RequireOptionalEnum(candidate.StealthLevel, StealthValues, nameof(candidate.StealthLevel), issues);
            RequireOptionalEnum(candidate.PublicVisibility, VisibilityValues, nameof(candidate.PublicVisibility), issues);
            RequireNonBlankOptional(candidate.TargetCrewId, nameof(candidate.TargetCrewId), issues);
            RequireNonBlankOptional(candidate.TargetCrewPoolId, nameof(candidate.TargetCrewPoolId), issues);
            RequireStringList(candidate.AuthorizedResources, nameof(candidate.AuthorizedResources), issues);
            RequireStringList(candidate.Constraints, nameof(candidate.Constraints), issues);
            if (candidate.TargetCrewId is null && candidate.TargetCrewPoolId is null)
            {
                issues.Add(Issue(AsterL1IssueCatalog.WorkOrderTargetMissing));
            }
        }

        if (HasInvalidIssue(issues))
        {
            return AsterL1ActionDecision.Create(issues);
        }

        var actor = ResolveIssuer(state!, candidate!.IssuerId, issues);
        if (actor is null)
        {
            return AsterL1ActionDecision.Create(issues);
        }

        ValidateIssuerAndCommonAuthority(actor, candidate.IssuerLayer, candidate.VisibilityIntent, issues);

        CrewState? crew = null;
        if (candidate.TargetCrewId is not null && candidate.TargetCrewPoolId is not null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderTargetsConflict));
        }
        else if (candidate.TargetCrewPoolId is not null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.CrewPoolUnsupported, nameof(candidate.TargetCrewPoolId)));
        }
        else
        {
            var matchingCrews = state!.Crews.Where(candidateCrew => candidateCrew is not null
                && string.Equals(candidateCrew.CrewId, candidate.TargetCrewId, StringComparison.Ordinal)).ToArray();
            if (matchingCrews.Length == 0)
            {
                issues.Add(Issue(AsterL1IssueCatalog.CrewNotFound, nameof(candidate.TargetCrewId)));
                return AsterL1ActionDecision.Create(issues);
            }
            if (matchingCrews.Length != 1)
            {
                issues.Add(Issue(AsterL1IssueCatalog.CrewAmbiguous, nameof(candidate.TargetCrewId)));
                return AsterL1ActionDecision.Create(issues);
            }
            crew = matchingCrews[0];
        }

        if (crew is not null
            && !actor.CrewRefs.Contains(candidate.TargetCrewId!, StringComparer.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.CrewNotReferenced, nameof(candidate.TargetCrewId)));
        }
        if (crew is not null
            && !string.Equals(crew.Status, "available", StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.CrewUnavailable, nameof(crew.Status)));
        }
        if (crew is not null
            && !string.Equals(crew.AssignedActorId, actor.ActorId, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.CrewAssignmentMismatch, nameof(crew.AssignedActorId)));
        }
        if (crew is not null
            && (!string.Equals(crew.HomeSectorId, AsterF1Policy.SectorId, StringComparison.Ordinal)
                || !string.Equals(actor.HomeSectorId, AsterF1Policy.SectorId, StringComparison.Ordinal)))
        {
            issues.Add(Issue(AsterL1IssueCatalog.CrewSectorMismatch, nameof(crew.HomeSectorId)));
        }
        if (!string.Equals(candidate.TargetLocation, AsterF1Policy.TargetLocation, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderLocationUnsupported, nameof(candidate.TargetLocation)));
        }
        if (!string.Equals(candidate.TaskType, AsterF1Policy.WorkOrderTask, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderTaskUnsupported, nameof(candidate.TaskType)));
        }
        if (candidate.RiskTolerance is not null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderRiskToleranceUnsupported, nameof(candidate.RiskTolerance)));
        }
        if (candidate.AuthorizedResources is { Count: > 0 })
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderResourceAuthorityDenied, nameof(candidate.AuthorizedResources)));
        }
        if (candidate.Constraints is { Count: > 0 })
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderConstraintsUnsupported, nameof(candidate.Constraints)));
        }
        if (candidate.ExpectedReport is not null
            && !string.Equals(candidate.ExpectedReport, "short_structured", StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderExpectedReportUnsupported, nameof(candidate.ExpectedReport)));
        }
        if (candidate.FallbackProtocol is { Length: > 0 })
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderFallbackUnsupported, nameof(candidate.FallbackProtocol)));
        }
        if (candidate.StealthLevel is "quiet" or "covert")
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderStealthDenied, nameof(candidate.StealthLevel)));
        }
        if (candidate.PublicVisibility is not null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.WorkOrderPublicVisibilityUnsupported, nameof(candidate.PublicVisibility)));
        }

        return AsterL1ActionDecision.Create(issues);
    }

    private static List<AsterL1ActionIssue> ValidateCommonCandidate(
        string? packetId,
        string? packetType,
        string expectedPacketType,
        string? schemaVersion,
        string? issuerId,
        string? issuerLayer,
        int? issuedAtTick,
        string? issuedAtTurn,
        string? sourceContextId,
        double? confidence,
        IReadOnlyList<string>? evidenceRefs,
        string? visibilityIntent,
        string? urgency)
    {
        var issues = new List<AsterL1ActionIssue>();
        RequireNonBlank(packetId, "PacketId", issues);
        RequireNonBlank(issuerId, "IssuerId", issues);
        RequireEnum(issuerLayer, IssuerLayers, "IssuerLayer", issues);
        RequireNonBlank(sourceContextId, "SourceContextId", issues);
        RequireNonBlankOptional(issuedAtTurn, "IssuedAtTurn", issues);
        RequireStringList(evidenceRefs, "EvidenceRefs", issues);
        RequireOptionalEnum(visibilityIntent, VisibilityValues, "VisibilityIntent", issues);
        RequireOptionalEnum(urgency, UrgencyValues, "Urgency", issues);

        if (!string.Equals(packetType, expectedPacketType, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.PacketTypeMismatch, "PacketType"));
        }
        if (!string.Equals(schemaVersion, AsterF1Policy.FamilyVersion, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.SchemaVersionMismatch, "SchemaVersion"));
        }
        if (issuedAtTick is null || issuedAtTick < 0)
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, "IssuedAtTick"));
        }
        if (confidence is not null && (!double.IsFinite(confidence.Value)
            || confidence.Value < 0
            || confidence.Value > 1))
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, "Confidence"));
        }

        return issues;
    }

    private static ActorState? ResolveIssuer(
        WorldState state,
        string issuerId,
        ICollection<AsterL1ActionIssue> issues)
    {
        var matchingActors = state.Actors.Where(actor => actor is not null
            && string.Equals(actor.ActorId, issuerId, StringComparison.Ordinal)).ToArray();
        if (matchingActors.Length == 0)
        {
            issues.Add(Issue(AsterL1IssueCatalog.IssuerNotFound, "IssuerId"));
            return null;
        }
        if (matchingActors.Length != 1)
        {
            issues.Add(Issue(AsterL1IssueCatalog.IssuerAmbiguous, "IssuerId"));
            return null;
        }
        return matchingActors[0];
    }

    private static void ValidateIssuerAndCommonAuthority(
        ActorState actor,
        string issuerLayer,
        string? visibilityIntent,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (!string.Equals(actor.Layer, issuerLayer, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.IssuerLayerMismatch, "IssuerLayer"));
        }
        if (!string.Equals(actor.Layer, AsterF1Policy.IssuerLayer, StringComparison.Ordinal)
            || !string.Equals(issuerLayer, AsterF1Policy.IssuerLayer, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.IssuerLayerDenied, "IssuerLayer"));
        }
        if (!string.Equals(actor.HomeSectorId, AsterF1Policy.SectorId, StringComparison.Ordinal))
        {
            issues.Add(Issue(AsterL1IssueCatalog.IssuerSectorUnsupported, "IssuerId"));
        }
        if (visibilityIntent is not null)
        {
            issues.Add(Issue(AsterL1IssueCatalog.VisibilityIntentUnsupported, "VisibilityIntent"));
        }
    }

    private static void ValidateArguments(
        AsterF1ActionRule? rule,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (arguments is null)
        {
            if (rule is not null && !rule.AllowAbsentArguments)
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentsMissing, "Arguments"));
            }
            return;
        }

        foreach (var key in arguments.Keys)
        {
            if (AsterF1Policy.IsForbiddenArgumentKey(key))
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolMacroSurfaceDenied, key));
            }
            else if (rule is null || !rule.RequiredArguments.Contains(key, StringComparer.Ordinal))
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentUnknown, key));
            }
        }

        if (rule is null)
        {
            return;
        }

        foreach (var required in rule.RequiredArguments)
        {
            if (!arguments.ContainsKey(required))
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentsMissing, required));
            }
        }

        if (arguments.TryGetValue("branch_id", out var branchId))
        {
            ValidateStringValue(
                branchId,
                "branch_id",
                value => value is AsterF1Policy.SourceBranch or AsterF1Policy.TargetBranch,
                issues);
        }
        if (arguments.TryGetValue("from_branch", out var fromBranch))
        {
            ValidateStringValue(
                fromBranch,
                "from_branch",
                value => string.Equals(value, AsterF1Policy.SourceBranch, StringComparison.Ordinal),
                issues);
        }
        if (arguments.TryGetValue("to_branch", out var toBranch))
        {
            ValidateStringValue(
                toBranch,
                "to_branch",
                value => string.Equals(value, AsterF1Policy.TargetBranch, StringComparison.Ordinal),
                issues);
        }
        if (arguments.TryGetValue("asset_id", out var assetId))
        {
            ValidateStringValue(
                assetId,
                "asset_id",
                value => string.Equals(value, AsterF1Policy.CouplerAsset, StringComparison.Ordinal),
                issues);
        }
        if (arguments.TryGetValue("load_fraction", out var loadFraction))
        {
            if (loadFraction.ValueKind != JsonValueKind.Number
                || !loadFraction.TryGetDouble(out var value))
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentTypeInvalid, "load_fraction"));
            }
            else if (!double.IsFinite(value) || value <= 0)
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentValueInvalid, "load_fraction"));
            }
            else if (value > AsterF1Policy.MaximumRerouteFraction)
            {
                issues.Add(Issue(AsterL1IssueCatalog.ToolMacroSurfaceDenied, "load_fraction"));
            }
        }
    }

    private static void ValidateStringValue(
        JsonElement element,
        string field,
        Func<string, bool> supported,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentTypeInvalid, field));
            return;
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentValueInvalid, field));
        }
        else if (!supported(value))
        {
            issues.Add(Issue(AsterL1IssueCatalog.ToolArgumentValueUnsupported, field));
        }
    }

    private static bool IsWorldStateUsable(WorldState? state)
    {
        if (state is null
            || state.Sectors is null
            || state.Sectors.Count == 0
            || state.Actors is null
            || state.Actors.Count == 0
            || state.Crews is null
            || state.Crews.Count == 0
            || state.Tools is null
            || state.Tools.Count == 0
            || state.Sectors.Any(sector => sector is null)
            || state.Actors.Any(actor => actor is null)
            || state.Crews.Any(crew => crew is null)
            || state.Tools.Any(tool => tool is null))
        {
            return false;
        }

        foreach (var sector in state.Sectors)
        {
            if (string.IsNullOrWhiteSpace(sector.SectorId)
                || sector.Systems is null
                || sector.Systems.Any(system => system is null))
            {
                return false;
            }
        }

        var sectorIds = new HashSet<string>(StringComparer.Ordinal);
        var systemIds = new HashSet<string>(StringComparer.Ordinal);
        var asterSectorCount = 0;
        foreach (var sector in state.Sectors)
        {
            if (!sectorIds.Add(sector.SectorId))
            {
                return false;
            }
            if (string.Equals(sector.SectorId, AsterF1Policy.SectorId, StringComparison.Ordinal))
            {
                asterSectorCount++;
            }

            foreach (var system in sector.Systems)
            {
                if (string.IsNullOrWhiteSpace(system.SystemId) || !systemIds.Add(system.SystemId))
                {
                    return false;
                }
            }
        }

        if (asterSectorCount != 1)
        {
            return false;
        }

        var actorIds = state.Actors.Select(actor => actor.ActorId).ToHashSet(StringComparer.Ordinal);
        var crewIds = state.Crews.Select(crew => crew.CrewId).ToHashSet(StringComparer.Ordinal);
        var toolIds = state.Tools.Select(tool => tool.ToolId).ToHashSet(StringComparer.Ordinal);

        foreach (var actor in state.Actors)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorId)
                || string.IsNullOrWhiteSpace(actor.Layer)
                || string.IsNullOrWhiteSpace(actor.HomeSectorId)
                || !sectorIds.Contains(actor.HomeSectorId)
                || !HasUniqueNonBlankValues(actor.SystemRefs)
                || !HasUniqueNonBlankValues(actor.ToolRefs)
                || !HasUniqueNonBlankValues(actor.CrewRefs)
                || actor.SystemRefs.Any(systemRef => !systemIds.Contains(systemRef))
                || actor.ToolRefs.Any(toolRef => !toolIds.Contains(toolRef))
                || actor.CrewRefs.Any(crewRef => !crewIds.Contains(crewRef)))
            {
                return false;
            }
        }

        foreach (var crew in state.Crews)
        {
            if (string.IsNullOrWhiteSpace(crew.CrewId)
                || string.IsNullOrWhiteSpace(crew.AssignedActorId)
                || !actorIds.Contains(crew.AssignedActorId)
                || string.IsNullOrWhiteSpace(crew.HomeSectorId)
                || !sectorIds.Contains(crew.HomeSectorId)
                || crew.Status is not ("available" or "unavailable")
                || !HasUniqueNonBlankValues(crew.SystemRefs, requireNonEmpty: true)
                || crew.SystemRefs.Any(systemRef => !systemIds.Contains(systemRef)))
            {
                return false;
            }
        }

        foreach (var tool in state.Tools)
        {
            if (string.IsNullOrWhiteSpace(tool.ToolId)
                || tool.Status is not ("available" or "unavailable")
                || !HasUniqueNonBlankValues(tool.SystemRefs, requireNonEmpty: true)
                || !HasUniqueNonBlankValues(tool.SupportedActions, requireNonEmpty: true)
                || tool.SystemRefs.Any(systemRef => !systemIds.Contains(systemRef)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUniqueNonBlankValues(
        IReadOnlyList<string>? values,
        bool requireNonEmpty = false)
    {
        if (values is null || (requireNonEmpty && values.Count == 0))
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return values.All(value => !string.IsNullOrWhiteSpace(value) && seen.Add(value));
    }

    private static void RequireNonBlank(
        string? value,
        string field,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, field));
        }
    }

    private static void RequireNonBlankOptional(
        string? value,
        string field,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, field));
        }
    }

    private static void RequireEnum(
        string? value,
        IReadOnlySet<string> values,
        string field,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (value is null || !values.Contains(value))
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, field));
        }
    }

    private static void RequireOptionalEnum(
        string? value,
        IReadOnlySet<string> values,
        string field,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (value is not null && !values.Contains(value))
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, field));
        }
    }

    private static void RequireStringList(
        IReadOnlyList<string>? values,
        string field,
        ICollection<AsterL1ActionIssue> issues)
    {
        if (values is not null && values.Any(string.IsNullOrWhiteSpace))
        {
            issues.Add(Issue(AsterL1IssueCatalog.InvalidCandidate, field));
        }
    }

    private static bool HasInvalidIssue(IEnumerable<AsterL1ActionIssue> issues)
    {
        return issues.Any(issue => AsterL1IssueCatalog.Status(issue.Code) == AsterL1DecisionStatus.Invalid);
    }

    private static AsterL1ActionDecision Decision(string code, string? field = null)
    {
        return AsterL1ActionDecision.Create([Issue(code, field)]);
    }

    private static AsterL1ActionIssue Issue(string code, string? field = null)
    {
        return new AsterL1ActionIssue(code, field);
    }
}
