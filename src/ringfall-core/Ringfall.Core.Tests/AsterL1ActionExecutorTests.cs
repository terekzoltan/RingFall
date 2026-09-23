using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ringfall.Core.Actions;
using Ringfall.Core.Artifacts;
using Ringfall.Core.Scenarios;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class AsterL1ActionExecutorTests
{
    private static WorldState State() => InitialStateLoader.LoadFromJson(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "aster-minimal-world-state.json")));

    private static AsterL1ToolActionCandidate Tool(string action = "dry_run_reroute", string? arguments = null) => new()
    {
        PacketId = "tool-1", PacketType = "ToolActionRequest", SchemaVersion = "0.1",
        IssuerId = "A1", IssuerLayer = "L1", IssuedAtTick = 1,
        SourceContextId = "ctx-aster-a1", ToolId = "local_grid_panel", Action = action,
        Arguments = arguments is null && action == "dry_run_reroute"
            ? Args("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}")
            : arguments is null ? null : Args(arguments),
        Mode = "dry_run", RequiresDryRun = action == "dry_run_reroute" ? true : null
    };

    private static AsterL1WorkOrderCandidate Work() => new()
    {
        PacketId = "work-1", PacketType = "WorkOrderRequest", SchemaVersion = "0.1",
        IssuerId = "A1", IssuerLayer = "L1", IssuedAtTick = 1,
        SourceContextId = "ctx-aster-a1", TargetCrewId = "crew_aster_repair_02",
        TargetLocation = "Aster/Grid-Spine-03", TaskType = "inspect_and_patch",
        Priority = "high", ExpectedReport = "short_structured", StealthLevel = "normal"
    };

    private static IReadOnlyDictionary<string, JsonElement> Args(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(
            item => item.Name, item => item.Value.Clone(), StringComparer.Ordinal);
    }

    [TestMethod]
    public void Identity_rejection_preserves_fresh_A4F_decision_and_exact_state_without_evidence()
    {
        var source = State();
        var cases = new (string Name, Func<AsterL1ActionExecution> Execute)[]
        {
            ("null tool", () => AsterL1ActionExecutor.Execute(source, (AsterL1ToolActionCandidate?)null)),
            ("null work", () => AsterL1ActionExecutor.Execute(source, (AsterL1WorkOrderCandidate?)null)),
            ("blank packet", () => AsterL1ActionExecutor.Execute(source, Tool() with { PacketId = " \t" })),
            ("blank work packet", () => AsterL1ActionExecutor.Execute(source, Work() with { PacketId = " " })),
            ("blank issuer", () => AsterL1ActionExecutor.Execute(source, Tool() with { IssuerId = " " })),
            ("blank work issuer", () => AsterL1ActionExecutor.Execute(source, Work() with { IssuerId = " " })),
            ("invalid layer", () => AsterL1ActionExecutor.Execute(source, Tool() with { IssuerLayer = "l1" })),
            ("invalid work layer", () => AsterL1ActionExecutor.Execute(source, Work() with { IssuerLayer = "L4" }))
        };
        var before = WorldStateJsonSerializer.Serialize(source);
        foreach (var (name, execute) in cases)
        {
            var result = execute();
            Assert.AreEqual(AsterL1DecisionStatus.Invalid, result.Decision.Status, name);
            Assert.IsNotEmpty(result.Decision.Issues, name);
            Assert.AreEqual(AsterL1ExecutionDisposition.IdentityRejected, result.Disposition, name);
            Assert.AreSame(source, result.InitialState, name);
            Assert.AreSame(source, result.FinalState, name);
            Assert.IsNull(result.ActionTrace, name);
            Assert.IsNull(result.ExecutionResult, name);
            Assert.IsNull(result.StateDiff, name);
            Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(source), name);
        }
    }

    [TestMethod]
    public void Valid_identities_reject_invalid_denied_and_unsupported_requests_with_ordered_diagnostics()
    {
        var state = State();
        var cases = new (AsterL1ToolActionCandidate Candidate, AsterL1DecisionStatus Status, string Issue, string Dimension)[]
        {
            (Tool() with { PacketId = "tool-rejected-invalid", PacketType = "WorkOrderRequest" }, AsterL1DecisionStatus.Invalid, "packet_type_mismatch:PacketType", "schema"),
            (Tool() with { PacketId = "tool-rejected-denied", Mode = "execute" }, AsterL1DecisionStatus.Denied, "tool_execute_denied:Mode", "authority"),
            (Tool(arguments: "{\"from_branch\":\"Aster-G4\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.2}") with { PacketId = "tool-rejected-unsupported" },
                AsterL1DecisionStatus.Unsupported, "tool_argument_value_unsupported:from_branch", "context")
        };
        foreach (var (candidate, status, issue, dimension) in cases)
        {
            var result = AsterL1ActionExecutor.Execute(state, candidate);
            Assert.AreEqual(status, result.Decision.Status);
            Assert.AreEqual(AsterL1ExecutionDisposition.Rejected, result.Disposition);
            Assert.AreSame(state, result.FinalState);
            Assert.IsNull(result.StateDiff);
            Assert.AreEqual("rejected", result.ActionTrace!.ExecutionStatus);
            Assert.AreEqual("tool_action", result.ActionTrace.ActionClass);
            AssertLinkage(result, candidate.PacketId);
            var executionResult = result.ExecutionResult!;
            Assert.AreEqual("Core rejected the request; no world state was changed.", executionResult.IssuerObservable.Summary);
            var diagnostic = result.Decision.Issues.Single();
            Assert.AreEqual(issue, $"{diagnostic.Code}:{diagnostic.Field}");
            Assert.AreEqual("fail", Validation(result.ActionTrace, dimension).Status);
            Assert.AreEqual(issue, Validation(result.ActionTrace, dimension).Notes);
            Assert.IsEmpty(result.ActionTrace.EvalFlags);
            Assert.IsEmpty(executionResult.TrueEffectRefs);
        }
    }

    [TestMethod]
    public void Contract_valid_identity_is_copied_verbatim_even_when_A4F_rejects_other_fields()
    {
        var state = State();
        var candidate = Tool() with { PacketId = " tool-1 ", IssuerId = " A1 ", IssuerLayer = "L2" };
        var result = AsterL1ActionExecutor.Execute(state, candidate);
        Assert.AreEqual(AsterL1ExecutionDisposition.Rejected, result.Disposition);
        Assert.AreSame(state, result.FinalState);
        Assert.AreEqual(" tool-1 ", result.ActionTrace!.SourcePacketId);
        Assert.AreEqual("action-trace: tool-1 ", result.ActionTrace.ActionTraceId);
        Assert.AreEqual(" A1 ", result.ActionTrace.IssuerId);
        Assert.AreEqual("L2", result.ActionTrace.IssuerLayer);
        Assert.AreEqual("execution-result: tool-1 ", result.ExecutionResult!.PacketId);
        Assert.AreEqual(" tool-1 ", result.ExecutionResult.SourcePacketId);
        Assert.IsNull(result.StateDiff);
    }

    [TestMethod]
    public void Reroute_succeeds_at_positive_and_maximum_fraction_without_world_effects()
    {
        foreach (var fraction in new[] { "0.01", "0.20" })
        {
            var state = State();
            var before = WorldStateJsonSerializer.Serialize(state);
            var candidate = Tool(arguments:
                $"{{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":{fraction}}}");
            var result = AsterL1ActionExecutor.Execute(state, candidate);
            AssertLinkage(result, candidate.PacketId);
            Assert.AreEqual(AsterL1DecisionStatus.Allowed, result.Decision.Status);
            Assert.AreEqual(AsterL1ExecutionDisposition.Succeeded, result.Disposition);
            Assert.AreSame(state, result.FinalState);
            Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
            Assert.IsNull(result.StateDiff);
            Assert.AreEqual("success", result.ActionTrace!.ExecutionStatus);
            Assert.AreEqual("tool_action", result.ActionTrace.ActionClass);
            Assert.AreEqual("action-trace:tool-1", result.ActionTrace.ActionTraceId);
            Assert.AreEqual("tool-1", result.ActionTrace.SourcePacketId);
            Assert.AreEqual("A1", result.ActionTrace.IssuerId);
            Assert.AreEqual("L1", result.ActionTrace.IssuerLayer);
            Assert.AreEqual("success", result.ExecutionResult!.ExecutionStatus);
            Assert.AreEqual("tool-1", result.ExecutionResult.SourcePacketId);
            Assert.AreEqual("The bounded reroute dry run completed without changing world state.", result.ExecutionResult.IssuerObservable.Summary);
            Assert.IsEmpty(result.ActionTrace.StateDiffRefs);
            Assert.IsEmpty(result.ActionTrace.EventsCreated);
            Assert.IsEmpty(result.ActionTrace.EvalFlags);
            Assert.IsEmpty(result.ExecutionResult.TrueEffectRefs);
            Assert.IsEmpty(result.ExecutionResult.EventsCreated);
            AssertAllPass(result.ActionTrace);
        }
    }

    [TestMethod]
    public void Invalid_reroutes_and_execute_mode_never_apply()
    {
        var cases = new (AsterL1ToolActionCandidate Candidate, string Issue)[]
        {
            (Tool() with { Mode = "execute" }, "tool_execute_denied"),
            (Tool() with { RequiresDryRun = false }, "tool_requires_dry_run_conflict"),
            (Tool(arguments: "{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0}"), "tool_argument_value_invalid"),
            (Tool(arguments: "{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.21}"), "tool_macro_surface_denied"),
            (Tool(arguments: "{\"from_branch\":\"Aster-G4\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.2}"), "tool_argument_value_unsupported")
        };
        foreach (var (candidate, issue) in cases)
        {
            var state = State();
            var before = WorldStateJsonSerializer.Serialize(state);
            var result = AsterL1ActionExecutor.Execute(state, candidate);
            Assert.AreEqual(AsterL1ExecutionDisposition.Rejected, result.Disposition);
            Assert.IsTrue(result.Decision.Issues.Any(item => item.Code == issue));
            Assert.AreSame(state, result.FinalState);
            Assert.IsNull(result.StateDiff);
            Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
        }
        var unavailable = State();
        unavailable = unavailable with { Tools = unavailable.Tools.Select(tool => tool.ToolId == "local_grid_panel"
            ? tool with { Status = "unavailable" } : tool).ToArray() };
        Assert.IsTrue(AsterL1ActionExecutor.Execute(unavailable, Tool()).Decision.Issues
            .Any(issue => issue.Code == "tool_unavailable"));
        Assert.IsTrue(AsterL1ActionExecutor.Execute(State(), Tool() with { IssuerId = "UNKNOWN" }).Decision.Issues
            .Any(issue => issue.Code == "issuer_not_found"));
    }

    [TestMethod]
    public void Allowed_queries_are_deferred_with_one_flag_and_no_effects()
    {
        foreach (var candidate in new[]
        {
            Tool("query_heat_alarm") with { PacketId = "tool-deferred-heat" },
            Tool("query_branch_load", "{\"branch_id\":\"Aster-G3\"}") with { PacketId = "tool-deferred-branch" }
        })
        {
            var state = State();
            var result = AsterL1ActionExecutor.Execute(state, candidate);
            AssertLinkage(result, candidate.PacketId);
            Assert.AreEqual(AsterL1DecisionStatus.Allowed, result.Decision.Status);
            Assert.AreEqual(AsterL1ExecutionDisposition.Deferred, result.Disposition);
            Assert.AreSame(state, result.FinalState);
            Assert.IsNull(result.StateDiff);
            Assert.AreEqual("deferred", result.ActionTrace!.ExecutionStatus);
            CollectionAssert.AreEqual(new[] { "a4g_execution_subset_deferred" }, result.ActionTrace.EvalFlags.ToArray());
            Assert.AreEqual("Core deferred this allowed request because it is outside the A4-G execution subset.",
                result.ExecutionResult!.IssuerObservable.Summary);
            Assert.IsEmpty(result.ExecutionResult.TrueEffectRefs);
            Assert.IsEmpty(result.ExecutionResult.EventsCreated);
            AssertAllPass(result.ActionTrace);
        }
    }

    [TestMethod]
    public void Work_order_reserves_only_exact_crew_and_repeated_dispatch_is_rejected()
    {
        var state = State();
        var before = WorldStateJsonSerializer.Serialize(state);
        var firstCandidate = Work();
        var first = AsterL1ActionExecutor.Execute(state, firstCandidate);
        AssertLinkage(first, firstCandidate.PacketId);
        Assert.AreEqual(AsterL1DecisionStatus.Allowed, first.Decision.Status);
        Assert.AreEqual(AsterL1ExecutionDisposition.Succeeded, first.Disposition);
        Assert.AreSame(state, first.InitialState);
        Assert.AreNotSame(state, first.FinalState);
        Assert.AreNotSame(state.Crews, first.FinalState.Crews);
        Assert.AreSame(state.Sectors, first.FinalState.Sectors);
        Assert.AreSame(state.Actors, first.FinalState.Actors);
        Assert.AreSame(state.Tools, first.FinalState.Tools);
        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
        Assert.AreEqual("available", state.Crews.Single(crew => crew.CrewId == "crew_aster_repair_02").Status);
        Assert.AreEqual("unavailable", first.FinalState.Crews.Single(crew => crew.CrewId == "crew_aster_repair_02").Status);
        var expected = JsonNode.Parse(before)!;
        expected["crews"]!.AsArray().Single(crew => crew!["crewId"]!.GetValue<string>() == "crew_aster_repair_02")!["status"] = "unavailable";
        Assert.IsTrue(JsonNode.DeepEquals(expected, JsonNode.Parse(WorldStateJsonSerializer.Serialize(first.FinalState))));
        Assert.AreEqual("work_order", first.ActionTrace!.ActionClass);
        Assert.AreEqual("success", first.ActionTrace.ExecutionStatus);
        Assert.AreEqual("The requested Aster inspection crew was dispatched.", first.ExecutionResult!.IssuerObservable.Summary);
        Assert.AreEqual("state-diff:work-1", first.StateDiff!.DiffId);
        Assert.AreEqual("action-trace:work-1", first.StateDiff.SourceActionTraceId);
        Assert.AreEqual(1, first.StateDiff.Tick);
        Assert.AreEqual(state.SimulationSeed, first.StateDiff.DeterministicSeed);
        Assert.HasCount(1, first.StateDiff.Changes);
        var change = first.StateDiff.Changes[0];
        Assert.AreEqual("/crews/crew_aster_repair_02/status", change.Path);
        Assert.AreEqual("issuer_observable", change.Visibility);
        Assert.AreEqual(StateDiffValue.FromString("available"), change.Old);
        Assert.AreEqual(StateDiffValue.FromString("unavailable"), change.New);
        Assert.HasCount(1, first.ActionTrace.StateDiffRefs);
        Assert.AreEqual("state-diff:work-1", first.ActionTrace.StateDiffRefs[0].RefId);
        Assert.AreEqual("state_diff", first.ActionTrace.StateDiffRefs[0].RefType);
        CollectionAssert.AreEqual(new[] { "state-diff:work-1" }, first.ExecutionResult.TrueEffectRefs.ToArray());
        Assert.IsEmpty(first.ActionTrace.EventsCreated);
        Assert.IsEmpty(first.ExecutionResult.EventsCreated);

        var secondCandidate = Work() with { PacketId = "work-2" };
        var second = AsterL1ActionExecutor.Execute(first.FinalState, secondCandidate);
        AssertLinkage(second, secondCandidate.PacketId);
        Assert.AreEqual(AsterL1ExecutionDisposition.Rejected, second.Disposition);
        Assert.AreEqual(AsterL1DecisionStatus.Denied, second.Decision.Status);
        Assert.IsTrue(second.Decision.Issues.Any(issue => issue.Code == "crew_unavailable"));
        Assert.AreSame(first.FinalState, second.FinalState);
        Assert.IsNull(second.StateDiff);
        Assert.AreEqual("execution-result:work-2", second.ExecutionResult!.PacketId);
    }

    [TestMethod]
    public void Another_A4F_allowed_crew_cannot_be_dispatched_by_this_bounded_executor()
    {
        var source = State();
        var crew = source.Crews.Single() with { CrewId = "crew_other" };
        var state = source with
        {
            Crews = [.. source.Crews, crew],
            Actors = source.Actors.Select(actor => actor.ActorId == "A1"
                ? actor with { CrewRefs = [.. actor.CrewRefs, crew.CrewId] } : actor).ToArray()
        };
        var before = WorldStateJsonSerializer.Serialize(state);
        var result = AsterL1ActionExecutor.Execute(state, Work() with { TargetCrewId = crew.CrewId });
        Assert.AreEqual(AsterL1DecisionStatus.Allowed, result.Decision.Status);
        Assert.AreEqual(AsterL1ExecutionDisposition.Failed, result.Disposition);
        Assert.AreSame(state, result.FinalState);
        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
        Assert.IsNull(result.StateDiff);
        Assert.AreEqual("failed", result.ActionTrace!.ExecutionStatus);
        Assert.AreEqual("a4g_work_order_subset_unsupported", result.ActionTrace.Validation.Context.Notes);
        Assert.IsEmpty(result.ExecutionResult!.TrueEffectRefs);
    }

    [TestMethod]
    public void Post_validation_failure_discards_tentative_crew_and_fails_context()
    {
        var state = State() with { SimulationSeed = 0 };
        var before = WorldStateJsonSerializer.Serialize(state);
        var candidate = Work() with { PacketId = "work-failed-seed" };
        var result = AsterL1ActionExecutor.Execute(state, candidate);
        AssertLinkage(result, candidate.PacketId);
        Assert.AreEqual(AsterL1DecisionStatus.Allowed, result.Decision.Status);
        Assert.AreEqual(AsterL1ExecutionDisposition.Failed, result.Disposition);
        Assert.AreSame(state, result.InitialState);
        Assert.AreSame(state, result.FinalState);
        Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(state));
        Assert.AreEqual("available", result.FinalState.Crews.Single().Status);
        Assert.IsNull(result.StateDiff);
        Assert.AreEqual("failed", result.ActionTrace!.ExecutionStatus);
        Assert.AreEqual("work_order", result.ActionTrace.ActionClass);
        Assert.AreEqual("failed", result.ExecutionResult!.ExecutionStatus);
        Assert.AreEqual("Core could not complete the bounded request; no world state was changed.", result.ExecutionResult.IssuerObservable.Summary);
        Assert.AreEqual("fail", result.ActionTrace.Validation.Context.Status);
        Assert.AreEqual("post_validation_state_invalid", result.ActionTrace.Validation.Context.Notes);
        Assert.AreEqual("pass", result.ActionTrace.Validation.Schema.Status);
        Assert.AreEqual("pass", result.ActionTrace.Validation.Authority.Status);
        Assert.AreEqual("pass", result.ActionTrace.Validation.Visibility.Status);
        CollectionAssert.AreEqual(new[] { "a4g_post_validation_failure" }, result.ActionTrace.EvalFlags.ToArray());
        Assert.IsEmpty(result.ActionTrace.StateDiffRefs);
        Assert.IsEmpty(result.ExecutionResult.TrueEffectRefs);
        Assert.IsEmpty(result.ExecutionResult.EventsCreated);
    }

    [TestMethod]
    public void Artifact_pair_and_state_invariants_reject_partial_or_false_success()
    {
        var state = State();
        var success = AsterL1ActionExecutor.Execute(state, Work());
        var dryRun = AsterL1ActionExecutor.Execute(state, Tool());
        Assert.AreEqual(AsterL1ExecutionDisposition.Succeeded, new AsterL1ActionExecution(
            state, success.FinalState, success.Decision, AsterL1ExecutionDisposition.Succeeded,
            success.ActionTrace, success.ExecutionResult, success.StateDiff).Disposition);
        Assert.AreEqual(AsterL1ExecutionDisposition.Succeeded, new AsterL1ActionExecution(
            state, state, dryRun.Decision, AsterL1ExecutionDisposition.Succeeded,
            dryRun.ActionTrace, dryRun.ExecutionResult, null).Disposition);
        Assert.ThrowsExactly<ArgumentException>(() => new AsterL1ActionExecution(state, state,
            success.Decision, AsterL1ExecutionDisposition.Succeeded, success.ActionTrace, null, null));
        Assert.ThrowsExactly<ArgumentException>(() => new AsterL1ActionExecution(state, state,
            success.Decision, AsterL1ExecutionDisposition.Succeeded, null, null, null));
        Assert.ThrowsExactly<ArgumentException>(() => new AsterL1ActionExecution(state, state,
            success.Decision, AsterL1ExecutionDisposition.Succeeded, success.ActionTrace, success.ExecutionResult, success.StateDiff));
        Assert.ThrowsExactly<ArgumentException>(() => new AsterL1ActionExecution(state, success.FinalState,
            success.Decision, AsterL1ExecutionDisposition.Failed, success.ActionTrace, success.ExecutionResult, success.StateDiff));
        Assert.ThrowsExactly<ArgumentException>(() => new AsterL1ActionExecution(state, success.FinalState,
            success.Decision, AsterL1ExecutionDisposition.Succeeded, success.ActionTrace, success.ExecutionResult, null));
        Assert.ThrowsExactly<ArgumentException>(() => new AsterL1ActionExecution(state, state,
            success.Decision, AsterL1ExecutionDisposition.Succeeded, success.ActionTrace, success.ExecutionResult, null));
    }

    [TestMethod]
    public void Issue_mapping_is_exhaustive_and_unique_against_current_A4F_catalog()
    {
        CollectionAssert.AreEquivalent(AsterL1IssueCatalog.Codes.ToArray(), AsterL1ActionExecutor.IssueDimensions.Keys.ToArray());
        Assert.HasCount(AsterL1IssueCatalog.Codes.Count, AsterL1ActionExecutor.IssueDimensions);
        CollectionAssert.AreEquivalent(new[] { "schema", "authority", "context", "visibility" },
            AsterL1ActionExecutor.IssueDimensions.Values.Distinct().ToArray());
    }

    [TestMethod]
    public void Bounded_diff_value_accepts_only_finite_numbers_and_strings_as_direct_primitives()
    {
        foreach (var number in new[] { 0.68, 0.73, 0.41, 0.46 })
        {
            var value = StateDiffValue.FromNumber(number);
            Assert.AreEqual(StateDiffValueKind.Number, value.Kind);
            Assert.AreEqual(number, value.Number);
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(value));
            Assert.AreEqual(JsonValueKind.Number, json.RootElement.ValueKind);
            Assert.AreEqual(value, JsonSerializer.Deserialize<StateDiffValue>(json.RootElement.GetRawText()));
        }
        var text = StateDiffValue.FromString("available");
        Assert.AreEqual(StateDiffValueKind.String, text.Kind);
        Assert.AreEqual("\"available\"", JsonSerializer.Serialize(text));
        Assert.AreEqual(text, JsonSerializer.Deserialize<StateDiffValue>("\"available\""));
        foreach (var number in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StateDiffValue.FromNumber(number));
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => StateDiffValue.FromString(null!));
        foreach (var json in new[] { "null", "true", "false", "[]", "{}", "1e999" })
        {
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<StateDiffValue>(json), json);
        }
        Assert.ThrowsExactly<InvalidOperationException>(() => JsonSerializer.Serialize(default(StateDiffValue)));
    }

    [TestMethod]
    public void Generated_evidence_is_deterministic_hidden_safe_and_directly_schema_validated()
    {
        var first = Cases();
        var second = Cases();
        var root = FindRepositoryRoot();
        var directory = Path.Combine(Path.GetTempPath(), $"ringfall-a4g-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var (name, outcome) in first)
            {
                var comparison = second[name];
                Assert.AreEqual(outcome.Disposition, comparison.Disposition);
                Assert.AreEqual(WorldStateJsonSerializer.Serialize(outcome.FinalState),
                    WorldStateJsonSerializer.Serialize(comparison.FinalState));
                Assert.IsNotNull(outcome.ActionTrace);
                Assert.IsNotNull(outcome.ExecutionResult);
                Assert.AreEqual(ArtifactJsonSerializer.Serialize(outcome.ActionTrace), ArtifactJsonSerializer.Serialize(comparison.ActionTrace));
                Assert.AreEqual(ArtifactJsonSerializer.Serialize(outcome.ExecutionResult), ArtifactJsonSerializer.Serialize(comparison.ExecutionResult));
                var traceJson = ArtifactJsonSerializer.Serialize(outcome.ActionTrace);
                var resultJson = ArtifactJsonSerializer.Serialize(outcome.ExecutionResult);
                Assert.IsFalse(traceJson.Contains("partial_success", StringComparison.Ordinal));
                Assert.IsFalse(resultJson.Contains("partial_success", StringComparison.Ordinal));
                foreach (var evidence in new[] { traceJson, resultJson })
                {
                    foreach (var forbidden in new[] { "thermalDebt", "0.41", "0.46", "hidden_effects", "public_observable", "formal_gate", "provider" })
                    {
                        Assert.IsFalse(evidence.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"{name}: {forbidden}");
                    }
                }
                ValidateSchema(root, directory, name + "-trace", traceJson,
                    "src/ringfall-contracts/schemas/traces/action-trace.schema.json");
                ValidateSchema(root, directory, name + "-result", resultJson,
                    "src/ringfall-contracts/schemas/packets/execution-result.schema.json");
                if (outcome.StateDiff is not null)
                {
                    var diffJson = ArtifactJsonSerializer.Serialize(outcome.StateDiff);
                    Assert.AreEqual(diffJson, ArtifactJsonSerializer.Serialize(comparison.StateDiff));
                    using var document = JsonDocument.Parse(diffJson);
                    Assert.AreEqual(JsonValueKind.String, document.RootElement.GetProperty("changes")[0].GetProperty("old").ValueKind);
                    Assert.AreEqual("available", document.RootElement.GetProperty("changes")[0].GetProperty("old").GetString());
                    Assert.AreEqual("unavailable", document.RootElement.GetProperty("changes")[0].GetProperty("new").GetString());
                    ValidateSchema(root, directory, name + "-diff", diffJson,
                        "src/ringfall-contracts/schemas/state/state-diff.schema.json");
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Dictionary<string, AsterL1ActionExecution> Cases()
    {
        var state = State();
        return new Dictionary<string, AsterL1ActionExecution>(StringComparer.Ordinal)
        {
            ["reroute"] = AsterL1ActionExecutor.Execute(state, Tool()),
            ["work"] = AsterL1ActionExecutor.Execute(state, Work()),
            ["rejected"] = AsterL1ActionExecutor.Execute(state, Tool() with { Mode = "execute" }),
            ["deferred"] = AsterL1ActionExecutor.Execute(state, Tool("query_heat_alarm")),
            ["failed"] = AsterL1ActionExecutor.Execute(state with { SimulationSeed = 0 }, Work())
        };
    }

    private static void ValidateSchema(string root, string directory, string name, string json, string schema)
    {
        var path = Path.Combine(directory, name + ".json");
        File.WriteAllText(path, json);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("python")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(
            "import json,sys; from jsonschema import Draft202012Validator; " +
            "schema=json.load(open(sys.argv[1],encoding='utf-8')); " +
            "instance=json.load(open(sys.argv[2],encoding='utf-8')); " +
            "Draft202012Validator.check_schema(schema); Draft202012Validator(schema).validate(instance)");
        process.StartInfo.ArgumentList.Add(Path.Combine(root, schema.Replace('/', Path.DirectorySeparatorChar)));
        process.StartInfo.ArgumentList.Add(path);
        Assert.IsTrue(process.Start(), name);
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, $"{name}: {output} {error}");
        Assert.AreEqual(string.Empty, error, name);
    }

    private static ActionValidationResult Validation(ActionTraceArtifact trace, string dimension) => dimension switch
    {
        "schema" => trace.Validation.Schema,
        "authority" => trace.Validation.Authority,
        "context" => trace.Validation.Context,
        "visibility" => trace.Validation.Visibility,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension))
    };

    private static void AssertLinkage(AsterL1ActionExecution outcome, string sourcePacketId)
    {
        var trace = outcome.ActionTrace!;
        var result = outcome.ExecutionResult!;
        Assert.AreEqual($"action-trace:{sourcePacketId}", trace.ActionTraceId);
        Assert.AreEqual(sourcePacketId, trace.SourcePacketId);
        Assert.AreEqual($"execution-result:{sourcePacketId}", result.PacketId);
        Assert.AreEqual(sourcePacketId, result.SourcePacketId);
        if (outcome.StateDiff is null)
        {
            Assert.IsEmpty(trace.StateDiffRefs);
            Assert.IsEmpty(result.TrueEffectRefs);
        }
        else
        {
            var diffId = $"state-diff:{sourcePacketId}";
            Assert.AreEqual(diffId, outcome.StateDiff.DiffId);
            Assert.AreEqual(trace.ActionTraceId, outcome.StateDiff.SourceActionTraceId);
            Assert.HasCount(1, trace.StateDiffRefs);
            Assert.AreEqual(diffId, trace.StateDiffRefs[0].RefId);
            Assert.AreEqual("state_diff", trace.StateDiffRefs[0].RefType);
            CollectionAssert.AreEqual(new[] { diffId }, result.TrueEffectRefs.ToArray());
        }
    }

    private static void AssertAllPass(ActionTraceArtifact trace)
    {
        foreach (var dimension in new[] { trace.Validation.Schema, trace.Validation.Authority, trace.Validation.Context, trace.Validation.Visibility })
        {
            Assert.AreEqual("pass", dimension.Status);
            Assert.IsNull(dimension.Notes);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "ringfall-contracts")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find RingFall schemas.");
    }
}
