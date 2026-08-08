using System.Reflection;
using System.Text.Json;
using Ringfall.Core.Actions;
using Ringfall.Core.Scenarios;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class AsterL1ActionValidatorTests
{
    [TestMethod]
    public void Candidate_contracts_carry_every_schema_field()
    {
        AssertProperties<AsterL1ToolActionCandidate>(
            "PacketId", "PacketType", "SchemaVersion", "IssuerId", "IssuerLayer",
            "IssuedAtTick", "IssuedAtTurn", "SourceContextId", "Rationale", "Confidence",
            "EvidenceRefs", "VisibilityIntent", "Urgency", "ToolId", "Action", "Arguments",
            "Mode", "RequiresDryRun");
        AssertProperties<AsterL1WorkOrderCandidate>(
            "PacketId", "PacketType", "SchemaVersion", "IssuerId", "IssuerLayer",
            "IssuedAtTick", "IssuedAtTurn", "SourceContextId", "Rationale", "Confidence",
            "EvidenceRefs", "VisibilityIntent", "Urgency", "TargetCrewId", "TargetCrewPoolId",
            "TargetLocation", "TaskType", "Priority", "RiskTolerance", "AuthorizedResources",
            "Constraints", "ExpectedReport", "FallbackProtocol", "StealthLevel", "PublicVisibility");
    }

    [TestMethod]
    public void Candidate_collections_and_argument_values_are_copied()
    {
        var evidence = new List<string> { "obs-1" };
        var workEvidence = new List<string> { "obs-work-1" };
        var resources = new List<string> { "resource-1" };
        var constraints = new List<string> { "constraint-1" };
        using var document = JsonDocument.Parse("{\"branch_id\":\"Aster-G3\"}");
        var arguments = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        var candidate = ToolCandidate("query_branch_load", arguments: arguments) with
        {
            EvidenceRefs = evidence
        };
        var workOrder = WorkOrderCandidate() with
        {
            EvidenceRefs = workEvidence,
            AuthorizedResources = resources,
            Constraints = constraints
        };

        evidence.Add("obs-2");
        workEvidence.Add("obs-work-2");
        resources.Add("resource-2");
        constraints.Add("constraint-2");
        arguments.Clear();
        document.Dispose();

        CollectionAssert.AreEqual(new[] { "obs-1" }, candidate.EvidenceRefs!.ToArray());
        Assert.AreEqual("Aster-G3", candidate.Arguments!["branch_id"].GetString());
        CollectionAssert.AreEqual(new[] { "obs-work-1" }, workOrder.EvidenceRefs!.ToArray());
        CollectionAssert.AreEqual(new[] { "resource-1" }, workOrder.AuthorizedResources!.ToArray());
        CollectionAssert.AreEqual(new[] { "constraint-1" }, workOrder.Constraints!.ToArray());
    }

    [TestMethod]
    public void Issue_catalog_and_status_precedence_are_frozen()
    {
        var expected = new[]
        {
            "invalid_world_state", "invalid_candidate", "packet_type_mismatch", "schema_version_mismatch",
            "issuer_not_found", "issuer_ambiguous", "issuer_layer_mismatch", "issuer_layer_denied",
            "issuer_sector_unsupported", "visibility_intent_unsupported", "tool_not_found", "tool_ambiguous",
            "tool_not_referenced", "tool_unavailable", "tool_action_not_supported", "tool_action_outside_family",
            "tool_mode_invalid", "tool_execute_denied", "tool_requires_dry_run_conflict", "tool_arguments_invalid",
            "tool_arguments_missing", "tool_macro_surface_denied", "tool_argument_unknown",
            "tool_argument_type_invalid", "tool_argument_value_invalid", "tool_argument_value_unsupported",
            "work_order_target_missing", "work_order_targets_conflict", "crew_pool_unsupported", "crew_not_found",
            "crew_ambiguous", "crew_not_referenced", "crew_unavailable", "crew_assignment_mismatch",
            "crew_sector_mismatch", "work_order_location_unsupported", "work_order_task_unsupported",
            "work_order_risk_tolerance_unsupported", "work_order_resource_authority_denied",
            "work_order_constraints_unsupported", "work_order_expected_report_unsupported",
            "work_order_fallback_unsupported", "work_order_stealth_denied",
            "work_order_public_visibility_unsupported"
        };

        CollectionAssert.AreEqual(expected, AsterL1IssueCatalog.Codes.ToArray());

        var invalid = new HashSet<string>(StringComparer.Ordinal)
        {
            AsterL1IssueCatalog.InvalidWorldState,
            AsterL1IssueCatalog.InvalidCandidate,
            AsterL1IssueCatalog.PacketTypeMismatch,
            AsterL1IssueCatalog.SchemaVersionMismatch,
            AsterL1IssueCatalog.IssuerAmbiguous,
            AsterL1IssueCatalog.ToolAmbiguous,
            AsterL1IssueCatalog.ToolModeInvalid,
            AsterL1IssueCatalog.ToolArgumentsInvalid,
            AsterL1IssueCatalog.ToolArgumentTypeInvalid,
            AsterL1IssueCatalog.ToolArgumentValueInvalid,
            AsterL1IssueCatalog.WorkOrderTargetMissing,
            AsterL1IssueCatalog.CrewAmbiguous
        };
        var denied = new HashSet<string>(StringComparer.Ordinal)
        {
            AsterL1IssueCatalog.IssuerNotFound,
            AsterL1IssueCatalog.IssuerLayerMismatch,
            AsterL1IssueCatalog.IssuerLayerDenied,
            AsterL1IssueCatalog.ToolNotFound,
            AsterL1IssueCatalog.ToolNotReferenced,
            AsterL1IssueCatalog.ToolUnavailable,
            AsterL1IssueCatalog.ToolActionNotSupported,
            AsterL1IssueCatalog.ToolExecuteDenied,
            AsterL1IssueCatalog.ToolRequiresDryRunConflict,
            AsterL1IssueCatalog.ToolArgumentsMissing,
            AsterL1IssueCatalog.ToolMacroSurfaceDenied,
            AsterL1IssueCatalog.WorkOrderTargetsConflict,
            AsterL1IssueCatalog.CrewNotFound,
            AsterL1IssueCatalog.CrewNotReferenced,
            AsterL1IssueCatalog.CrewUnavailable,
            AsterL1IssueCatalog.CrewAssignmentMismatch,
            AsterL1IssueCatalog.CrewSectorMismatch,
            AsterL1IssueCatalog.WorkOrderResourceAuthorityDenied,
            AsterL1IssueCatalog.WorkOrderStealthDenied
        };

        foreach (var code in expected)
        {
            var expectedStatus = invalid.Contains(code)
                ? AsterL1DecisionStatus.Invalid
                : denied.Contains(code)
                    ? AsterL1DecisionStatus.Denied
                    : AsterL1DecisionStatus.Unsupported;
            Assert.AreEqual(expectedStatus, AsterL1IssueCatalog.Status(code), code);
        }

        var decision = AsterL1ActionDecision.Create(
        [
            new(AsterL1IssueCatalog.ToolArgumentUnknown, "z"),
            new(AsterL1IssueCatalog.ToolExecuteDenied),
            new(AsterL1IssueCatalog.InvalidCandidate, "b"),
            new(AsterL1IssueCatalog.InvalidCandidate, "a"),
            new(AsterL1IssueCatalog.InvalidCandidate, "a")
        ]);

        Assert.AreEqual(AsterL1DecisionStatus.Invalid, decision.Status);
        CollectionAssert.AreEqual(
            new[] { "invalid_candidate:a", "invalid_candidate:b", "tool_execute_denied:", "tool_argument_unknown:z" },
            decision.Issues.Select(issue => $"{issue.Code}:{issue.Field}").ToArray());
    }

    [TestMethod]
    public void Packet_and_schema_mismatches_are_exact_for_both_candidate_types()
    {
        var state = LoadFixture();

        AssertDecisionExact(
            "tool_packet_type_mismatch",
            AsterL1ActionValidator.Validate(
                state,
                ToolCandidate("query_heat_alarm") with { PacketType = "WorkOrderRequest" }),
            AsterL1DecisionStatus.Invalid,
            "packet_type_mismatch:PacketType");
        AssertDecisionExact(
            "tool_schema_version_mismatch",
            AsterL1ActionValidator.Validate(
                state,
                ToolCandidate("query_heat_alarm") with { SchemaVersion = "1.0" }),
            AsterL1DecisionStatus.Invalid,
            "schema_version_mismatch:SchemaVersion");
        AssertDecisionExact(
            "work_order_packet_type_mismatch",
            AsterL1ActionValidator.Validate(
                state,
                WorkOrderCandidate() with { PacketType = "ToolActionRequest" }),
            AsterL1DecisionStatus.Invalid,
            "packet_type_mismatch:PacketType");
        AssertDecisionExact(
            "work_order_schema_version_mismatch",
            AsterL1ActionValidator.Validate(
                state,
                WorkOrderCandidate() with { SchemaVersion = "1.0" }),
            AsterL1DecisionStatus.Invalid,
            "schema_version_mismatch:SchemaVersion");
    }

    [TestMethod]
    public void All_six_canonical_tool_actions_are_allowed()
    {
        var state = LoadFixture();
        var cases = new[]
        {
            ToolCandidate("query_branch_load", Arguments("{\"branch_id\":\"Aster-G3\"}")),
            ToolCandidate("query_heat_alarm"),
            ToolCandidate(
                "dry_run_reroute",
                Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}"),
                requiresDryRun: true),
            ToolCandidate("query_asset_status", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"), "maintenance_console"),
            ToolCandidate("query_backlog", toolId: "maintenance_console"),
            ToolCandidate(
                "dry_run_patch",
                Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"),
                "maintenance_console",
                true)
        };

        foreach (var candidate in cases)
        {
            var decision = AsterL1ActionValidator.Validate(state, candidate);
            Assert.AreEqual(AsterL1DecisionStatus.Allowed, decision.Status, candidate.Action);
            Assert.IsEmpty(decision.Issues, candidate.Action);
        }
    }

    [TestMethod]
    public void Tool_argument_policy_is_fail_closed_and_ordinal()
    {
        var state = LoadFixture();
        var cases = new[]
        {
            ("required_arguments_missing", ToolCandidate("query_branch_load"), AsterL1DecisionStatus.Denied, new[] { "tool_arguments_missing:Arguments" }),
            ("unknown_argument", ToolCandidate("query_heat_alarm", Arguments("{\"extra\":true}")), AsterL1DecisionStatus.Unsupported, new[] { "tool_argument_unknown:extra" }),
            ("undefined_argument", ToolCandidate("query_heat_alarm", new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["bad"] = default }), AsterL1DecisionStatus.Invalid, new[] { "tool_arguments_invalid:Arguments" }),
            ("forbidden_argument", ToolCandidate("query_branch_load", Arguments("{\"world_state\":{},\"branch_id\":\"Aster-G3\"}")), AsterL1DecisionStatus.Denied, new[] { "tool_macro_surface_denied:world_state" }),
            ("wrong_argument_type", ToolCandidate("query_branch_load", Arguments("{\"branch_id\":3}")), AsterL1DecisionStatus.Invalid, new[] { "tool_argument_type_invalid:branch_id" }),
            ("noncanonical_argument", ToolCandidate("query_branch_load", Arguments("{\"branch_id\":\"aster-g3\"}")), AsterL1DecisionStatus.Unsupported, new[] { "tool_argument_value_unsupported:branch_id" }),
            ("invalid_reroute_fraction", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, new[] { "tool_argument_value_invalid:load_fraction" }),
            ("macro_reroute_fraction", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.21}"), requiresDryRun: true), AsterL1DecisionStatus.Denied, new[] { "tool_macro_surface_denied:load_fraction" }),
            ("dry_run_flag_conflict", ToolCandidate("dry_run_patch", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"), "maintenance_console", false), AsterL1DecisionStatus.Denied, new[] { "tool_requires_dry_run_conflict:RequiresDryRun" }),
            ("execute_mode", ToolCandidate("query_heat_alarm") with { Mode = "execute" }, AsterL1DecisionStatus.Denied, new[] { "tool_execute_denied:Mode" }),
            ("invalid_mode", ToolCandidate("query_heat_alarm") with { Mode = "DRY_RUN" }, AsterL1DecisionStatus.Invalid, new[] { "tool_mode_invalid:Mode" }),
            ("case_variant_action", ToolCandidate("Query_Heat_Alarm"), AsterL1DecisionStatus.Denied, new[] { "tool_action_not_supported:Action", "tool_action_outside_family:Action" })
        };

        foreach (var (name, candidate, status, expectedIssues) in cases)
        {
            var decision = AsterL1ActionValidator.Validate(state, candidate);
            AssertDecisionExact(name, decision, status, expectedIssues);
        }
    }

    [TestMethod]
    public void Required_tool_argument_paths_are_complete_and_exact()
    {
        var state = LoadFixture();
        var cases = new (string Name, AsterL1ToolActionCandidate Candidate, AsterL1DecisionStatus Status, string[] Issues)[]
        {
            ("branch_id_missing", ToolCandidate("query_branch_load", Arguments("{}")), AsterL1DecisionStatus.Denied, ["tool_arguments_missing:branch_id"]),
            ("branch_id_type", ToolCandidate("query_branch_load", Arguments("{\"branch_id\":3}")), AsterL1DecisionStatus.Invalid, ["tool_argument_type_invalid:branch_id"]),
            ("branch_id_blank", ToolCandidate("query_branch_load", Arguments("{\"branch_id\":\" \"}")), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:branch_id"]),
            ("branch_id_unsupported", ToolCandidate("query_branch_load", Arguments("{\"branch_id\":\"aster-g3\"}")), AsterL1DecisionStatus.Unsupported, ["tool_argument_value_unsupported:branch_id"]),
            ("reroute_from_branch_missing", ToolCandidate("dry_run_reroute", Arguments("{\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Denied, ["tool_arguments_missing:from_branch"]),
            ("reroute_to_branch_missing", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Denied, ["tool_arguments_missing:to_branch"]),
            ("reroute_load_fraction_missing", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\"}"), requiresDryRun: true), AsterL1DecisionStatus.Denied, ["tool_arguments_missing:load_fraction"]),
            ("reroute_from_branch_type", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":3,\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_type_invalid:from_branch"]),
            ("reroute_to_branch_type", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":3,\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_type_invalid:to_branch"]),
            ("reroute_load_fraction_type", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":\"0.20\"}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_type_invalid:load_fraction"]),
            ("reroute_from_branch_blank", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\" \u0020\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:from_branch"]),
            ("reroute_to_branch_blank", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\" \u0020\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:to_branch"]),
            ("reroute_from_branch_unsupported", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G4\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Unsupported, ["tool_argument_value_unsupported:from_branch"]),
            ("reroute_to_branch_unsupported", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G3\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Unsupported, ["tool_argument_value_unsupported:to_branch"]),
            ("reroute_load_fraction_nonpositive", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:load_fraction"]),
            ("reroute_load_fraction_finite_negative", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":-0.01}"), requiresDryRun: true), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:load_fraction"]),
            ("reroute_load_fraction_above_boundary", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.21}"), requiresDryRun: true), AsterL1DecisionStatus.Denied, ["tool_macro_surface_denied:load_fraction"]),
            ("reroute_load_fraction_boundary", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}"), requiresDryRun: true), AsterL1DecisionStatus.Allowed, []),
            ("asset_status_asset_id_missing", ToolCandidate("query_asset_status", Arguments("{}"), "maintenance_console"), AsterL1DecisionStatus.Denied, ["tool_arguments_missing:asset_id"]),
            ("asset_status_asset_id_type", ToolCandidate("query_asset_status", Arguments("{\"asset_id\":3}"), "maintenance_console"), AsterL1DecisionStatus.Invalid, ["tool_argument_type_invalid:asset_id"]),
            ("asset_status_asset_id_blank", ToolCandidate("query_asset_status", Arguments("{\"asset_id\":\" \"}"), "maintenance_console"), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:asset_id"]),
            ("asset_status_asset_id_unsupported", ToolCandidate("query_asset_status", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-8\"}"), "maintenance_console"), AsterL1DecisionStatus.Unsupported, ["tool_argument_value_unsupported:asset_id"]),
            ("asset_status_asset_id_canonical", ToolCandidate("query_asset_status", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"), "maintenance_console"), AsterL1DecisionStatus.Allowed, []),
            ("dry_run_patch_asset_id_missing", ToolCandidate("dry_run_patch", Arguments("{}"), "maintenance_console", true), AsterL1DecisionStatus.Denied, ["tool_arguments_missing:asset_id"]),
            ("dry_run_patch_asset_id_type", ToolCandidate("dry_run_patch", Arguments("{\"asset_id\":3}"), "maintenance_console", true), AsterL1DecisionStatus.Invalid, ["tool_argument_type_invalid:asset_id"]),
            ("dry_run_patch_asset_id_blank", ToolCandidate("dry_run_patch", Arguments("{\"asset_id\":\" \"}"), "maintenance_console", true), AsterL1DecisionStatus.Invalid, ["tool_argument_value_invalid:asset_id"]),
            ("dry_run_patch_asset_id_unsupported", ToolCandidate("dry_run_patch", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-8\"}"), "maintenance_console", true), AsterL1DecisionStatus.Unsupported, ["tool_argument_value_unsupported:asset_id"]),
            ("dry_run_patch_asset_id_canonical", ToolCandidate("dry_run_patch", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"), "maintenance_console", true), AsterL1DecisionStatus.Allowed, [])
        };

        foreach (var (name, candidate, status, expectedIssues) in cases)
        {
            AssertDecisionExact(
                name,
                AsterL1ActionValidator.Validate(state, candidate),
                status,
                expectedIssues);
        }
    }

    [TestMethod]
    public void Tool_resolution_and_actor_authority_fail_closed()
    {
        var state = LoadFixture();

        AssertDecision(
            state,
            ToolCandidate("query_heat_alarm") with { IssuerId = "UNKNOWN" },
            AsterL1DecisionStatus.Denied,
            AsterL1IssueCatalog.IssuerNotFound);
        AssertDecision(
            state with { Actors = state.Actors.Append(state.Actors.Single(actor => actor.ActorId == "A1")).ToArray() },
            ToolCandidate("query_heat_alarm"),
            AsterL1DecisionStatus.Invalid,
            AsterL1IssueCatalog.IssuerAmbiguous);
        AssertDecision(
            state,
            ToolCandidate("query_heat_alarm") with { IssuerLayer = "L2" },
            AsterL1DecisionStatus.Denied,
            "issuer_layer_mismatch:IssuerLayer",
            "issuer_layer_denied:IssuerLayer");
        AssertDecision(
            state,
            ToolCandidate("query_heat_alarm") with { ToolId = "UNKNOWN" },
            AsterL1DecisionStatus.Denied,
            AsterL1IssueCatalog.ToolNotFound);
        AssertDecision(
            state with { Tools = state.Tools.Append(state.Tools[0]).ToArray() },
            ToolCandidate("query_heat_alarm"),
            AsterL1DecisionStatus.Invalid,
            AsterL1IssueCatalog.ToolAmbiguous);
        AssertDecision(
            state with
            {
                Actors = state.Actors.Select(actor => actor.ActorId == "A1" ? actor with { ToolRefs = [] } : actor).ToArray()
            },
            ToolCandidate("query_heat_alarm"),
            AsterL1DecisionStatus.Denied,
            AsterL1IssueCatalog.ToolNotReferenced);
        AssertDecision(
            state with { Tools = state.Tools.Select(tool => tool.ToolId == "local_grid_panel" ? tool with { Status = "unavailable" } : tool).ToArray() },
            ToolCandidate("query_heat_alarm"),
            AsterL1DecisionStatus.Denied,
            AsterL1IssueCatalog.ToolUnavailable);
        AssertDecision(
            state with
            {
                Tools = state.Tools.Select(tool => tool.ToolId == "local_grid_panel"
                    ? tool with { SupportedActions = tool.SupportedActions.Append("custom_action").ToArray() }
                    : tool).ToArray()
            },
            ToolCandidate("custom_action"),
            AsterL1DecisionStatus.Unsupported,
            AsterL1IssueCatalog.ToolActionOutsideFamily);
        AssertDecision(
            state with
            {
                Actors = state.Actors.Select(actor => actor.ActorId == "A1"
                    ? actor with { HomeSectorId = "Vireo" }
                    : actor).ToArray()
            },
            ToolCandidate("query_heat_alarm"),
            AsterL1DecisionStatus.Unsupported,
            AsterL1IssueCatalog.IssuerSectorUnsupported);
    }

    [TestMethod]
    public void Unresolved_identities_suppress_later_policy_findings_exactly()
    {
        var state = LoadFixture();
        var lateToolDefects = ToolCandidate(
            "custom_action",
            Arguments("{\"world_state\":{},\"other\":true}"),
            "UNKNOWN") with
        {
            Mode = "execute"
        };

        AssertDecisionExact(
            "issuer_not_found_suppresses_later_tool_defects",
            AsterL1ActionValidator.Validate(state, lateToolDefects with { IssuerId = "UNKNOWN" }),
            AsterL1DecisionStatus.Denied,
            "issuer_not_found:IssuerId");
        AssertDecisionExact(
            "issuer_ambiguous_suppresses_later_tool_defects",
            AsterL1ActionValidator.Validate(
                state with { Actors = state.Actors.Append(state.Actors.Single(actor => actor.ActorId == "A1")).ToArray() },
                lateToolDefects),
            AsterL1DecisionStatus.Invalid,
            "issuer_ambiguous:IssuerId");
        AssertDecisionExact(
            "tool_not_found_suppresses_later_tool_defects",
            AsterL1ActionValidator.Validate(state, lateToolDefects),
            AsterL1DecisionStatus.Denied,
            "tool_not_found:ToolId");

        var ambiguousToolState = state with
        {
            Tools = state.Tools.Append(state.Tools.Single(tool => tool.ToolId == "local_grid_panel")).ToArray()
        };
        AssertDecisionExact(
            "tool_ambiguous_suppresses_later_tool_defects",
            AsterL1ActionValidator.Validate(
                ambiguousToolState,
                lateToolDefects with { ToolId = "local_grid_panel" }),
            AsterL1DecisionStatus.Invalid,
            "tool_ambiguous:ToolId");

        var lateWorkOrderDefects = WorkOrderCandidate() with
        {
            TargetCrewId = "UNKNOWN",
            TargetLocation = "Aster/Grid-Spine-04",
            TaskType = "repair_now",
            AuthorizedResources = ["one_spare_coupler"]
        };
        AssertDecisionExact(
            "crew_not_found_suppresses_later_work_order_defects",
            AsterL1ActionValidator.Validate(state, lateWorkOrderDefects),
            AsterL1DecisionStatus.Denied,
            "crew_not_found:TargetCrewId");
        AssertDecisionExact(
            "crew_ambiguous_suppresses_later_work_order_defects",
            AsterL1ActionValidator.Validate(
                state with { Crews = state.Crews.Append(state.Crews[0]).ToArray() },
                lateWorkOrderDefects with { TargetCrewId = "crew_aster_repair_02" }),
            AsterL1DecisionStatus.Invalid,
            "crew_ambiguous:TargetCrewId");
    }

    [TestMethod]
    public void Mixed_tool_policy_findings_preserve_denied_precedence()
    {
        var state = LoadFixture();
        state = state with
        {
            Tools = state.Tools.Select(tool => tool.ToolId == "maintenance_console"
                ? tool with { SupportedActions = tool.SupportedActions.Append("query_branch_load").ToArray() }
                : tool).ToArray()
        };
        var candidate = ToolCandidate(
            "query_branch_load",
            Arguments("{\"branch_id\":\"Aster-G3\",\"world_state\":{}}"),
            "maintenance_console") with
        {
            Mode = "execute"
        };

        var decision = AsterL1ActionValidator.Validate(state, candidate);

        AssertDecisionExact(
            "wrong_tool_execute_macro",
            decision,
            AsterL1DecisionStatus.Denied,
            "tool_action_outside_family:Action",
            "tool_execute_denied:Mode",
            "tool_macro_surface_denied:world_state");

        state = LoadFixture();
        state = state with
        {
            Tools = state.Tools.Select(tool => tool.ToolId == "local_grid_panel"
                ? tool with { SupportedActions = tool.SupportedActions.Append("custom_action").ToArray() }
                : tool).ToArray()
        };
        var noRuleDecision = AsterL1ActionValidator.Validate(
            state,
            ToolCandidate(
                "custom_action",
                Arguments("{\"world_state\":{},\"other\":true}")) with
            {
                Mode = "execute"
            });
        AssertDecisionExact(
            "no_rule_execute_macro_unknown",
            noRuleDecision,
            AsterL1DecisionStatus.Denied,
            "tool_action_outside_family:Action",
            "tool_execute_denied:Mode",
            "tool_macro_surface_denied:world_state",
            "tool_argument_unknown:other");

        state = LoadFixture();
        var notAdvertisedNoRuleDecision = AsterL1ActionValidator.Validate(
            state,
            ToolCandidate(
                "custom_action",
                Arguments("{\"world_state\":{},\"other\":true}")) with
            {
                Mode = "execute"
            });
        AssertDecisionExact(
            "not_advertised_no_rule_execute_macro_unknown",
            notAdvertisedNoRuleDecision,
            AsterL1DecisionStatus.Denied,
            "tool_action_not_supported:Action",
            "tool_action_outside_family:Action",
            "tool_execute_denied:Mode",
            "tool_macro_surface_denied:world_state",
            "tool_argument_unknown:other");

        var notAdvertisedWrongToolDecision = AsterL1ActionValidator.Validate(
            state,
            ToolCandidate(
                "query_branch_load",
                Arguments("{\"branch_id\":\"Aster-G3\",\"world_state\":{}}"),
                "maintenance_console") with
            {
                Mode = "execute"
            });
        AssertDecisionExact(
            "not_advertised_wrong_tool_execute_macro",
            notAdvertisedWrongToolDecision,
            AsterL1DecisionStatus.Denied,
            "tool_action_not_supported:Action",
            "tool_action_outside_family:Action",
            "tool_execute_denied:Mode",
            "tool_macro_surface_denied:world_state");
    }

    [TestMethod]
    public void Mode_and_requires_dry_run_matrix_is_frozen_for_all_actions()
    {
        var state = LoadFixture();
        var cases = new[]
        {
            ("query_branch_load", ToolCandidate("query_branch_load", Arguments("{\"branch_id\":\"Aster-G3\"}")), false),
            ("query_heat_alarm", ToolCandidate("query_heat_alarm", Arguments("{}")), false),
            ("dry_run_reroute", ToolCandidate("dry_run_reroute", Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}")), true),
            ("query_asset_status", ToolCandidate("query_asset_status", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"), "maintenance_console"), false),
            ("query_backlog", ToolCandidate("query_backlog", Arguments("{}"), "maintenance_console"), false),
            ("dry_run_patch", ToolCandidate("dry_run_patch", Arguments("{\"asset_id\":\"Aster/Grid-Spine-03/Coupler-7\"}"), "maintenance_console"), true)
        };

        foreach (var (name, candidate, rejectsFalseFlag) in cases)
        {
            AssertDecisionExact(
                $"{name}_dry_run_absent_flag",
                AsterL1ActionValidator.Validate(state, candidate),
                AsterL1DecisionStatus.Allowed);
            AssertDecisionExact(
                $"{name}_execute",
                AsterL1ActionValidator.Validate(state, candidate with { Mode = "execute" }),
                AsterL1DecisionStatus.Denied,
                "tool_execute_denied:Mode");
            AssertDecisionExact(
                $"{name}_invalid_mode",
                AsterL1ActionValidator.Validate(state, candidate with { Mode = "DRY_RUN" }),
                AsterL1DecisionStatus.Invalid,
                "tool_mode_invalid:Mode");
            AssertDecisionExact(
                $"{name}_true_flag",
                AsterL1ActionValidator.Validate(state, candidate with { RequiresDryRun = true }),
                AsterL1DecisionStatus.Allowed);
            AssertDecisionExact(
                $"{name}_false_flag",
                AsterL1ActionValidator.Validate(state, candidate with { RequiresDryRun = false }),
                rejectsFalseFlag ? AsterL1DecisionStatus.Denied : AsterL1DecisionStatus.Allowed,
                rejectsFalseFlag ? ["tool_requires_dry_run_conflict:RequiresDryRun"] : []);
        }
    }

    [TestMethod]
    public void Work_order_target_forms_are_explicit()
    {
        var state = LoadFixture();

        Assert.AreEqual(
            AsterL1DecisionStatus.Allowed,
            AsterL1ActionValidator.Validate(state, WorkOrderCandidate()).Status);
        AssertDecision(
            state,
            WorkOrderCandidate() with { TargetCrewId = null, TargetCrewPoolId = "aster-repair-pool" },
            AsterL1DecisionStatus.Unsupported,
            AsterL1IssueCatalog.CrewPoolUnsupported);
        AssertDecision(
            state,
            WorkOrderCandidate() with { TargetCrewPoolId = "aster-repair-pool" },
            AsterL1DecisionStatus.Denied,
            AsterL1IssueCatalog.WorkOrderTargetsConflict);
        AssertDecision(
            state,
            WorkOrderCandidate() with { TargetCrewId = null },
            AsterL1DecisionStatus.Invalid,
            AsterL1IssueCatalog.WorkOrderTargetMissing);
    }

    [TestMethod]
    public void Pool_and_conflicting_targets_collect_independent_policy_findings()
    {
        var state = LoadFixture();
        var baseCandidate = WorkOrderCandidate() with
        {
            VisibilityIntent = "internal",
            TargetLocation = "Aster/Grid-Spine-04",
            TaskType = "repair_now",
            RiskTolerance = "moderate",
            AuthorizedResources = ["one_spare_coupler"],
            Constraints = ["avoid shutdown"],
            ExpectedReport = "full_hidden_report",
            FallbackProtocol = "execute_patch",
            StealthLevel = "covert",
            PublicVisibility = "public"
        };

        var poolDecision = AsterL1ActionValidator.Validate(
            state,
            baseCandidate with { TargetCrewId = null, TargetCrewPoolId = "aster-repair-pool" });
        AssertDecisionExact(
            "pool_with_independent_policy",
            poolDecision,
            AsterL1DecisionStatus.Denied,
            "visibility_intent_unsupported:VisibilityIntent",
            "crew_pool_unsupported:TargetCrewPoolId",
            "work_order_location_unsupported:TargetLocation",
            "work_order_task_unsupported:TaskType",
            "work_order_risk_tolerance_unsupported:RiskTolerance",
            "work_order_resource_authority_denied:AuthorizedResources",
            "work_order_constraints_unsupported:Constraints",
            "work_order_expected_report_unsupported:ExpectedReport",
            "work_order_fallback_unsupported:FallbackProtocol",
            "work_order_stealth_denied:StealthLevel",
            "work_order_public_visibility_unsupported:PublicVisibility");

        var conflictDecision = AsterL1ActionValidator.Validate(
            state,
            baseCandidate with { TargetCrewPoolId = "aster-repair-pool" });
        AssertDecisionExact(
            "conflicting_targets_with_independent_policy",
            conflictDecision,
            AsterL1DecisionStatus.Denied,
            "visibility_intent_unsupported:VisibilityIntent",
            "work_order_targets_conflict:",
            "work_order_location_unsupported:TargetLocation",
            "work_order_task_unsupported:TaskType",
            "work_order_risk_tolerance_unsupported:RiskTolerance",
            "work_order_resource_authority_denied:AuthorizedResources",
            "work_order_constraints_unsupported:Constraints",
            "work_order_expected_report_unsupported:ExpectedReport",
            "work_order_fallback_unsupported:FallbackProtocol",
            "work_order_stealth_denied:StealthLevel",
            "work_order_public_visibility_unsupported:PublicVisibility");
    }

    [TestMethod]
    public void Work_order_crew_and_optional_authority_surfaces_fail_closed()
    {
        var state = LoadFixture();
        var cases = new[]
        {
            (state, WorkOrderCandidate() with { TargetCrewId = "UNKNOWN" }, AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.CrewNotFound),
            (state with { Crews = state.Crews.Append(state.Crews[0]).ToArray() }, WorkOrderCandidate(), AsterL1DecisionStatus.Invalid, AsterL1IssueCatalog.CrewAmbiguous),
            (state with { Actors = state.Actors.Select(actor => actor.ActorId == "A1" ? actor with { CrewRefs = [] } : actor).ToArray() }, WorkOrderCandidate(), AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.CrewNotReferenced),
            (state with { Crews = [state.Crews[0] with { Status = "unavailable" }] }, WorkOrderCandidate(), AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.CrewUnavailable),
            (state with { Crews = [state.Crews[0] with { AssignedActorId = "A2" }] }, WorkOrderCandidate(), AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.CrewAssignmentMismatch),
            (state with { Crews = [state.Crews[0] with { HomeSectorId = "Vireo" }] }, WorkOrderCandidate(), AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.CrewSectorMismatch),
            (state, WorkOrderCandidate() with { TargetLocation = "Aster/Grid-Spine-04" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderLocationUnsupported),
            (state, WorkOrderCandidate() with { TaskType = "repair_now" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderTaskUnsupported),
            (state, WorkOrderCandidate() with { RiskTolerance = "moderate" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderRiskToleranceUnsupported),
            (state, WorkOrderCandidate() with { AuthorizedResources = ["one_spare_coupler"] }, AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.WorkOrderResourceAuthorityDenied),
            (state, WorkOrderCandidate() with { Constraints = ["avoid shutdown"] }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderConstraintsUnsupported),
            (state, WorkOrderCandidate() with { ExpectedReport = "full_hidden_report" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderExpectedReportUnsupported),
            (state, WorkOrderCandidate() with { ExpectedReport = string.Empty }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderExpectedReportUnsupported),
            (state, WorkOrderCandidate() with { FallbackProtocol = "execute_patch" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderFallbackUnsupported),
            (state, WorkOrderCandidate() with { StealthLevel = "covert" }, AsterL1DecisionStatus.Denied, AsterL1IssueCatalog.WorkOrderStealthDenied),
            (state, WorkOrderCandidate() with { PublicVisibility = "public" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.WorkOrderPublicVisibilityUnsupported),
            (state, WorkOrderCandidate() with { VisibilityIntent = "internal" }, AsterL1DecisionStatus.Unsupported, AsterL1IssueCatalog.VisibilityIntentUnsupported)
        };

        foreach (var (candidateState, candidate, status, code) in cases)
        {
            AssertDecision(candidateState, candidate, status, code);
        }
    }

    [TestMethod]
    public void Empty_optional_work_order_collections_and_fallback_do_not_grant_or_block_authority()
    {
        var decision = AsterL1ActionValidator.Validate(
            LoadFixture(),
            WorkOrderCandidate() with
            {
                AuthorizedResources = [],
                Constraints = [],
                FallbackProtocol = string.Empty
            });

        Assert.AreEqual(AsterL1DecisionStatus.Allowed, decision.Status);
        Assert.IsEmpty(decision.Issues);
    }

    [TestMethod]
    public void Null_state_and_top_level_collections_fail_exactly_for_both_candidate_types()
    {
        var state = LoadFixture();
        var cases = new (string Name, WorldState? State)[]
        {
            ("null_world_state", null),
            ("null_sectors", state with { Sectors = null! }),
            ("null_actors", state with { Actors = null! }),
            ("null_crews", state with { Crews = null! }),
            ("null_tools", state with { Tools = null! })
        };

        foreach (var (name, candidateState) in cases)
        {
            AssertDecisionExact(
                $"tool_{name}",
                AsterL1ActionValidator.Validate(candidateState, ToolCandidate("query_heat_alarm")),
                AsterL1DecisionStatus.Invalid,
                "invalid_world_state:");
            AssertDecisionExact(
                $"work_order_{name}",
                AsterL1ActionValidator.Validate(candidateState, WorkOrderCandidate()),
                AsterL1DecisionStatus.Invalid,
                "invalid_world_state:");
        }
    }

    [TestMethod]
    public void Malformed_runtime_record_fails_as_invalid_world_state()
    {
        var state = LoadFixture();
        state = state with
        {
            Tools = [state.Tools[0] with { SupportedActions = null! }, state.Tools[1]]
        };

        AssertDecision(
            state,
            ToolCandidate("query_heat_alarm"),
            AsterL1DecisionStatus.Invalid,
            AsterL1IssueCatalog.InvalidWorldState);
    }

    [TestMethod]
    public void Counterfeit_authority_states_fail_with_only_invalid_world_state()
    {
        var cases = new (string Name, Func<WorldState, WorldState> Mutate)[]
        {
            ("empty_sectors", state => state with { Sectors = [] }),
            ("empty_actors", state => state with { Actors = [] }),
            ("empty_crews", state => state with { Crews = [] }),
            ("empty_tools", state => state with { Tools = [] }),
            ("missing_aster", state => state with { Sectors = state.Sectors.Where(sector => sector.SectorId != "Aster").ToArray() }),
            ("duplicate_aster", state => state with { Sectors = state.Sectors.Append(state.Sectors[0]).ToArray() }),
            ("null_sector", state => state with { Sectors = [null!, .. state.Sectors] }),
            ("null_sector_systems", state => state with { Sectors = ReplaceAster(state, sector => sector with { Systems = null! }) }),
            ("null_system", state => state with { Sectors = ReplaceAster(state, sector => sector with { Systems = [null!, .. sector.Systems] }) }),
            ("blank_sector_id", state => state with { Sectors = ReplaceAster(state, sector => sector with { SectorId = " " }) }),
            ("blank_system_id", state => state with { Sectors = ReplaceAster(state, sector => sector with { Systems = [sector.Systems[0] with { SystemId = " " }, .. sector.Systems.Skip(1)] }) }),
            ("duplicate_system_id", state => state with { Sectors = ReplaceAster(state, sector => sector with { Systems = [sector.Systems[0], sector.Systems[1] with { SystemId = sector.Systems[0].SystemId }, .. sector.Systems.Skip(2)] }) }),
            ("null_actor", state => state with { Actors = [null!, .. state.Actors] }),
            ("null_crew", state => state with { Crews = [null!, .. state.Crews] }),
            ("null_tool", state => state with { Tools = [null!, .. state.Tools] }),
            ("null_actor_system_refs", state => state with { Actors = ReplaceA1(state, actor => actor with { SystemRefs = null! }) }),
            ("null_actor_tool_refs", state => state with { Actors = ReplaceA1(state, actor => actor with { ToolRefs = null! }) }),
            ("null_actor_crew_refs", state => state with { Actors = ReplaceA1(state, actor => actor with { CrewRefs = null! }) }),
            ("unknown_actor_system_ref", state => state with { Actors = ReplaceA1(state, actor => actor with { SystemRefs = [.. actor.SystemRefs, "UNKNOWN"] }) }),
            ("duplicate_actor_system_ref", state => state with { Actors = ReplaceA1(state, actor => actor with { SystemRefs = [.. actor.SystemRefs, actor.SystemRefs[0]] }) }),
            ("duplicate_actor_tool_ref", state => state with { Actors = ReplaceA1(state, actor => actor with { ToolRefs = [.. actor.ToolRefs, actor.ToolRefs[0]] }) }),
            ("unknown_actor_tool_ref", state => state with { Actors = ReplaceA1(state, actor => actor with { ToolRefs = [.. actor.ToolRefs, "UNKNOWN"] }) }),
            ("unknown_actor_crew_ref", state => state with { Actors = ReplaceA1(state, actor => actor with { CrewRefs = [.. actor.CrewRefs, "UNKNOWN"] }) }),
            ("duplicate_actor_crew_ref", state => state with { Actors = ReplaceA1(state, actor => actor with { CrewRefs = [.. actor.CrewRefs, actor.CrewRefs[0]] }) }),
            ("unknown_actor_home", state => state with { Actors = ReplaceA1(state, actor => actor with { HomeSectorId = "UNKNOWN" }) }),
            ("invalid_crew_status", state => state with { Crews = [state.Crews[0] with { Status = "assigned" }] }),
            ("null_crew_system_refs", state => state with { Crews = [state.Crews[0] with { SystemRefs = null! }] }),
            ("empty_crew_system_refs", state => state with { Crews = [state.Crews[0] with { SystemRefs = [] }] }),
            ("unknown_crew_system_ref", state => state with { Crews = [state.Crews[0] with { SystemRefs = [.. state.Crews[0].SystemRefs, "UNKNOWN"] }] }),
            ("duplicate_crew_system_ref", state => state with { Crews = [state.Crews[0] with { SystemRefs = [.. state.Crews[0].SystemRefs, state.Crews[0].SystemRefs[0]] }] }),
            ("unknown_crew_assignment", state => state with { Crews = [state.Crews[0] with { AssignedActorId = "UNKNOWN" }] }),
            ("unknown_crew_home", state => state with { Crews = [state.Crews[0] with { HomeSectorId = "UNKNOWN" }] }),
            ("invalid_tool_status", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { Status = "online" }) }),
            ("null_tool_system_refs", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SystemRefs = null! }) }),
            ("empty_tool_system_refs", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SystemRefs = [] }) }),
            ("unknown_tool_system_ref", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SystemRefs = [.. tool.SystemRefs, "UNKNOWN"] }) }),
            ("duplicate_tool_system_ref", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SystemRefs = [.. tool.SystemRefs, tool.SystemRefs[0]] }) }),
            ("null_supported_actions", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SupportedActions = null! }) }),
            ("empty_supported_actions", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SupportedActions = [] }) }),
            ("blank_supported_action", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SupportedActions = [.. tool.SupportedActions, " "] }) }),
            ("duplicate_supported_action", state => state with { Tools = ReplaceTool(state, "local_grid_panel", tool => tool with { SupportedActions = [.. tool.SupportedActions, tool.SupportedActions[0]] }) })
        };

        foreach (var (name, mutate) in cases)
        {
            var decision = AsterL1ActionValidator.Validate(mutate(LoadFixture()), ToolCandidate("query_heat_alarm"));
            AssertDecisionExact(
                name,
                decision,
                AsterL1DecisionStatus.Invalid,
                "invalid_world_state:");
        }
    }

    [TestMethod]
    public void Validation_does_not_mutate_world_state()
    {
        var state = LoadFixture();
        var before = WorldStateJsonSerializer.Serialize(state);

        _ = AsterL1ActionValidator.Validate(
            state,
            ToolCandidate(
                "dry_run_reroute",
                Arguments("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.18}"),
                requiresDryRun: true));
        _ = AsterL1ActionValidator.Validate(state, WorkOrderCandidate());

        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
    }

    private static AsterL1ToolActionCandidate ToolCandidate(
        string action,
        IReadOnlyDictionary<string, JsonElement>? arguments = null,
        string toolId = "local_grid_panel",
        bool? requiresDryRun = null)
    {
        return new AsterL1ToolActionCandidate
        {
            PacketId = "pkt-tool-001",
            PacketType = "ToolActionRequest",
            SchemaVersion = "0.1",
            IssuerId = "A1",
            IssuerLayer = "L1",
            IssuedAtTick = 1,
            SourceContextId = "ctx-aster-a1",
            ToolId = toolId,
            Action = action,
            Arguments = arguments,
            Mode = "dry_run",
            RequiresDryRun = requiresDryRun
        };
    }

    private static AsterL1WorkOrderCandidate WorkOrderCandidate()
    {
        return new AsterL1WorkOrderCandidate
        {
            PacketId = "pkt-work-001",
            PacketType = "WorkOrderRequest",
            SchemaVersion = "0.1",
            IssuerId = "A1",
            IssuerLayer = "L1",
            IssuedAtTick = 1,
            SourceContextId = "ctx-aster-a1",
            TargetCrewId = "crew_aster_repair_02",
            TargetLocation = "Aster/Grid-Spine-03",
            TaskType = "inspect_and_patch",
            Priority = "high",
            ExpectedReport = "short_structured",
            StealthLevel = "normal"
        };
    }

    private static IReadOnlyDictionary<string, JsonElement> Arguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
    }

    private static void AssertDecision(
        WorldState state,
        AsterL1ToolActionCandidate candidate,
        AsterL1DecisionStatus expectedStatus,
        params string[] expectedIssues)
    {
        var decision = AsterL1ActionValidator.Validate(state, candidate);
        AssertDecisionExact(
            expectedIssues[0],
            decision,
            expectedStatus,
            NormalizeExpectedIssues(expectedIssues));
    }

    private static void AssertDecision(
        WorldState state,
        AsterL1WorkOrderCandidate candidate,
        AsterL1DecisionStatus expectedStatus,
        params string[] expectedIssues)
    {
        var decision = AsterL1ActionValidator.Validate(state, candidate);
        AssertDecisionExact(
            expectedIssues[0],
            decision,
            expectedStatus,
            NormalizeExpectedIssues(expectedIssues));
    }

    private static void AssertDecisionExact(
        string name,
        AsterL1ActionDecision decision,
        AsterL1DecisionStatus expectedStatus,
        params string[] expectedIssues)
    {
        Assert.AreEqual(expectedStatus, decision.Status, name);
        CollectionAssert.AreEqual(
            expectedIssues,
            decision.Issues.Select(issue => $"{issue.Code}:{issue.Field}").ToArray(),
            name);
    }

    private static string[] NormalizeExpectedIssues(IEnumerable<string> expectedIssues)
    {
        return expectedIssues.Select(issue => issue.Contains(':', StringComparison.Ordinal)
            ? issue
            : $"{issue}:{ExpectedField(issue)}").ToArray();
    }

    private static string? ExpectedField(string code)
    {
        return code switch
        {
            AsterL1IssueCatalog.InvalidWorldState => null,
            AsterL1IssueCatalog.IssuerNotFound or AsterL1IssueCatalog.IssuerAmbiguous => "IssuerId",
            AsterL1IssueCatalog.IssuerSectorUnsupported => "IssuerId",
            AsterL1IssueCatalog.VisibilityIntentUnsupported => "VisibilityIntent",
            AsterL1IssueCatalog.ToolNotFound or AsterL1IssueCatalog.ToolAmbiguous
                or AsterL1IssueCatalog.ToolNotReferenced => "ToolId",
            AsterL1IssueCatalog.ToolUnavailable or AsterL1IssueCatalog.CrewUnavailable => "Status",
            AsterL1IssueCatalog.ToolActionOutsideFamily => "Action",
            AsterL1IssueCatalog.WorkOrderTargetMissing or AsterL1IssueCatalog.WorkOrderTargetsConflict => null,
            AsterL1IssueCatalog.CrewPoolUnsupported => "TargetCrewPoolId",
            AsterL1IssueCatalog.CrewNotFound or AsterL1IssueCatalog.CrewAmbiguous
                or AsterL1IssueCatalog.CrewNotReferenced => "TargetCrewId",
            AsterL1IssueCatalog.CrewAssignmentMismatch => "AssignedActorId",
            AsterL1IssueCatalog.CrewSectorMismatch => "HomeSectorId",
            AsterL1IssueCatalog.WorkOrderLocationUnsupported => "TargetLocation",
            AsterL1IssueCatalog.WorkOrderTaskUnsupported => "TaskType",
            AsterL1IssueCatalog.WorkOrderRiskToleranceUnsupported => "RiskTolerance",
            AsterL1IssueCatalog.WorkOrderResourceAuthorityDenied => "AuthorizedResources",
            AsterL1IssueCatalog.WorkOrderConstraintsUnsupported => "Constraints",
            AsterL1IssueCatalog.WorkOrderExpectedReportUnsupported => "ExpectedReport",
            AsterL1IssueCatalog.WorkOrderFallbackUnsupported => "FallbackProtocol",
            AsterL1IssueCatalog.WorkOrderStealthDenied => "StealthLevel",
            AsterL1IssueCatalog.WorkOrderPublicVisibilityUnsupported => "PublicVisibility",
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Test field mapping is missing.")
        };
    }

    private static IReadOnlyList<SectorState> ReplaceAster(
        WorldState state,
        Func<SectorState, SectorState> replace)
    {
        return state.Sectors.Select(sector => sector.SectorId == "Aster" ? replace(sector) : sector).ToArray();
    }

    private static IReadOnlyList<ActorState> ReplaceA1(
        WorldState state,
        Func<ActorState, ActorState> replace)
    {
        return state.Actors.Select(actor => actor.ActorId == "A1" ? replace(actor) : actor).ToArray();
    }

    private static IReadOnlyList<ToolState> ReplaceTool(
        WorldState state,
        string toolId,
        Func<ToolState, ToolState> replace)
    {
        return state.Tools.Select(tool => tool.ToolId == toolId ? replace(tool) : tool).ToArray();
    }

    private static void AssertProperties<T>(params string[] expected)
    {
        var actual = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(sortedExpected, actual);
    }

    private static WorldState LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "aster-minimal-world-state.json");
        return InitialStateLoader.LoadFromJson(File.ReadAllText(path));
    }
}
