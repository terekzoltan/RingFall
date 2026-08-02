using Ringfall.Core.Scenarios;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class InitialStateLoaderTests
{
    [TestMethod]
    public void Loads_minimal_world_state_fixture()
    {
        var worldState = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));

        var aster = worldState.Sectors.Single(sector => sector.SectorId == "Aster");
        var asterSystems = aster.Systems.Select(system => system.SystemId).ToArray();
        var actorIds = worldState.Actors.Select(actor => actor.ActorId).ToArray();
        var sectorIds = worldState.Sectors.Select(sector => sector.SectorId).ToArray();

        CollectionAssert.IsSubsetOf(new[] { "R2", "R5", "R6", "R10" }, asterSystems);
        CollectionAssert.IsSubsetOf(new[] { "A1", "A2", "A4", "A6", "A9" }, actorIds);
        CollectionAssert.IsSubsetOf(new[] { "Vireo", "Morrow", "BlackSeam" }, sectorIds);
        Assert.IsTrue(worldState.Crews.Any(crew => crew.CrewId == "crew_aster_repair_02"));
    }

    [TestMethod]
    public void Missing_required_aster_system_fails_deterministically()
    {
        var root = ParseFixtureNode();
        var systems = root["sectors"]!.AsArray()[0]!["systems"]!.AsArray();
        var r2 = systems.Single(system => system!["systemId"]!.GetValue<string>() == "R2");
        systems.Remove(r2);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("Aster system R2 is required.", exception.Message);
    }

    [TestMethod]
    public void Duplicate_system_ids_fail_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"systemId\": \"VIREO-STUB\"", "\"systemId\": \"R2\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("system ids must be globally unique.", exception.Message);
    }

    [TestMethod]
    public void Unknown_crew_assigned_actor_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"assignedActorId\": \"A1\"", "\"assignedActorId\": \"UNKNOWN_ACTOR\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("crew crew_aster_repair_02 assigned actor must exist.", exception.Message);
    }

    [TestMethod]
    public void Missing_required_actor_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"actorId\": \"A1\"", "\"actorId\": \"MISSING_A1\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("actor A1 is required.", exception.Message);
    }

    [TestMethod]
    public void Missing_required_crew_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"crewId\": \"crew_aster_repair_02\"", "\"crewId\": \"missing_crew\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("crew crew_aster_repair_02 is required.", exception.Message);
    }

    [TestMethod]
    public void Loads_exact_canonical_crew_state()
    {
        var state = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));

        var crew = state.Crews.Single(candidate => candidate.CrewId == "crew_aster_repair_02");
        Assert.AreEqual("Aster repair crew 02", crew.DisplayName);
        Assert.AreEqual("Aster", crew.HomeSectorId);
        Assert.AreEqual("available", crew.Status);
        Assert.AreEqual("A1", crew.AssignedActorId);
        CollectionAssert.AreEqual(new[] { "R2", "R5", "R6" }, crew.SystemRefs.ToArray());
    }

    [TestMethod]
    [DataRow("available")]
    [DataRow("unavailable")]
    public void Bounded_crew_statuses_are_accepted(string status)
    {
        var root = ParseFixtureNode();
        root["crews"]!.AsArray()[0]!["status"] = status;

        var state = InitialStateLoader.LoadFromJson(root.ToJsonString());

        Assert.AreEqual(status, state.Crews[0].Status);
    }

    [TestMethod]
    public void Unsupported_crew_status_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["crews"]!.AsArray()[0]!["status"] = "assigned";

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("crew crew_aster_repair_02 status is invalid.", exception.Message);
    }

    [TestMethod]
    public void Empty_crew_system_refs_fail_deterministically()
    {
        var root = ParseFixtureNode();
        root["crews"]!.AsArray()[0]!["systemRefs"] = new System.Text.Json.Nodes.JsonArray();

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(
            "crew crew_aster_repair_02 systemRefs must be non-empty and unique.",
            exception.Message);
    }

    [TestMethod]
    [DataRow(" ")]
    [DataRow("R2")]
    public void Blank_or_duplicate_crew_system_refs_fail_deterministically(string duplicateOrBlank)
    {
        var root = ParseFixtureNode();
        var refs = root["crews"]!.AsArray()[0]!["systemRefs"]!.AsArray();
        refs.Add(duplicateOrBlank);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(
            "crew crew_aster_repair_02 systemRefs must be non-empty and unique.",
            exception.Message);
    }

    [TestMethod]
    public void Unknown_crew_system_ref_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["crews"]!.AsArray()[0]!["systemRefs"]!.AsArray()[0] = "UNKNOWN-SYSTEM";

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(
            "crew crew_aster_repair_02 references unknown system UNKNOWN-SYSTEM.",
            exception.Message);
    }

    [TestMethod]
    public void Loads_exact_canonical_tools_in_source_order()
    {
        var state = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));

        Assert.HasCount(2, state.Tools);
        AssertTool(
            state.Tools[0],
            "local_grid_panel",
            "Local grid panel",
            "available",
            ["R2", "R5"],
            ["query_branch_load", "query_heat_alarm", "dry_run_reroute"]);
        AssertTool(
            state.Tools[1],
            "maintenance_console",
            "Maintenance console",
            "available",
            ["R2", "R5"],
            ["query_asset_status", "query_backlog", "dry_run_patch"]);
    }

    [TestMethod]
    [DataRow("available")]
    [DataRow("unavailable")]
    public void Bounded_tool_statuses_are_accepted(string status)
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[0]!["status"] = status;

        var state = InitialStateLoader.LoadFromJson(root.ToJsonString());

        Assert.AreEqual(status, state.Tools[0].Status);
    }

    [TestMethod]
    public void Missing_tools_fails_at_deserialization_boundary()
    {
        var root = ParseFixtureNode();
        root.Remove("tools");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Null_tools_fails_loader_validation()
    {
        var root = ParseFixtureNode();
        root["tools"] = null;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("tools is required.", exception.Message);
    }

    [TestMethod]
    public void Empty_tools_fails_with_first_required_tool()
    {
        var root = ParseFixtureNode();
        root["tools"] = new System.Text.Json.Nodes.JsonArray();

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("tool local_grid_panel is required.", exception.Message);
    }

    [TestMethod]
    public void Null_tool_entry_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[0] = null;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("tool entry is required.", exception.Message);
    }

    [TestMethod]
    [DataRow("toolId", " ", "tool id is required.")]
    [DataRow("displayName", " ", "tool local_grid_panel displayName is required.")]
    [DataRow("status", " ", "tool local_grid_panel status is required.")]
    public void Blank_tool_scalar_fields_fail_deterministically(
        string propertyName,
        string value,
        string expectedMessage)
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[0]![propertyName] = value;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(expectedMessage, exception.Message);
    }

    [TestMethod]
    public void Unsupported_tool_status_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[0]!["status"] = "executing";

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("tool local_grid_panel status is invalid.", exception.Message);
    }

    [TestMethod]
    [DataRow("systemRefs", "tool local_grid_panel systemRefs are required.")]
    [DataRow("supportedActions", "tool local_grid_panel supportedActions are required.")]
    public void Null_tool_collections_fail_deterministically(string propertyName, string expectedMessage)
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[0]![propertyName] = null;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(expectedMessage, exception.Message);
    }

    [TestMethod]
    [DataRow("systemRefs", "empty")]
    [DataRow("systemRefs", "blank")]
    [DataRow("systemRefs", "duplicate")]
    [DataRow("supportedActions", "empty")]
    [DataRow("supportedActions", "blank")]
    [DataRow("supportedActions", "duplicate")]
    public void Invalid_tool_list_contents_fail_deterministically(string propertyName, string mutation)
    {
        var root = ParseFixtureNode();
        var values = root["tools"]!.AsArray()[0]![propertyName]!.AsArray();
        if (mutation == "empty")
        {
            values.Clear();
        }
        else if (mutation == "blank")
        {
            values[0] = " ";
        }
        else
        {
            values.Add(values[0]!.GetValue<string>());
        }

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(
            propertyName == "systemRefs"
                ? "tool local_grid_panel systemRefs must be non-empty and unique."
                : "tool local_grid_panel supportedActions must be non-empty and unique.",
            exception.Message);
    }

    [TestMethod]
    public void Unknown_tool_system_ref_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[0]!["systemRefs"]!.AsArray()[0] = "UNKNOWN-SYSTEM";

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(
            "tool local_grid_panel references unknown system UNKNOWN-SYSTEM.",
            exception.Message);
    }

    [TestMethod]
    public void Duplicate_tool_ids_fail_deterministically()
    {
        var root = ParseFixtureNode();
        root["tools"]!.AsArray()[1]!["toolId"] = "local_grid_panel";

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("tool ids must be unique.", exception.Message);
    }

    [TestMethod]
    [DataRow("local_grid_panel")]
    [DataRow("maintenance_console")]
    public void Missing_required_tool_fails_deterministically(string toolId)
    {
        var root = ParseFixtureNode();
        var tools = root["tools"]!.AsArray();
        var tool = tools.Single(candidate => candidate!["toolId"]!.GetValue<string>() == toolId);
        tools.Remove(tool);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual($"tool {toolId} is required.", exception.Message);
    }

    [TestMethod]
    public void Unknown_actor_tool_ref_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["actors"]!.AsArray()[0]!["toolRefs"]!.AsArray()[0] = "unknown_tool";

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("actor A1 references unknown tool unknown_tool.", exception.Message);
    }

    [TestMethod]
    public void Loads_A1_local_context_and_reference_only_resources()
    {
        var state = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));

        var a1 = state.Actors.Single(actor => actor.ActorId == "A1");
        Assert.HasCount(1, a1.LocalObservations);
        Assert.AreEqual("alarm", a1.LocalObservations[0].Kind);
        Assert.AreEqual("event:event-t000-aster-r2-heat-alarm", a1.LocalObservations[0].SourceRef);
        CollectionAssert.AreEqual(new[] { "crew_aster_repair_02" }, a1.CrewRefs.ToArray());
        CollectionAssert.AreEqual(new[] { "local_grid_panel", "maintenance_console" }, a1.ToolRefs.ToArray());

        foreach (var actor in state.Actors.Where(actor => actor.ActorId != "A1"))
        {
            Assert.IsEmpty(actor.LocalObservations, actor.ActorId);
            Assert.IsEmpty(actor.CrewRefs, actor.ActorId);
            Assert.IsEmpty(actor.ToolRefs, actor.ActorId);
        }
    }

    [TestMethod]
    [DataRow("event:event-t000-aster-r2-heat-alarm")]
    [DataRow("system:Aster.R2.gridLoad")]
    [DataRow("system:Aster.R6.crewPressure")]
    [DataRow("sensor:aster_r2.heat-alarm")]
    public void Accepted_source_refs_load(string sourceRef)
    {
        var json = ReplaceA1SourceRef(ReadFixture("aster-minimal-world-state.json"), sourceRef);

        var state = InitialStateLoader.LoadFromJson(json);

        Assert.AreEqual(sourceRef, state.Actors.Single(actor => actor.ActorId == "A1").LocalObservations[0].SourceRef);
    }

    [TestMethod]
    [DataRow("event-t000-aster-r2-heat-alarm")]
    [DataRow("event:")]
    [DataRow("event:heat alarm")]
    [DataRow("system:Aster/R2/gridLoad")]
    [DataRow("sensor:aster:r2-heat-alarm")]
    [DataRow("event:hőriasztás")]
    [DataRow("event:heat-alarm\n")]
    [DataRow("Event:event-t000-aster-r2-heat-alarm")]
    [DataRow("event:-heat-alarm")]
    [DataRow("sensor:.aster-r2")]
    [DataRow("system:_Aster.R2")]
    public void Rejected_source_refs_fail_deterministically(string sourceRef)
    {
        var json = ReplaceA1SourceRef(ReadFixture("aster-minimal-world-state.json"), sourceRef);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual(
            "actor A1 observation sourceRef is invalid.",
            exception.Message);
    }

    [TestMethod]
    public void A9_can_load_its_actor_local_observable_R10_source()
    {
        var json = WithActorObservation(
            ReadFixture("aster-minimal-world-state.json"),
            "A9",
            "system:Aster.R10.rumorPressure",
            "Local rumor indicator is active.");

        var state = InitialStateLoader.LoadFromJson(json);

        Assert.AreEqual(
            "system:Aster.R10.rumorPressure",
            state.Actors.Single(actor => actor.ActorId == "A9").LocalObservations[0].SourceRef);
    }

    [TestMethod]
    [DataRow("A1", "system:Aster.R10.rumorPressure", "actor observation source is not authorized for that actor.")]
    [DataRow("A1", "system:Vireo.VIREO-STUB.stubReadiness", "actor observation source is not authorized for that actor.")]
    [DataRow("A9", "system:Aster.R2.gridLoad", "actor observation source is not authorized for that actor.")]
    [DataRow("A2", "event:event-t000-aster-r2-heat-alarm", "actor observation source is not authorized for that actor.")]
    [DataRow("A2", "sensor:aster_r2.heat-alarm", "actor observation source is not authorized for that actor.")]
    [DataRow("A1", "system:Aster.R2.thermalDebt", "actor observation uses a non-observable source.")]
    [DataRow("A1", "system:Aster.R5.debtLevel", "actor observation uses a non-observable source.")]
    [DataRow("A1", "system:Aster.R2.unknownMetric", "actor observation source cannot be resolved uniquely.")]
    [DataRow("A1", "system:Aster.UNKNOWN.gridLoad", "actor observation source is not authorized for that actor.")]
    [DataRow("A1", "system:Unknown.R2.gridLoad", "actor observation source is not authorized for that actor.")]
    [DataRow("A1", "event:unknown-a4a-event", "actor observation uses an unsupported source.")]
    [DataRow("A1", "sensor:unknown-a4a-sensor", "actor observation uses an unsupported source.")]
    public void Actor_local_source_policy_rejects_unavailable_sources(
        string actorId,
        string sourceRef,
        string expectedMessage)
    {
        var json = WithActorObservation(
            ReadFixture("aster-minimal-world-state.json"), actorId, sourceRef, "Local indicator is active.");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.IsFalse(exception.Message.Contains(sourceRef, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Home_sector_check_rejects_other_sector_even_when_system_is_known_and_metric_is_observable()
    {
        var root = ParseFixtureNode();
        root["sectors"]!.AsArray()[1]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray()[0]!["visibility"] = "observable";
        root["actors"]!.AsArray()[0]!["systemRefs"]!.AsArray().Add("VIREO-STUB");
        SetActorObservation(root, "A1", "system:Vireo.VIREO-STUB.stubReadiness", "Local indicator is active.");

        Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));
    }

    [TestMethod]
    [DataRow("thermalDebt is elevated")]
    [DataRow("THERMALDEBT is elevated")]
    [DataRow("(thermalDebt) is elevated")]
    [DataRow("thermal.Debt is elevated")]
    [DataRow("debtLevel is elevated")]
    [DataRow("stubReadiness is low")]
    public void Protected_metric_names_are_rejected_across_the_loaded_world(string signal)
    {
        var json = WithActorObservation(
            ReadFixture("aster-minimal-world-state.json"),
            "A1",
            "event:event-t000-aster-r2-heat-alarm",
            signal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        StringAssert.Contains(exception.Message, "protected metric name");
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Protected value is 0.41")]
    [DataRow("Protected value is 0.410")]
    [DataRow("Protected value is 41e-2")]
    [DataRow("Protected value is +0.41")]
    [DataRow("Protected value is 0.52")]
    [DataRow("Protected value is 0.1")]
    public void Protected_metric_values_are_rejected_across_the_loaded_world(string signal)
    {
        var json = WithActorObservation(
            ReadFixture("aster-minimal-world-state.json"),
            "A1",
            "event:event-t000-aster-r2-heat-alarm",
            signal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        StringAssert.Contains(exception.Message, "protected metric value");
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("prethermalDebtpost is an unrelated identifier")]
    [DataRow("Visible delta is -0.41")]
    [DataRow("Visible reading is 10.41")]
    [DataRow("Visible reading is 0.4101")]
    [DataRow("Visible reading is 0_41")]
    public void Protected_content_checks_avoid_defined_boundary_and_numeric_false_matches(string signal)
    {
        var json = WithActorObservation(
            ReadFixture("aster-minimal-world-state.json"),
            "A1",
            "event:event-t000-aster-r2-heat-alarm",
            signal);

        _ = InitialStateLoader.LoadFromJson(json);
    }

    [TestMethod]
    [DataRow("Protected integer is 1")]
    [DataRow("Protected integer is +1")]
    [DataRow("Protected integer is 1e0")]
    public void Protected_numeric_tokenizer_handles_integer_forms(string signal)
    {
        var root = ParseFixtureNode();
        root["sectors"]!.AsArray()[1]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray()[0]!["value"] = 1.0;
        SetActorObservation(root, "A1", "event:event-t000-aster-r2-heat-alarm", signal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        StringAssert.Contains(exception.Message, "protected metric value");
    }

    [TestMethod]
    [DataRow("observationId", "thermalDebt")]
    [DataRow("kind", "value 0.41")]
    [DataRow("signal", "thermal\u200bDebt is 0.4\u200b1")]
    [DataRow("signal", "thermal\U000E0001Debt is elevated")]
    public void Protected_content_is_rejected_from_every_projected_observation_field(
        string propertyName,
        string value)
    {
        var root = ParseFixtureNode();
        root["actors"]!.AsArray()[0]!["localObservations"]!.AsArray()[0]![propertyName] = value;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.IsFalse(exception.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("role", "thermalDebt operator")]
    [DataRow("toolRefs", "value=0.41")]
    public void Protected_content_is_rejected_from_other_projected_actor_fields(
        string propertyName,
        string value)
    {
        var root = ParseFixtureNode();
        var actor = root["actors"]!.AsArray()[0]!;
        if (propertyName == "toolRefs")
        {
            actor[propertyName] = new System.Text.Json.Nodes.JsonArray(value);
        }
        else
        {
            actor[propertyName] = value;
        }

        Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));
    }

    [TestMethod]
    [DataRow("displayName", "thermal_Debt", "actor context exposes a protected metric name.")]
    [DataRow("role", "thermalDebt_", "actor context exposes a protected metric name.")]
    [DataRow("layer", "_thermalDebt", "actor context exposes a protected metric name.")]
    [DataRow("crewRefs", "debt_Level", "actor context exposes a protected metric name.")]
    [DataRow("displayName", "stub_Readiness", "actor context exposes a protected metric name.")]
    [DataRow("displayName", "value_0.41", "actor context exposes a protected metric value.")]
    [DataRow("role", "0.41_value", "actor context exposes a protected metric value.")]
    [DataRow("layer", "0.4_1", "actor context exposes a protected metric value.")]
    [DataRow("crewRefs", "0.4_1", "actor context exposes a protected metric value.")]
    public void Protected_actor_fields_reject_underscore_evasions_after_referential_integrity(
        string propertyName,
        string value,
        string expectedMessage)
    {
        var root = ParseFixtureNode();
        SetA1ProjectedActorField(root, propertyName, value);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.IsFalse(exception.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(0.41, "_0.41", true)]
    [DataRow(0.41, "0.41_", true)]
    [DataRow(0.41, "value__0.41", true)]
    [DataRow(0.41, "0.41__value", true)]
    [DataRow(0.41, "delta=_0.41", true)]
    [DataRow(0.41, "_-0.41", false)]
    [DataRow(0.41, "_+0.41", true)]
    [DataRow(0.41, "value__+0.41", true)]
    [DataRow(0.41, "delta=-0.41", false)]
    [DataRow(0.41, "value-0.41", false)]
    [DataRow(41.0, "0_41", true)]
    [DataRow(-0.41, "_0.41", false)]
    [DataRow(-0.41, "0.41_", false)]
    [DataRow(-0.41, "value__0.41", false)]
    [DataRow(-0.41, "0.41__value", false)]
    [DataRow(-0.41, "delta=_0.41", false)]
    [DataRow(-0.41, "_-0.41", true)]
    [DataRow(-0.41, "_+0.41", false)]
    [DataRow(-0.41, "value__+0.41", false)]
    [DataRow(-0.41, "delta=-0.41", true)]
    [DataRow(-0.41, "value-0.41", true)]
    public void Numeric_boundary_matrix_preserves_sign_through_loader(
        double protectedValue,
        string value,
        bool expectRejection)
    {
        var root = ParseFixtureNode();
        root["sectors"]!.AsArray()[0]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray()[1]!["value"] = protectedValue;
        SetA1ProjectedActorField(root, "displayName", value);

        if (!expectRejection)
        {
            var state = InitialStateLoader.LoadFromJson(root.ToJsonString());
            Assert.AreEqual(value, state.Actors.Single(actor => actor.ActorId == "A1").DisplayName);
            return;
        }

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("actor context exposes a protected metric value.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("thermalDebt", "actor context exposes a protected metric name.")]
    [DataRow("value_0.41", "actor context exposes a protected metric value.")]
    public void Policy_driven_actor_id_diagnostics_do_not_reflect_rejected_ids(
        string actorId,
        string expectedMessage)
    {
        var root = ParseFixtureNode();
        AddValidExtraActor(root, actorId);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual(expectedMessage, exception.Message);
        Assert.IsFalse(exception.Message.Contains(actorId, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Duplicate_metric_names_fail_even_without_actor_observations()
    {
        var root = ParseFixtureNode();
        root["actors"]!.AsArray()[0]!["localObservations"] = new System.Text.Json.Nodes.JsonArray();
        var metrics = root["sectors"]!.AsArray()[0]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray();
        metrics.Add(System.Text.Json.Nodes.JsonNode.Parse(metrics[0]!.ToJsonString()));

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("system R2 metric names must be unique.", exception.Message);
    }

    [TestMethod]
    public void Protected_metric_inventory_normalizes_format_characters_in_metric_names()
    {
        var root = ParseFixtureNode();
        root["sectors"]!.AsArray()[0]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray()[1]!["name"] = "thermal\u200bDebt";
        SetActorObservation(root, "A1", "event:event-t000-aster-r2-heat-alarm", "thermalDebt is elevated");

        Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));
    }

    [TestMethod]
    public void Negative_protected_metric_value_preserves_sign_semantics()
    {
        var root = ParseFixtureNode();
        root["sectors"]!.AsArray()[0]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray()[1]!["value"] = -0.41;
        const string signal = "Protected value is -0.41";
        SetActorObservation(root, "A1", "event:event-t000-aster-r2-heat-alarm", signal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("actor observation exposes a protected metric value.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Protected value is \u22120.41")]
    [DataRow("Protected value is 0\u200B\u066B\u200B41")]
    [DataRow("thermal\u200bDebt is elevated")]
    public void Non_ascii_projected_content_is_rejected_before_protected_value_comparison(string signal)
    {
        var root = ParseFixtureNode();
        SetActorObservation(root, "A1", "event:event-t000-aster-r2-heat-alarm", signal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("actor observation contains unsupported non-ASCII content.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    public void All_observable_worlds_remain_valid_without_a_protected_inventory()
    {
        var root = ParseFixtureNode();
        foreach (var sector in root["sectors"]!.AsArray())
        {
            foreach (var system in sector!["systems"]!.AsArray())
            {
                foreach (var metric in system!["metrics"]!.AsArray())
                {
                    metric!["visibility"] = "observable";
                }
            }
        }

        _ = InitialStateLoader.LoadFromJson(root.ToJsonString());
    }

    [TestMethod]
    public void Protected_metric_names_are_rejected_from_projected_source_refs()
    {
        var root = ParseFixtureNode();
        root["sectors"]!.AsArray()[0]!["systems"]!.AsArray()[0]!["metrics"]!.AsArray()[1]!["name"] = "R2";
        SetActorObservation(root, "A1", "system:Aster.R2.gridLoad", "Local grid load is visible.");

        Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));
    }

    [TestMethod]
    [DataRow("localObservations")]
    [DataRow("crewRefs")]
    [DataRow("toolRefs")]
    public void Missing_actor_context_collection_fails_at_deserialization_boundary(string propertyName)
    {
        var json = RemoveA1Property(ReadFixture("aster-minimal-world-state.json"), propertyName);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    [DataRow("localObservations")]
    [DataRow("crewRefs")]
    [DataRow("toolRefs")]
    public void Null_actor_context_collection_fails_loader_validation(string propertyName)
    {
        var json = ReplaceA1PropertyWithNull(ReadFixture("aster-minimal-world-state.json"), propertyName);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual($"actor A1 {propertyName} are required.", exception.Message);
    }

    [TestMethod]
    public void Duplicate_observation_ids_fail_deterministically()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(ReadFixture("aster-minimal-world-state.json"))!.AsObject();
        var observations = root["actors"]!.AsArray()[0]!["localObservations"]!.AsArray();
        observations.Add(new System.Text.Json.Nodes.JsonObject
        {
            ["observationId"] = "obs-a1-t000-aster-r2-heat-alarm",
            ["kind"] = "alarm",
            ["signal"] = "Second alarm.",
            ["sourceRef"] = "sensor:aster-r2-secondary"
        });

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("actor A1 observation ids must be unique.", exception.Message);
    }

    [TestMethod]
    public void Duplicate_resource_refs_fail_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json").Replace(
            "\"crewRefs\": [\"crew_aster_repair_02\"]",
            "\"crewRefs\": [\"crew_aster_repair_02\", \"crew_aster_repair_02\"]",
            StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("actor A1 crewRefs must be non-empty and unique.", exception.Message);
    }

    [TestMethod]
    public void Duplicate_tool_refs_fail_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json").Replace(
            "\"toolRefs\": [\"local_grid_panel\", \"maintenance_console\"]",
            "\"toolRefs\": [\"local_grid_panel\", \"local_grid_panel\"]",
            StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("actor A1 toolRefs must be non-empty and unique.", exception.Message);
    }

    [TestMethod]
    public void Null_observation_entry_fails_deterministically()
    {
        var root = ParseFixtureNode();
        root["actors"]!.AsArray()[0]!["localObservations"]!.AsArray().Add(null);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));

        Assert.AreEqual("actor A1 observation entry is required.", exception.Message);
    }

    [TestMethod]
    [DataRow("observationId")]
    [DataRow("kind")]
    [DataRow("signal")]
    [DataRow("sourceRef")]
    public void Empty_observation_fields_fail_deterministically(string propertyName)
    {
        var root = ParseFixtureNode();
        root["actors"]!.AsArray()[0]!["localObservations"]!.AsArray()[0]![propertyName] = " ";

        Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));
    }

    [TestMethod]
    [DataRow("crewRefs")]
    [DataRow("toolRefs")]
    public void Empty_resource_ref_fails_deterministically(string propertyName)
    {
        var root = ParseFixtureNode();
        root["actors"]!.AsArray()[0]![propertyName]!.AsArray()[0] = " ";

        Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(root.ToJsonString()));
    }

    [TestMethod]
    public void Unknown_actor_crew_reference_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json").Replace(
            "\"crewRefs\": [\"crew_aster_repair_02\"]",
            "\"crewRefs\": [\"unknown_crew\"]",
            StringComparison.Ordinal);

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("actor A1 crew reference unknown_crew is required.", exception.Message);
    }

    [TestMethod]
    public void Missing_required_stub_sector_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"sectorId\": \"Vireo\"", "\"sectorId\": \"MissingVireo\"");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("sector Vireo is required.", exception.Message);
    }

    [TestMethod]
    public void Default_simulation_seed_fails_deterministically()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"simulationSeed\": 42", "\"simulationSeed\": 0");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("simulationSeed must be present and greater than zero.", exception.Message);
    }

    [TestMethod]
    public void Missing_simulation_seed_fails_at_loader_boundary()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "scenarioId": "invalid-missing-seed",
          "sectors": [],
          "actors": [],
          "crews": []
        }
        """;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Null_top_level_collection_fails_deterministically()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "scenarioId": "invalid-null-sectors",
          "simulationSeed": 42,
          "sectors": null,
          "actors": [],
          "crews": [],
          "tools": []
        }
        """;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("sectors is required.", exception.Message);
    }

    [TestMethod]
    public void Null_nested_collection_fails_deterministically()
    {
        const string json = """
        {
          "schemaVersion": "0.1",
          "scenarioId": "invalid-null-systems",
          "simulationSeed": 42,
          "sectors": [
            { "sectorId": "Aster", "displayName": "Aster", "systems": null }
          ],
          "actors": [],
          "crews": [],
          "tools": []
        }
        """;

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("sector Aster systems are required.", exception.Message);
    }

    [TestMethod]
    public void Malformed_json_fails_at_loader_boundary()
    {
        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson("{ not-json"));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Numeric_metric_visibility_json_fails_at_loader_boundary()
    {
        var json = ReadFixture("aster-minimal-world-state.json")
            .Replace("\"visibility\": \"observable\"", "\"visibility\": 999");

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.LoadFromJson(json));

        Assert.AreEqual("WorldState JSON is invalid.", exception.Message);
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void Undefined_metric_visibility_fails_deterministically()
    {
        var source = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
        var metric = source.Sectors[0].Systems[0].Metrics[0];
        var invalid = ReplaceFirstMetric(source, metric with { Visibility = (MetricVisibility)999 });

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.Validate(invalid));

        Assert.AreEqual("system R2 metric gridLoad visibility is invalid.", exception.Message);
    }

    [TestMethod]
    public void Non_finite_metric_value_fails_deterministically()
    {
        var source = InitialStateLoader.LoadFromJson(ReadFixture("aster-minimal-world-state.json"));
        var metric = source.Sectors[0].Systems[0].Metrics[0];
        var invalid = ReplaceFirstMetric(source, metric with { Value = double.NaN });

        var exception = Assert.ThrowsExactly<InitialStateValidationException>(() =>
            InitialStateLoader.Validate(invalid));

        Assert.AreEqual("system R2 metric gridLoad value must be finite.", exception.Message);
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static void AssertTool(
        ToolState tool,
        string toolId,
        string displayName,
        string status,
        string[] systemRefs,
        string[] supportedActions)
    {
        Assert.AreEqual(toolId, tool.ToolId);
        Assert.AreEqual(displayName, tool.DisplayName);
        Assert.AreEqual(status, tool.Status);
        CollectionAssert.AreEqual(systemRefs, tool.SystemRefs.ToArray());
        CollectionAssert.AreEqual(supportedActions, tool.SupportedActions.ToArray());
    }

    private static string ReplaceA1SourceRef(string json, string sourceRef)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        root["actors"]!.AsArray()[0]!["localObservations"]!.AsArray()[0]!["sourceRef"] = sourceRef;
        return root.ToJsonString();
    }

    private static string RemoveA1Property(string json, string propertyName)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = System.Text.Json.Nodes.JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        root["actors"]!.AsArray()[0]!.AsObject().Remove(propertyName);
        return root.ToJsonString();
    }

    private static string ReplaceA1PropertyWithNull(string json, string propertyName)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        root["actors"]!.AsArray()[0]![propertyName] = null;
        return root.ToJsonString();
    }

    private static System.Text.Json.Nodes.JsonObject ParseFixtureNode()
    {
        return System.Text.Json.Nodes.JsonNode.Parse(ReadFixture("aster-minimal-world-state.json"))!.AsObject();
    }

    private static string WithActorObservation(string json, string actorId, string sourceRef, string signal)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        SetActorObservation(root, actorId, sourceRef, signal);
        return root.ToJsonString();
    }

    private static void SetActorObservation(
        System.Text.Json.Nodes.JsonObject root,
        string actorId,
        string sourceRef,
        string signal)
    {
        var actor = root["actors"]!.AsArray()
            .Single(node => string.Equals(node!["actorId"]!.GetValue<string>(), actorId, StringComparison.Ordinal))!;
        actor["localObservations"] = new System.Text.Json.Nodes.JsonArray
        {
            new System.Text.Json.Nodes.JsonObject
            {
                ["observationId"] = $"obs-{actorId.ToLowerInvariant()}-policy-test",
                ["kind"] = "alarm",
                ["signal"] = signal,
                ["sourceRef"] = sourceRef
            }
        };
    }

    private static void SetA1ProjectedActorField(
        System.Text.Json.Nodes.JsonObject root,
        string propertyName,
        string value)
    {
        var actor = root["actors"]!.AsArray()
            .Single(node => string.Equals(node!["actorId"]!.GetValue<string>(), "A1", StringComparison.Ordinal))!;
        if (propertyName == "crewRefs")
        {
            var crews = root["crews"]!.AsArray();
            var knownCrew = System.Text.Json.Nodes.JsonNode.Parse(crews[0]!.ToJsonString())!.AsObject();
            knownCrew["crewId"] = value;
            crews.Add(knownCrew);
            actor[propertyName] = new System.Text.Json.Nodes.JsonArray(value);
            return;
        }

        actor[propertyName] = value;
    }

    private static void AddValidExtraActor(System.Text.Json.Nodes.JsonObject root, string actorId)
    {
        var actors = root["actors"]!.AsArray();
        var source = actors.Single(node => string.Equals(
            node!["actorId"]!.GetValue<string>(), "A2", StringComparison.Ordinal));
        var extraActor = System.Text.Json.Nodes.JsonNode.Parse(source!.ToJsonString())!.AsObject();
        extraActor["actorId"] = actorId;
        actors.Add(extraActor);
    }

    private static WorldState ReplaceFirstMetric(WorldState source, SystemMetric metric)
    {
        var sectors = source.Sectors.ToArray();
        var systems = sectors[0].Systems.ToArray();
        var metrics = systems[0].Metrics.ToArray();

        metrics[0] = metric;
        systems[0] = systems[0] with { Metrics = metrics };
        sectors[0] = sectors[0] with { Systems = systems };

        return source with { Sectors = sectors };
    }
}
