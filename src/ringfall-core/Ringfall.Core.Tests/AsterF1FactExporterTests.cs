using System.Reflection;
using Ringfall.Core.Actions;
using Ringfall.Core.Formal;
using Ringfall.Core.Scenarios;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class AsterF1FactExporterTests
{
    [TestMethod]
    public void Fact_set_contract_is_complete_and_has_no_hidden_content_fields()
    {
        var actual = typeof(AsterF1FactSet).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var expected = new[]
        {
            "CandidateAction", "CandidateMode", "CandidateTargetLocation", "CandidateTaskType",
            "CoreIssueCodes", "CoreStatus", "CoverageClassifications", "CrewAssignedActorId",
            "CrewHomeSectorId", "CrewId", "CrewStatus", "CrewSystemRefs", "FamilyId", "FamilyVersion",
            "IssuerId", "IssuerLayer", "IssuerSectorId", "PacketId", "PacketKind", "SourceContextId",
            "ToolId", "ToolStatus", "ToolSupportedActions", "ToolSystemRefs"
        };

        CollectionAssert.AreEqual(expected, actual);
        foreach (var forbidden in new[]
        {
            "Metrics", "Observations", "Rationale", "Confidence", "EvidenceRefs", "Arguments",
            "AuthorizedResources", "Constraints", "VisibilityIntent", "HiddenEffects"
        })
        {
            Assert.IsFalse(actual.Contains(forbidden, StringComparer.Ordinal), forbidden);
        }
    }

    [TestMethod]
    public void Tool_fact_export_is_minimal_ordered_and_copied()
    {
        var state = LoadFixture();
        var supportedActions = new List<string>(state.Tools[0].SupportedActions);
        var systemRefs = new List<string>(state.Tools[0].SystemRefs);
        state = state with
        {
            Tools = [state.Tools[0] with { SupportedActions = supportedActions, SystemRefs = systemRefs }, state.Tools[1]]
        };
        var before = WorldStateJsonSerializer.Serialize(state);
        var candidate = ToolCandidate();

        var facts = AsterF1FactExporter.Export(state, candidate);
        supportedActions.Add("mutated_after_export");
        systemRefs.Clear();

        Assert.AreEqual("aster-l1-action-work-order", facts.FamilyId);
        Assert.AreEqual("0.1", facts.FamilyVersion);
        Assert.AreEqual("ctx-aster-a1", facts.SourceContextId);
        Assert.AreEqual("A1", facts.IssuerId);
        Assert.AreEqual("Aster", facts.IssuerSectorId);
        Assert.AreEqual("local_grid_panel", facts.ToolId);
        Assert.AreEqual("available", facts.ToolStatus);
        CollectionAssert.AreEqual(new[] { "R2", "R5" }, facts.ToolSystemRefs.ToArray());
        CollectionAssert.AreEqual(
            new[] { "query_branch_load", "query_heat_alarm", "dry_run_reroute" },
            facts.ToolSupportedActions.ToArray());
        CollectionAssert.AreEqual(
            new[] { "schema_only", "guarded_by_core_validator", "unsupported" },
            facts.CoverageClassifications.ToArray());
        Assert.IsFalse(facts.CoverageClassifications.Contains("proved_by_refinery", StringComparer.Ordinal));
        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state with
        {
            Tools = [state.Tools[0] with { SupportedActions = supportedActions[..3], SystemRefs = ["R2", "R5"] }, state.Tools[1]]
        }));
    }

    [TestMethod]
    public void Work_order_fact_export_preserves_only_bounded_relationships()
    {
        var state = LoadFixture();
        var before = WorldStateJsonSerializer.Serialize(state);
        var candidate = WorkOrderCandidate() with
        {
            Rationale = "thermalDebt should never be exported",
            EvidenceRefs = ["secret-evidence"],
            AuthorizedResources = ["one_spare_coupler"],
            Constraints = ["hidden constraint"]
        };

        var facts = AsterF1FactExporter.Export(state, candidate);

        Assert.AreEqual("crew_aster_repair_02", facts.CrewId);
        Assert.AreEqual("available", facts.CrewStatus);
        Assert.AreEqual("A1", facts.CrewAssignedActorId);
        Assert.AreEqual("Aster", facts.CrewHomeSectorId);
        CollectionAssert.AreEqual(new[] { "R2", "R5", "R6" }, facts.CrewSystemRefs.ToArray());
        Assert.AreEqual("inspect_and_patch", facts.CandidateTaskType);
        Assert.AreEqual("Aster/Grid-Spine-03", facts.CandidateTargetLocation);
        CollectionAssert.AreEqual(
            new[] { "schema_only", "guarded_by_core_validator", "unsupported", "observability_only" },
            facts.CoverageClassifications.ToArray());
        Assert.AreEqual(AsterL1DecisionStatus.Denied, facts.CoreStatus);
        CollectionAssert.Contains(facts.CoreIssueCodes.ToArray(), AsterL1IssueCatalog.WorkOrderResourceAuthorityDenied);
        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
    }

    [TestMethod]
    public void Ambiguous_targets_export_no_arbitrary_runtime_record()
    {
        var state = LoadFixture();
        state = state with { Tools = state.Tools.Append(state.Tools[0]).ToArray() };

        var facts = AsterF1FactExporter.Export(state, ToolCandidate());

        Assert.IsNull(facts.ToolStatus);
        Assert.IsEmpty(facts.ToolSystemRefs);
        Assert.IsEmpty(facts.ToolSupportedActions);
        Assert.AreEqual(AsterL1DecisionStatus.Invalid, facts.CoreStatus);
        CollectionAssert.Contains(facts.CoreIssueCodes.ToArray(), AsterL1IssueCatalog.ToolAmbiguous);
    }

    private static AsterL1ToolActionCandidate ToolCandidate()
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
            ToolId = "local_grid_panel",
            Action = "query_heat_alarm",
            Mode = "dry_run"
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
            ExpectedReport = "short_structured"
        };
    }

    private static WorldState LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "aster-minimal-world-state.json");
        return InitialStateLoader.LoadFromJson(File.ReadAllText(path));
    }
}
