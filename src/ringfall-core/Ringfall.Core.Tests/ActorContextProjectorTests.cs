using System.Text;
using System.Text.Json;
using Ringfall.Core.Projection;
using Ringfall.Core.Scenarios;
using Ringfall.Core.State;
using Ringfall.Core.Visibility;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class ActorContextProjectorTests
{
    [TestMethod]
    public void Project_emits_only_materialized_A1_context()
    {
        var state = LoadFixture();

        var projection = ActorContextProjector.Project(state, "A1");

        Assert.AreEqual("A1", projection.ActorId);
        Assert.AreEqual("A1", projection.DisplayName);
        Assert.AreEqual("senior grid runner", projection.Role);
        Assert.AreEqual("L1", projection.Layer);
        Assert.HasCount(1, projection.Observations);
        Assert.AreEqual("Local R2 heat alarm is active.", projection.Observations[0].Signal);
        CollectionAssert.AreEqual(new[] { "crew_aster_repair_02" }, projection.CrewRefs.ToArray());
        CollectionAssert.AreEqual(new[] { "local_grid_panel", "maintenance_console" }, projection.ToolRefs.ToArray());

        var json = Encoding.UTF8.GetString(ActorContextProjectionJsonSerializer.SerializeToUtf8(state, "A1"));
        foreach (var forbidden in new[] { "thermalDebt", "debtLevel", "systemRefs", "availability", "capability", "authority" })
        {
            Assert.IsFalse(json.Contains(forbidden, StringComparison.Ordinal), forbidden);
        }
    }

    [TestMethod]
    public void Project_preserves_empty_context_for_other_Aster_actors()
    {
        var state = LoadFixture();

        foreach (var actorId in new[] { "A2", "A4", "A6", "A9" })
        {
            var projection = ActorContextProjector.Project(state, actorId);
            Assert.IsEmpty(projection.Observations, actorId);
            Assert.IsEmpty(projection.CrewRefs, actorId);
            Assert.IsEmpty(projection.ToolRefs, actorId);
        }
    }

    [TestMethod]
    public void Project_excludes_another_actors_materialized_observation()
    {
        var state = LoadFixture();
        var actors = state.Actors.ToArray();
        var a2Index = Array.FindIndex(actors, actor => actor.ActorId == "A2");
        actors[a2Index] = actors[a2Index] with
        {
            LocalObservations =
            [
                new ActorLocalObservation
                {
                    ObservationId = "obs-a2-private",
                    Kind = "inspection",
                    Signal = "A2-only inspection result.",
                    SourceRef = "sensor:a2-private-meter"
                }
            ]
        };

        var json = Encoding.UTF8.GetString(ActorContextProjectionJsonSerializer.SerializeToUtf8(state with { Actors = actors }, "A1"));

        Assert.IsFalse(json.Contains("obs-a2-private", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("A2-only inspection result.", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("A1", "system:Aster.R10.rumorPressure", "Local indicator is active.")]
    [DataRow("A1", "system:Vireo.VIREO-STUB.stubReadiness", "Local indicator is active.")]
    [DataRow("A2", "event:event-t000-aster-r2-heat-alarm", "Local alarm is active.")]
    [DataRow("A2", "sensor:aster_r2.heat-alarm", "Local alarm is active.")]
    [DataRow("A1", "system:Aster.R2.thermalDebt", "Local indicator is active.")]
    [DataRow("A1", "system:Aster.R5.debtLevel", "Local indicator is active.")]
    [DataRow("A1", "event:event-t000-aster-r2-heat-alarm", "stubReadiness is low.")]
    [DataRow("A1", "event:event-t000-aster-r2-heat-alarm", "Protected value is 0.1")]
    [DataRow("A1", "event:event-t000-aster-r2-heat-alarm", "Protected value is 0.410")]
    [DataRow("A1", "event:event-t000-aster-r2-heat-alarm", "Protected value is 41e-2")]
    [DataRow("A1", "event:event-t000-aster-r2-heat-alarm", "Protected value is +0.41")]
    [DataRow("A1", "event:bad source", "Local indicator is active.")]
    public void Project_rejects_directly_constructed_unsafe_actor_context(
        string actorId,
        string sourceRef,
        string signal)
    {
        var state = WithActorObservation(LoadFixture(), actorId, sourceRef, signal);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, actorId));

        Assert.IsFalse(exception.Message.Contains(sourceRef, StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("A1", "event:unknown-a4a-event", (int)ActorObservationViolationCategory.UnsupportedSource)]
    [DataRow("A2", "event:event-t000-aster-r2-heat-alarm", (int)ActorObservationViolationCategory.ActorSourceMismatch)]
    [DataRow("A1", "system:Aster.R2.unknownMetric", (int)ActorObservationViolationCategory.UnknownOrAmbiguousSource)]
    [DataRow("A1", "system:Aster.R2.thermalDebt", (int)ActorObservationViolationCategory.NonObservableSource)]
    public void Policy_classifies_material_source_rejection_branches(
        string actorId,
        string sourceRef,
        int expectedCategory)
    {
        var state = WithActorObservation(LoadFixture(), actorId, sourceRef, "Local indicator is active.");
        var actor = state.Actors.Single(candidate => candidate.ActorId == actorId);

        var violation = ActorObservationVisibilityPolicy.Evaluate(state, actor, actor.LocalObservations[0]);

        Assert.IsNotNull(violation);
        Assert.AreEqual((ActorObservationViolationCategory)expectedCategory, violation.Value.Category);
    }

    [TestMethod]
    public void Policy_classifies_invalid_world_state_before_source_evaluation()
    {
        var state = WithActorObservation(
            LoadFixture(), "A1", "event:event-t000-aster-r2-heat-alarm", "Local indicator is active.");
        state = state with { Sectors = [] };
        var actor = state.Actors.Single(candidate => candidate.ActorId == "A1");

        var violation = ActorObservationVisibilityPolicy.Evaluate(state, actor, actor.LocalObservations[0]);

        Assert.IsNotNull(violation);
        Assert.AreEqual(ActorObservationViolationCategory.InvalidWorldState, violation.Value.Category);
    }

    [TestMethod]
    [DataRow("thermal_Debt")]
    [DataRow("thermalDebt_")]
    [DataRow("_thermalDebt")]
    [DataRow("debt_Level")]
    [DataRow("stub_Readiness")]
    public void Policy_classifies_underscore_name_evasions_as_protected_metric_names(string value)
    {
        var violation = ActorObservationVisibilityPolicy.EvaluateProjectedText(LoadFixture(), value);

        Assert.IsNotNull(violation);
        Assert.AreEqual(ActorObservationViolationCategory.ProtectedMetricName, violation.Value.Category);
    }

    [TestMethod]
    [DataRow("value_0.41")]
    [DataRow("0.41_value")]
    [DataRow("0.4_1")]
    public void Policy_classifies_underscore_numeric_evasions_as_protected_metric_values(string value)
    {
        var violation = ActorObservationVisibilityPolicy.EvaluateProjectedText(LoadFixture(), value);

        Assert.IsNotNull(violation);
        Assert.AreEqual(ActorObservationViolationCategory.ProtectedMetricValue, violation.Value.Category);
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
    public void Policy_numeric_boundary_matrix_preserves_sign(
        double protectedValue,
        string value,
        bool expectViolation)
    {
        var state = WithProtectedThermalDebtValue(LoadFixture(), protectedValue);

        var violation = ActorObservationVisibilityPolicy.EvaluateProjectedText(state, value);

        if (!expectViolation)
        {
            Assert.IsNull(violation);
            return;
        }

        Assert.IsNotNull(violation);
        Assert.AreEqual(ActorObservationViolationCategory.ProtectedMetricValue, violation.Value.Category);
    }

    [TestMethod]
    [DataRow("prethermalDebtpost")]
    [DataRow("10.41")]
    [DataRow("0.4101")]
    [DataRow("-0.41")]
    [DataRow("0_41")]
    public void Policy_does_not_false_match_bounded_controls(string value)
    {
        Assert.IsNull(ActorObservationVisibilityPolicy.EvaluateProjectedText(LoadFixture(), value));
    }

    [TestMethod]
    public void Policy_preserves_signed_numeric_comparison()
    {
        var state = WithProtectedThermalDebtValue(LoadFixture(), -0.41);

        var violation = ActorObservationVisibilityPolicy.EvaluateProjectedText(state, "-0.41");

        Assert.IsNotNull(violation);
        Assert.AreEqual(ActorObservationViolationCategory.ProtectedMetricValue, violation.Value.Category);
    }

    [TestMethod]
    [DataRow("Protected value is \u22120.41")]
    [DataRow("Protected value is 0\u200B\u066B\u200B41")]
    [DataRow("thermal\u200bDebt is elevated")]
    public void Policy_classifies_non_ascii_projected_content_before_normalization(string value)
    {
        var violation = ActorObservationVisibilityPolicy.EvaluateProjectedText(LoadFixture(), value);

        Assert.IsNotNull(violation);
        Assert.AreEqual(ActorObservationViolationCategory.NonAsciiProjectedContent, violation.Value.Category);
    }

    [TestMethod]
    [DataRow("prethermalDebtpost is an unrelated identifier")]
    [DataRow("Visible delta is -0.41")]
    [DataRow("Visible reading is 10.41")]
    [DataRow("Visible reading is 0.4101")]
    [DataRow("Visible reading is 0_41")]
    public void Project_does_not_false_match_safe_numeric_tokens(string signal)
    {
        var state = WithActorObservation(
            LoadFixture(), "A1", "event:event-t000-aster-r2-heat-alarm", signal);

        var projection = ActorContextProjector.Project(state, "A1");

        Assert.AreEqual(signal, projection.Observations[0].Signal);
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
    public void Project_numeric_boundary_matrix_preserves_sign(
        double protectedValue,
        string value,
        bool expectRejection)
    {
        var state = WithProtectedThermalDebtValue(LoadFixture(), protectedValue);
        state = WithActorProjectedField(state, "displayName", value);

        if (!expectRejection)
        {
            Assert.AreEqual(value, ActorContextProjector.Project(state, "A1").DisplayName);
            return;
        }

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, "A1"));

        Assert.AreEqual("Actor context contains protected world-state content.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Project_rejects_negative_value_only_when_the_protected_inventory_is_negative()
    {
        var state = WithProtectedThermalDebtValue(LoadFixture(), -0.41);
        const string signal = "Protected value is -0.41";
        state = WithActorObservation(state, "A1", "event:event-t000-aster-r2-heat-alarm", signal);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, "A1"));

        Assert.AreEqual("Actor context contains a non-observable local observation.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("Protected value is \u22120.41")]
    [DataRow("Protected value is 0\u200B\u066B\u200B41")]
    [DataRow("thermal\u200bDebt is elevated")]
    public void Project_rejects_non_ascii_observations_on_the_observation_policy_path(string signal)
    {
        var state = WithActorObservation(
            LoadFixture(), "A1", "event:event-t000-aster-r2-heat-alarm", signal);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, "A1"));

        Assert.AreEqual("Actor context contains a non-observable local observation.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(signal, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Project_rejects_selected_unsafe_actor_but_ignores_non_selected_unsafe_actor()
    {
        var state = WithActorObservation(
            LoadFixture(), "A2", "event:event-t000-aster-r2-heat-alarm", "Local alarm is active.");

        _ = ActorContextProjector.Project(state, "A1");
        Assert.ThrowsExactly<InvalidOperationException>(() => ActorContextProjector.Project(state, "A2"));
    }

    [TestMethod]
    public void Project_rejects_duplicate_target_actor_deterministically()
    {
        var state = LoadFixture();
        var actors = state.Actors.Concat([state.Actors[0]]).ToArray();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Actors = actors }, "A1"));

        StringAssert.Contains(exception.Message, "ambiguous world state");
    }

    [TestMethod]
    public void Project_reports_duplicate_system_topology_as_invalid_world_state()
    {
        var state = LoadFixture();
        var sectors = state.Sectors.ToArray();
        sectors[0] = sectors[0] with { Systems = sectors[0].Systems.Concat([sectors[0].Systems[0]]).ToArray() };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Sectors = sectors }, "A1"));

        Assert.AreEqual("Actor context cannot be projected from an invalid world state.", exception.Message);
    }

    [TestMethod]
    [DataRow("observationId", "thermalDebt")]
    [DataRow("kind", "value 0.41")]
    [DataRow("signal", "thermal\u200bDebt is 0.4\u200b1")]
    public void Project_rejects_protected_content_in_any_observation_field(
        string propertyName,
        string value)
    {
        var state = LoadFixture();
        var actors = state.Actors.ToArray();
        var observation = actors[0].LocalObservations[0];
        observation = propertyName switch
        {
            "observationId" => observation with { ObservationId = value },
            "kind" => observation with { Kind = value },
            _ => observation with { Signal = value }
        };
        actors[0] = actors[0] with { LocalObservations = [observation] };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Actors = actors }, "A1"));
    }

    [TestMethod]
    public void Project_rejects_protected_content_in_resource_refs()
    {
        var state = LoadFixture();
        var actors = state.Actors.ToArray();
        actors[0] = actors[0] with { ToolRefs = ["value=0.41"] };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Actors = actors }, "A1"));
    }

    [TestMethod]
    [DataRow("displayName", "thermal_Debt")]
    [DataRow("role", "thermalDebt_")]
    [DataRow("layer", "_thermalDebt")]
    [DataRow("crewRefs", "debt_Level")]
    [DataRow("displayName", "stub_Readiness")]
    [DataRow("displayName", "value_0.41")]
    [DataRow("role", "0.41_value")]
    [DataRow("layer", "0.4_1")]
    [DataRow("crewRefs", "0.4_1")]
    public void Project_rejects_protected_actor_fields_after_known_crew_validation(
        string propertyName,
        string value)
    {
        var state = WithActorProjectedField(LoadFixture(), propertyName, value);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, "A1"));

        Assert.AreEqual("Actor context contains protected world-state content.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("thermalDebt")]
    [DataRow("value_0.41")]
    public void Project_policy_diagnostics_do_not_reflect_rejected_actor_ids(string actorId)
    {
        var state = WithValidExtraActor(LoadFixture(), actorId);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, actorId));

        Assert.AreEqual("Actor context contains protected world-state content.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(actorId, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("thermal\u200bDebt")]
    [DataRow("Protected value is \u22120.41")]
    public void Project_reports_non_ascii_actor_text_without_reflecting_the_payload(string value)
    {
        var state = WithActorProjectedField(LoadFixture(), "displayName", value);

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state, "A1"));

        Assert.AreEqual("Actor context contains unsupported non-ASCII content.", exception.Message);
        Assert.IsFalse(exception.Message.Contains(value, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Project_rejects_unknown_selected_actor_crew_refs()
    {
        var state = LoadFixture();
        var actors = state.Actors.ToArray();
        actors[0] = actors[0] with { CrewRefs = ["unknown_crew"] };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Actors = actors }, "A1"));
    }

    [TestMethod]
    public void Serializer_rejects_directly_constructed_unsafe_state()
    {
        var state = WithActorObservation(
            LoadFixture(), "A1", "event:event-t000-aster-r2-heat-alarm", "thermalDebt=0.41");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjectionJsonSerializer.SerializeToUtf8(state, "A1"));
    }

    [TestMethod]
    public void Project_rejects_empty_world_topology()
    {
        var state = LoadFixture() with { Sectors = [] };

        Assert.ThrowsExactly<InvalidOperationException>(() => ActorContextProjector.Project(state, "A1"));
    }

    [TestMethod]
    public void Project_rejects_nonempty_world_with_empty_system_topology()
    {
        var state = LoadFixture();
        var sectors = state.Sectors.ToArray();
        sectors[0] = sectors[0] with { Systems = [] };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Sectors = sectors }, "A1"));
    }

    [TestMethod]
    public void Project_rejects_cross_sector_duplicate_system_ids()
    {
        var state = LoadFixture();
        var sectors = state.Sectors.ToArray();
        sectors[1] = sectors[1] with { Systems = sectors[1].Systems.Concat([sectors[0].Systems[0]]).ToArray() };

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Sectors = sectors }, "A1"));
    }

    [TestMethod]
    public void Project_rejects_duplicate_or_empty_selected_actor_context_values()
    {
        var state = LoadFixture();
        var actors = state.Actors.ToArray();
        actors[0] = actors[0] with { ToolRefs = ["local_grid_panel", "local_grid_panel"] };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Actors = actors }, "A1"));

        actors = state.Actors.ToArray();
        actors[0] = actors[0] with
        {
            LocalObservations =
            [
                actors[0].LocalObservations[0],
                actors[0].LocalObservations[0] with { Signal = "Second visible alarm." }
            ]
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ActorContextProjector.Project(state with { Actors = actors }, "A1"));
    }

    [TestMethod]
    public void Project_serializes_resource_references_as_scalar_strings_only()
    {
        var state = LoadFixture();
        using var document = JsonDocument.Parse(ActorContextProjectionJsonSerializer.SerializeToUtf8(state, "A1"));
        var root = document.RootElement;

        foreach (var propertyName in new[] { "crewRefs", "toolRefs" })
        {
            Assert.AreEqual(JsonValueKind.Array, root.GetProperty(propertyName).ValueKind);
            foreach (var item in root.GetProperty(propertyName).EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.String, item.ValueKind);
            }
        }

        foreach (var forbidden in new[]
        {
            "status", "availability", "capability", "capabilities", "authority",
            "supportedActions", "permissions", "execution", "accessGrant"
        })
        {
            Assert.IsFalse(root.TryGetProperty(forbidden, out _), forbidden);
        }
    }

    [TestMethod]
    public void Project_rejects_unknown_actor()
    {
        Assert.ThrowsExactly<KeyNotFoundException>(() => ActorContextProjector.Project(LoadFixture(), "UNKNOWN"));
    }

    [TestMethod]
    public void Canonical_A1_projection_matches_brain_example_byte_for_byte()
    {
        var state = LoadFixture();
        var actual = ActorContextProjectionJsonSerializer.SerializeToUtf8(state, "A1");
        var expected = File.ReadAllBytes(FindRepositoryFile(
            "src", "ringfall-brain", "examples", "aster-a1-context.example.json"));

        CollectionAssert.AreEqual(expected, actual);
        Assert.IsFalse(actual.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.AreEqual((byte)'\n', actual[^1]);
        Assert.AreNotEqual((byte)'\n', actual[^2]);
        Assert.IsFalse(Encoding.UTF8.GetString(actual).Contains("\r", StringComparison.Ordinal));
    }

    private static Core.State.WorldState LoadFixture()
    {
        return InitialStateLoader.LoadFromJson(File.ReadAllText(FindRepositoryFile(
            "src", "ringfall-core", "Ringfall.Core.Tests", "Fixtures", "aster-minimal-world-state.json")));
    }

    private static WorldState WithActorObservation(
        WorldState state,
        string actorId,
        string sourceRef,
        string signal)
    {
        var actors = state.Actors.ToArray();
        var actorIndex = Array.FindIndex(actors, actor => actor.ActorId == actorId);
        actors[actorIndex] = actors[actorIndex] with
        {
            LocalObservations =
            [
                new ActorLocalObservation
                {
                    ObservationId = $"obs-{actorId.ToLowerInvariant()}-direct-test",
                    Kind = "alarm",
                    Signal = signal,
                    SourceRef = sourceRef
                }
            ]
        };
        return state with { Actors = actors };
    }

    private static WorldState WithActorProjectedField(
        WorldState state,
        string propertyName,
        string value)
    {
        var actors = state.Actors.ToArray();
        var actorIndex = Array.FindIndex(actors, actor => actor.ActorId == "A1");
        actors[actorIndex] = propertyName switch
        {
            "displayName" => actors[actorIndex] with { DisplayName = value },
            "role" => actors[actorIndex] with { Role = value },
            "layer" => actors[actorIndex] with { Layer = value },
            "crewRefs" => actors[actorIndex] with { CrewRefs = [value] },
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName))
        };

        var result = state with { Actors = actors };
        if (propertyName != "crewRefs")
        {
            return result;
        }

        var knownCrew = state.Crews.Single(crew => crew.CrewId == "crew_aster_repair_02") with { CrewId = value };
        return result with { Crews = state.Crews.Concat([knownCrew]).ToArray() };
    }

    private static WorldState WithProtectedThermalDebtValue(WorldState state, double value)
    {
        var sectors = state.Sectors.ToArray();
        var systems = sectors[0].Systems.ToArray();
        var metrics = systems[0].Metrics.ToArray();
        metrics[1] = metrics[1] with { Value = value };
        systems[0] = systems[0] with { Metrics = metrics };
        sectors[0] = sectors[0] with { Systems = systems };
        return state with { Sectors = sectors };
    }

    private static WorldState WithValidExtraActor(WorldState state, string actorId)
    {
        var source = state.Actors.Single(actor => actor.ActorId == "A2");
        return state with { Actors = state.Actors.Concat([source with { ActorId = actorId }]).ToArray() };
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file {Path.Combine(parts)}.");
    }
}
