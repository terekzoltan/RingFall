using Ringfall.Core.Artifacts;
using Ringfall.Core.Scenarios;
using Ringfall.Core.State;

namespace Ringfall.Core.Actions;

internal static class AsterL1ActionExecutor
{
    private const string CrewId = "crew_aster_repair_02";
    private const string RejectionSummary = "Core rejected the request; no world state was changed.";
    private const string DeferralSummary = "Core deferred this allowed request because it is outside the A4-G execution subset.";
    private const string FailureSummary = "Core could not complete the bounded request; no world state was changed.";

    // A4-F owns the issue catalog; every issue must belong to exactly one evidence dimension.
    internal static readonly IReadOnlyDictionary<string, string> IssueDimensions = BuildIssueDimensions();

    public static AsterL1ActionExecution Execute(WorldState state, AsterL1ToolActionCandidate? candidate)
    {
        var decision = AsterL1ActionValidator.Validate(state, candidate);
        ArgumentNullException.ThrowIfNull(state);
        if (!AdmitsIdentity(candidate?.PacketId, candidate?.IssuerId, candidate?.IssuerLayer))
        {
            return IdentityRejected(state, decision);
        }

        if (decision.Status != AsterL1DecisionStatus.Allowed)
        {
            return Complete(state, state, decision, AsterL1ExecutionDisposition.Rejected,
                candidate!.PacketId, candidate.IssuerId, candidate.IssuerLayer, "tool_action", RejectionSummary);
        }

        if (candidate!.ToolId != "local_grid_panel" || candidate.Action != "dry_run_reroute" || candidate.Mode != "dry_run")
        {
            return Complete(state, state, decision, AsterL1ExecutionDisposition.Deferred,
                candidate.PacketId, candidate.IssuerId, candidate.IssuerLayer, "tool_action", DeferralSummary,
                flag: "a4g_execution_subset_deferred");
        }

        // The fresh A4-F decision checks the exact branch pair and finite fraction.
        return Complete(state, state, decision, AsterL1ExecutionDisposition.Succeeded,
            candidate.PacketId, candidate.IssuerId, candidate.IssuerLayer, "tool_action",
            "The bounded reroute dry run completed without changing world state.");
    }

    public static AsterL1ActionExecution Execute(WorldState state, AsterL1WorkOrderCandidate? candidate)
    {
        var decision = AsterL1ActionValidator.Validate(state, candidate);
        ArgumentNullException.ThrowIfNull(state);
        if (!AdmitsIdentity(candidate?.PacketId, candidate?.IssuerId, candidate?.IssuerLayer))
        {
            return IdentityRejected(state, decision);
        }

        if (decision.Status != AsterL1DecisionStatus.Allowed)
        {
            return Complete(state, state, decision, AsterL1ExecutionDisposition.Rejected,
                candidate!.PacketId, candidate.IssuerId, candidate.IssuerLayer, "work_order", RejectionSummary);
        }

        if (candidate!.TargetCrewId != CrewId || candidate.TargetCrewPoolId is not null
            || candidate.TargetLocation != AsterF1Policy.TargetLocation
            || candidate.TaskType != AsterF1Policy.WorkOrderTask)
        {
            return Complete(state, state, decision, AsterL1ExecutionDisposition.Failed,
                candidate.PacketId, candidate.IssuerId, candidate.IssuerLayer, "work_order", FailureSummary,
                contextDiagnostic: "a4g_work_order_subset_unsupported");
        }

        var crews = state.Crews.Select(crew => crew.CrewId == CrewId
            ? crew with { Status = "unavailable" }
            : crew).ToArray();
        var tentative = state with { Crews = crews };
        try
        {
            InitialStateLoader.Validate(tentative);
        }
        catch (InitialStateValidationException)
        {
            return Complete(state, state, decision, AsterL1ExecutionDisposition.Failed,
                candidate.PacketId, candidate.IssuerId, candidate.IssuerLayer, "work_order", FailureSummary,
                flag: "a4g_post_validation_failure", contextDiagnostic: "post_validation_state_invalid");
        }

        var traceId = $"action-trace:{candidate.PacketId}";
        var diffId = $"state-diff:{candidate.PacketId}";
        var diff = new StateDiffArtifact
        {
            DiffType = "StateDiff",
            SchemaVersion = "0.1",
            DiffId = diffId,
            Tick = candidate.IssuedAtTick,
            SourceActionTraceId = traceId,
            DeterministicSeed = state.SimulationSeed,
            Changes =
            [
                new StateDiffChange
                {
                    Path = "/crews/crew_aster_repair_02/status",
                    Old = StateDiffValue.FromString("available"),
                    New = StateDiffValue.FromString("unavailable"),
                    Visibility = "issuer_observable"
                }
            ]
        };

        return Complete(state, tentative, decision, AsterL1ExecutionDisposition.Succeeded,
            candidate.PacketId, candidate.IssuerId, candidate.IssuerLayer, "work_order",
            "The requested Aster inspection crew was dispatched.", diff);
    }

    private static AsterL1ActionExecution IdentityRejected(WorldState state, AsterL1ActionDecision decision) =>
        new(state, state, decision, AsterL1ExecutionDisposition.IdentityRejected, null, null, null);

    private static bool AdmitsIdentity(string? packetId, string? issuerId, string? layer) =>
        !string.IsNullOrWhiteSpace(packetId)
        && !string.IsNullOrWhiteSpace(issuerId)
        && layer is "L1" or "L2" or "L3" or "sim" or "system";

    private static AsterL1ActionExecution Complete(
        WorldState initial, WorldState final, AsterL1ActionDecision decision,
        AsterL1ExecutionDisposition disposition, string packetId, string issuerId, string issuerLayer,
        string actionClass, string summary, StateDiffArtifact? diff = null,
        string? flag = null, string? contextDiagnostic = null)
    {
        var status = disposition switch
        {
            AsterL1ExecutionDisposition.Rejected => "rejected",
            AsterL1ExecutionDisposition.Deferred => "deferred",
            AsterL1ExecutionDisposition.Succeeded => "success",
            AsterL1ExecutionDisposition.Failed => "failed",
            _ => throw new ArgumentOutOfRangeException(nameof(disposition))
        };
        var trace = new ActionTraceArtifact
        {
            ActionTraceId = $"action-trace:{packetId}",
            SourcePacketId = packetId,
            IssuerId = issuerId,
            IssuerLayer = issuerLayer,
            ActionClass = actionClass,
            Validation = MapValidation(decision, contextDiagnostic),
            ExecutionStatus = status,
            StateDiffRefs = diff is null ? [] : [new ActionArtifactRef { RefId = diff.DiffId, RefType = "state_diff" }],
            EventsCreated = [],
            EvalFlags = flag is null ? [] : [flag]
        };
        var result = new ExecutionResultArtifact
        {
            PacketId = $"execution-result:{packetId}",
            SourcePacketId = packetId,
            ExecutionStatus = status,
            TrueEffectRefs = diff is null ? [] : [diff.DiffId],
            IssuerObservable = new IssuerObservableResult { Summary = summary },
            EventsCreated = []
        };
        return new AsterL1ActionExecution(initial, final, decision, disposition, trace, result, diff);
    }

    private static ActionValidation MapValidation(AsterL1ActionDecision decision, string? contextDiagnostic)
    {
        ActionValidationResult Dimension(string name)
        {
            var issues = decision.Issues.Where(issue => IssueDimensions[issue.Code] == name)
                .Select(issue => $"{issue.Code}:{issue.Field}").ToArray();
            if (name == "context" && contextDiagnostic is not null)
            {
                issues = [.. issues, contextDiagnostic];
            }
            return new ActionValidationResult
            {
                Status = issues.Length == 0 ? "pass" : "fail",
                Notes = issues.Length == 0 ? null : string.Join("; ", issues)
            };
        }

        return new ActionValidation
        {
            Schema = Dimension("schema"),
            Authority = Dimension("authority"),
            Context = Dimension("context"),
            Visibility = Dimension("visibility")
        };
    }

    private static IReadOnlyDictionary<string, string> BuildIssueDimensions()
    {
        var dimensions = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string dimension, params string[] codes)
        {
            foreach (var code in codes)
            {
                dimensions.Add(code, dimension);
            }
        }

        Add("schema", "invalid_candidate", "packet_type_mismatch", "schema_version_mismatch",
            "tool_mode_invalid", "tool_arguments_invalid", "tool_argument_type_invalid",
            "tool_argument_value_invalid", "work_order_target_missing");
        Add("authority", "issuer_not_found", "issuer_layer_mismatch", "issuer_layer_denied",
            "tool_not_referenced", "tool_unavailable", "tool_execute_denied",
            "tool_requires_dry_run_conflict", "tool_macro_surface_denied", "crew_not_referenced",
            "crew_unavailable", "crew_assignment_mismatch", "crew_sector_mismatch",
            "work_order_resource_authority_denied", "work_order_stealth_denied");
        Add("context", "invalid_world_state", "issuer_ambiguous", "issuer_sector_unsupported",
            "tool_not_found", "tool_ambiguous", "tool_action_not_supported", "tool_action_outside_family",
            "tool_arguments_missing", "tool_argument_unknown", "tool_argument_value_unsupported",
            "work_order_targets_conflict", "crew_pool_unsupported", "crew_not_found", "crew_ambiguous",
            "work_order_location_unsupported", "work_order_task_unsupported",
            "work_order_risk_tolerance_unsupported", "work_order_constraints_unsupported",
            "work_order_expected_report_unsupported", "work_order_fallback_unsupported");
        Add("visibility", "visibility_intent_unsupported", "work_order_public_visibility_unsupported");
        return dimensions;
    }
}
