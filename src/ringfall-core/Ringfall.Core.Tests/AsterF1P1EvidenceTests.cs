using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ringfall.Core.Actions;
using Ringfall.Core.Formal;
using Ringfall.Core.Projection;
using Ringfall.Core.Scenarios;
using Ringfall.Core.Snapshots;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class AsterF1P1EvidenceTests
{
    private const string ToolPacket = """
        {"packet_id":"draft_A1_tool_heat_alarm_check","packet_type":"ToolActionRequest","schema_version":"0.1",
        "issuer_id":"A1","issuer_layer":"L1","issued_at_tick":0,"source_context_id":"ctx_A1_aster_heat_t000",
        "evidence_refs":["obs-a1-t000-aster-r2-heat-alarm","event:event-t000-aster-r2-heat-alarm"],
        "tool_id":"local_grid_panel","action":"query_heat_alarm","mode":"dry_run"}
        """;
    private const string WorkPacket = """
        {"packet_id":"draft_A1_work_order_heat_alarm_inspection","packet_type":"WorkOrderRequest","schema_version":"0.1",
        "issuer_id":"A1","issuer_layer":"L1","issued_at_tick":0,"source_context_id":"ctx_A1_aster_heat_t000",
        "evidence_refs":["obs-a1-t000-aster-r2-heat-alarm","event:event-t000-aster-r2-heat-alarm"],
        "target_crew_id":"crew_aster_repair_02","target_location":"Aster/Grid-Spine-03",
        "task_type":"inspect_and_patch","priority":"high"}
        """;

    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (Directory.Exists(Path.Combine(current.FullName, "src", "ringfall-contracts"))) return current.FullName;
        throw new DirectoryNotFoundException("RingFall root not found.");
    }

    private static byte[] State() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "aster-minimal-world-state.json"));
    private static byte[] Pulse() => File.ReadAllBytes(Path.Combine(Root(), "src", "ringfall-brain", "examples", "aster-a1-pulse.example.json"));
    private static byte[] Context() => File.ReadAllBytes(Path.Combine(Root(), "src", "ringfall-brain", "examples", "aster-a1-context.example.json"));
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static JsonNode Packet(string source) => JsonNode.Parse(source)!;
    private static JsonDocument Evidence(JsonNode packet) => JsonDocument.Parse(
        AsterF1P1EvidenceBridge.Produce(State(), Bytes(packet.ToJsonString()), Pulse(), Context()));

    [TestMethod]
    public void Accepted_A4H_requests_bind_to_core_and_are_byte_deterministic()
    {
        foreach (var packet in new[] { ToolPacket, WorkPacket })
        {
            var state = State();
            var before = WorldStateJsonSerializer.Serialize(InitialStateLoader.LoadFromJson(Encoding.UTF8.GetString(state)));
            var first = AsterF1P1EvidenceBridge.Produce(state, Bytes(packet), Pulse(), Context());
            var second = AsterF1P1EvidenceBridge.Produce(state, Bytes(packet), Pulse(), Context());
            Assert.AreEqual(first, second);
            using var doc = JsonDocument.Parse(first);
            var root = doc.RootElement;
            CollectionAssert.AreEqual(new[] { "record_type", "evidence_version", "family_id", "family_version",
                "input_versions", "inputs", "state", "binding", "core", "links", "facts", "completeness",
                "unsupported_predicates", "diagnostics", "schema_validation", "formal_proof", "mutation_authorized" },
                root.EnumerateObject().Select(item => item.Name).ToArray());
            Assert.AreEqual("0.1", root.GetProperty("evidence_version").GetString());
            Assert.AreEqual("complete", root.GetProperty("completeness").GetString());
            Assert.AreEqual("not_run", root.GetProperty("schema_validation").GetString());
            Assert.AreEqual("not_run", root.GetProperty("formal_proof").GetString());
            Assert.IsFalse(root.GetProperty("mutation_authorized").GetBoolean());
            Assert.AreEqual("allowed", root.GetProperty("core").GetProperty("status").GetString());
            Assert.AreEqual(2, root.GetProperty("binding").GetProperty("evidence_ref_count").GetInt32());
            Assert.AreEqual(packet == ToolPacket ? "deferred" : "succeeded",
                root.GetProperty("core").GetProperty("disposition").GetString());
            Assert.AreEqual(packet != ToolPacket, root.GetProperty("state").GetProperty("changed").GetBoolean());
            Assert.AreEqual(packet == ToolPacket ? JsonValueKind.Null : JsonValueKind.Object,
                root.GetProperty("links").GetProperty("diff").ValueKind);
            var target = packet == ToolPacket
                ? root.GetProperty("facts").GetProperty("tool")
                : root.GetProperty("facts").GetProperty("work_order");
            Assert.IsTrue(target.GetProperty(packet == ToolPacket ? "actor_tool_referenced" : "actor_crew_referenced").GetBoolean());
            Assert.AreEqual("available", target.GetProperty(packet == ToolPacket ? "status" : "crew_status").GetString());
            Assert.AreNotEqual(0, target.GetProperty(packet == ToolPacket ? "system_refs" : "crew_system_refs").GetArrayLength());
            Assert.AreEqual(before, WorldStateJsonSerializer.Serialize(InitialStateLoader.LoadFromJson(Encoding.UTF8.GetString(state))));
            foreach (var secret in new[] { "thermalDebt", "debtLevel", "0.41", "0.52", "rationale", "hidden_effects" })
                Assert.IsFalse(first.Contains(secret, StringComparison.OrdinalIgnoreCase), secret);
        }
    }

    [TestMethod]
    public void Unreferenced_targets_keep_core_rejection_but_suppress_resolved_target_facts()
    {
        var source = InitialStateLoader.LoadFromJson(Encoding.UTF8.GetString(State()));
        foreach (var (kind, packet, issue, unsupported) in new[]
        {
            ("tool", ToolPacket, AsterL1IssueCatalog.ToolNotReferenced, "tool_surface"),
            ("work_order", WorkPacket, AsterL1IssueCatalog.CrewNotReferenced, "crew_target")
        })
        {
            var altered = source with
            {
                Actors = source.Actors.Select(actor => actor.ActorId != "A1" ? actor : actor with
                {
                    ToolRefs = kind == "tool"
                        ? actor.ToolRefs.Where(id => id != "local_grid_panel").ToArray()
                        : actor.ToolRefs,
                    CrewRefs = kind == "work_order" ? [] : actor.CrewRefs
                }).ToArray()
            };
            // The actual loader and Core projection, rather than a copied fixture context,
            // establish the altered actor-local reference set for this negative case.
            var loaded = InitialStateLoader.LoadFromJson(WorldStateJsonSerializer.Serialize(altered));
            var stateBytes = Bytes(WorldStateJsonSerializer.Serialize(loaded));
            var contextBytes = ActorContextProjectionJsonSerializer.SerializeToUtf8(loaded, "A1");
            using var doc = JsonDocument.Parse(AsterF1P1EvidenceBridge.Produce(
                stateBytes, Bytes(packet), Pulse(), contextBytes));
            var root = doc.RootElement;
            Assert.AreEqual("denied", root.GetProperty("core").GetProperty("status").GetString(), kind);
            CollectionAssert.AreEqual(new[] { issue }, root.GetProperty("core").GetProperty("issues")
                .EnumerateArray().Select(item => item.GetString()).ToArray(), kind);
            Assert.AreEqual("unsupported", root.GetProperty("completeness").GetString(), kind);
            CollectionAssert.AreEqual(new[] { unsupported }, root.GetProperty("unsupported_predicates")
                .EnumerateArray().Select(item => item.GetString()).ToArray(), kind);
            var target = root.GetProperty("facts").GetProperty(kind);
            Assert.IsFalse(target.GetProperty(kind == "tool" ? "actor_tool_referenced" : "actor_crew_referenced").GetBoolean(), kind);
            foreach (var field in kind == "tool"
                ? new[] { "tool_id", "status", "system_refs", "supported_actions" }
                : new[] { "crew_id", "crew_status", "assigned_actor_id", "crew_home_sector_id", "crew_system_refs" })
                Assert.AreEqual(JsonValueKind.Null, target.GetProperty(field).ValueKind, $"{kind}: {field}");
            if (kind == "work_order")
            {
                var crewField = target.GetProperty("target_crew_id");
                Assert.AreEqual("value", crewField.GetProperty("presence").GetString());
                Assert.AreEqual(JsonValueKind.Null, crewField.GetProperty("value").ValueKind);
                Assert.AreEqual("unsupported", crewField.GetProperty("coverage").GetString());
            }
            var serialized = root.GetRawText();
            Assert.IsFalse(serialized.Contains(kind == "tool" ? "local_grid_panel" : "crew_aster_repair_02",
                StringComparison.Ordinal), kind);
            Assert.IsFalse(serialized.Contains("\"available\"", StringComparison.Ordinal), kind);
            Assert.IsFalse(serialized.Contains("\"R2\"", StringComparison.Ordinal), kind);
            Assert.IsFalse(serialized.Contains("\"R5\"", StringComparison.Ordinal), kind);
        }
    }

    [TestMethod]
    public void Tool_fact_matrix_preserves_absent_and_core_denied_values_without_false_proof()
    {
        var packet = Packet(ToolPacket);
        packet["action"] = "dry_run_reroute";
        packet["requires_dry_run"] = false;
        packet["arguments"] = JsonNode.Parse("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\",\"load_fraction\":0.20}");
        using (var doc = Evidence(packet))
        {
            var root = doc.RootElement;
            Assert.AreEqual("complete", root.GetProperty("completeness").GetString());
            Assert.AreEqual("denied", root.GetProperty("core").GetProperty("status").GetString());
            CollectionAssert.Contains(root.GetProperty("core").GetProperty("issues").EnumerateArray()
                .Select(item => item.GetString()).ToArray(), "tool_requires_dry_run_conflict");
            var rows = root.GetProperty("facts").GetProperty("tool_arguments");
            CollectionAssert.AreEqual(new[] { "branch_id", "from_branch", "to_branch", "asset_id", "load_fraction" },
                rows.EnumerateArray().Select(row => row.GetProperty("name").GetString()).ToArray());
            Assert.AreEqual(0.20, rows[4].GetProperty("value").GetDouble());
            Assert.IsFalse(root.GetProperty("facts").GetProperty("tool").GetProperty("requires_dry_run")
                .GetProperty("value").GetBoolean());
        }
        packet["requires_dry_run"] = true;
        foreach (var fraction in new[] { -0.01, 0.0, 0.20, 0.21 })
        {
            packet["arguments"]!["load_fraction"] = fraction;
            using var doc = Evidence(packet);
            Assert.AreEqual("complete", doc.RootElement.GetProperty("completeness").GetString());
            Assert.AreEqual(fraction, doc.RootElement.GetProperty("facts").GetProperty("tool_arguments")[4]
                .GetProperty("value").GetDouble());
        }
        packet["arguments"] = JsonNode.Parse("{\"from_branch\":\"Aster-G3\",\"to_branch\":\"Aster-G4\"}");
        using (var doc = Evidence(packet))
        {
            Assert.AreEqual("absent", doc.RootElement.GetProperty("facts").GetProperty("tool_arguments")[4]
                .GetProperty("presence").GetString());
            Assert.AreEqual("complete", doc.RootElement.GetProperty("completeness").GetString());
        }
        foreach (var raw in new[] { "null", "\"secret-value\"", "true", "1e999" })
        {
            packet["arguments"] = JsonNode.Parse("{\"load_fraction\":" + raw + "}");
            var json = packet.ToJsonString();
            using var doc = JsonDocument.Parse(AsterF1P1EvidenceBridge.Produce(State(), Bytes(json), Pulse(), Context()));
            Assert.AreEqual("unsupported", doc.RootElement.GetProperty("completeness").GetString());
            Assert.AreEqual("unsupported", doc.RootElement.GetProperty("facts").GetProperty("tool_arguments")[4]
                .GetProperty("coverage").GetString());
            Assert.IsFalse(doc.RootElement.GetRawText().Contains("secret-value", StringComparison.Ordinal));
        }
        packet["arguments"] = JsonNode.Parse("{\"secret-value\":1}");
        using (var doc = Evidence(packet))
        {
            Assert.AreEqual("unsupported", doc.RootElement.GetProperty("completeness").GetString());
            Assert.IsFalse(doc.RootElement.GetRawText().Contains("secret-value", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Work_options_and_binding_reject_unmodeled_or_untrusted_evidence()
    {
        var packet = Packet(WorkPacket);
        packet["authorized_resources"] = new JsonArray("secret-resource");
        using (var doc = Evidence(packet))
        {
            Assert.AreEqual("unsupported", doc.RootElement.GetProperty("completeness").GetString());
            Assert.IsFalse(doc.RootElement.GetRawText().Contains("secret-resource", StringComparison.Ordinal));
        }
        packet = Packet(WorkPacket);
        packet["priority"] = "arbitrary-priority";
        using (var doc = Evidence(packet))
        {
            Assert.AreEqual("unsupported", doc.RootElement.GetProperty("completeness").GetString());
            Assert.IsFalse(doc.RootElement.GetRawText().Contains("arbitrary-priority", StringComparison.Ordinal));
        }
        packet = Packet(ToolPacket);
        foreach (var (key, value, code) in new[]
        {
            ("issuer_id", "A2", "context_mismatch"), ("issued_at_tick", "5", "context_mismatch"),
            ("source_context_id", "bogus", "context_mismatch"), ("evidence_refs", "[]", "context_mismatch")
        })
        {
            var variant = Packet(ToolPacket);
            variant[key] = JsonNode.Parse(value.StartsWith('[') || char.IsDigit(value[0]) ? value : "\"" + value + "\"");
            Assert.AreEqual(code, Assert.ThrowsExactly<AsterF1P1Exception>(() => Evidence(variant)).Code);
        }
        packet["rationale"] = "thermalDebt secret narrative";
        using (var doc = Evidence(packet))
            Assert.IsFalse(doc.RootElement.GetRawText().Contains("secret narrative", StringComparison.Ordinal));
        packet["visibility_intent"] = "public";
        using (var doc = Evidence(packet))
            Assert.AreEqual("unsupported", doc.RootElement.GetProperty("completeness").GetString());
        packet = Packet(ToolPacket);
        packet["arguments"] = null;
        Assert.AreEqual("input_invalid", Assert.ThrowsExactly<AsterF1P1Exception>(() => Evidence(packet)).Code);
        var badContext = JsonNode.Parse(Encoding.UTF8.GetString(Context()))!;
        badContext["observations"]![0]!["signal"] = "thermalDebt: 0.41";
        Assert.AreEqual("context_mismatch", Assert.ThrowsExactly<AsterF1P1Exception>(() =>
            AsterF1P1EvidenceBridge.Produce(State(), Bytes(ToolPacket), Pulse(), Bytes(badContext.ToJsonString()))).Code);
        var wrongDraft = JsonNode.Parse(Encoding.UTF8.GetString(Pulse()))!;
        wrongDraft["requested_packets"]![1]!["draft_ref"] = "wrong";
        Assert.AreEqual("context_mismatch", Assert.ThrowsExactly<AsterF1P1Exception>(() =>
            AsterF1P1EvidenceBridge.Produce(State(), Bytes(ToolPacket), Bytes(wrongDraft.ToJsonString()), Context())).Code);
    }

    [TestMethod]
    public void Internal_bridge_covers_failed_work_and_rejects_broken_links()
    {
        var source = InitialStateLoader.LoadFromJson(Encoding.UTF8.GetString(State()));
        var candidate = new AsterL1WorkOrderCandidate
        {
            PacketId = "internal-work", PacketType = "WorkOrderRequest", SchemaVersion = "0.1",
            IssuerId = "A1", IssuerLayer = "L1", IssuedAtTick = 0, SourceContextId = "ctx_A1_aster_heat_t000",
            TargetCrewId = "crew_aster_repair_02", TargetLocation = "Aster/Grid-Spine-03",
            TaskType = "inspect_and_patch", Priority = "high"
        };
        var invalidForLoader = source with { SimulationSeed = 0 };
        var failure = AsterL1ActionExecutor.Execute(invalidForLoader, candidate);
        Assert.AreEqual(AsterL1ExecutionDisposition.Failed, failure.Disposition);
        AsterF1P1EvidenceBridge.VerifyExecution(invalidForLoader, candidate, failure);
        var identity = candidate with { PacketId = " " };
        var identityFailure = AsterL1ActionExecutor.Execute(source, identity);
        Assert.AreEqual(AsterL1ExecutionDisposition.IdentityRejected, identityFailure.Disposition);
        AsterF1P1EvidenceBridge.VerifyExecution(source, identity, identityFailure);
        var tool = JsonSerializer.Deserialize<AsterL1ToolActionCandidate>(ToolPacket,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        var deferred = AsterL1ActionExecutor.Execute(source, tool);
        AsterF1P1EvidenceBridge.VerifyExecution(source, tool, deferred);
        var rejected = AsterL1ActionExecutor.Execute(source, tool with { Mode = "execute" });
        AsterF1P1EvidenceBridge.VerifyExecution(source, tool with { Mode = "execute" }, rejected);
        var success = AsterL1ActionExecutor.Execute(source, candidate);
        AsterF1P1EvidenceBridge.VerifyExecution(source, candidate, success);
        var malformedState = success.FinalState with { SimulationSeed = 100 };
        var falseEffect = new AsterL1ActionExecution(source, malformedState, success.Decision,
            success.Disposition, success.ActionTrace, success.ExecutionResult, success.StateDiff);
        Assert.AreEqual("evidence_mismatch", Assert.ThrowsExactly<AsterF1P1Exception>(() =>
            AsterF1P1EvidenceBridge.VerifyExecution(source, candidate, falseEffect)).Code);
        var wrongTrace = success.ActionTrace! with { SourcePacketId = "not-this-packet" };
        var mismatch = new AsterL1ActionExecution(source, success.FinalState, success.Decision,
            success.Disposition, wrongTrace, success.ExecutionResult, success.StateDiff);
        Assert.AreEqual("evidence_mismatch", Assert.ThrowsExactly<AsterF1P1Exception>(() =>
            AsterF1P1EvidenceBridge.VerifyExecution(source, candidate, mismatch)).Code);
    }

    [TestMethod]
    public void Headless_opt_in_command_emits_only_record_or_sanitized_error()
    {
        var root = Root();
        var directory = Path.Combine(Path.GetTempPath(), "ringfall-p1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var statePath = Path.Combine(directory, "state.json");
            var candidatePath = Path.Combine(directory, "request.json");
            var pulsePath = Path.Combine(directory, "pulse.json");
            var contextPath = Path.Combine(directory, "context.json");
            File.WriteAllBytes(statePath, State());
            File.WriteAllBytes(candidatePath, Bytes(ToolPacket));
            File.WriteAllBytes(pulsePath, Pulse());
            File.WriteAllBytes(contextPath, Context());
            (int Exit, string Stdout, string Stderr) Invoke(params string[] arguments)
            {
                var start = new ProcessStartInfo("dotnet")
                {
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, WorkingDirectory = root
                };
                start.ArgumentList.Add("run");
                start.ArgumentList.Add("--no-restore");
                start.ArgumentList.Add("--project");
                start.ArgumentList.Add(Path.Combine(root, "src", "ringfall-core", "Ringfall.Headless", "Ringfall.Headless.csproj"));
                start.ArgumentList.Add("--");
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return (process.ExitCode, stdout, stderr);
            }
            var arguments = new[] { "aster-f1-evidence", "--state", statePath, "--candidate", candidatePath,
                "--pulse", pulsePath, "--context", contextPath };
            var success = Invoke(arguments);
            Assert.AreEqual(0, success.Exit, success.Stderr);
            Assert.AreEqual(string.Empty, success.Stderr);
            Assert.EndsWith("\n", success.Stdout);
            using (var doc = JsonDocument.Parse(success.Stdout))
                Assert.AreEqual("complete", doc.RootElement.GetProperty("completeness").GetString());
            File.WriteAllBytes(candidatePath, Bytes(WorkPacket));
            var work = Invoke(arguments);
            Assert.AreEqual(0, work.Exit, work.Stderr);
            using (var doc = JsonDocument.Parse(work.Stdout))
            {
                Assert.AreEqual("succeeded", doc.RootElement.GetProperty("core").GetProperty("disposition").GetString());
                Assert.AreEqual("crew_status_available_to_unavailable", doc.RootElement.GetProperty("links")
                    .GetProperty("diff").GetProperty("change_class").GetString());
            }
            File.WriteAllBytes(candidatePath, Bytes(ToolPacket.Replace("ctx_A1_aster_heat_t000", "secret-invalid", StringComparison.Ordinal)));
            var failure = Invoke(arguments);
            Assert.AreEqual(4, failure.Exit);
            Assert.AreEqual(string.Empty, failure.Stdout);
            Assert.AreEqual("p1_error:context_mismatch" + Environment.NewLine, failure.Stderr);
            File.WriteAllBytes(candidatePath, Bytes(ToolPacket.Replace("\"packet_type\":\"ToolActionRequest\"",
                "\"packet_type\":\"SceneActionPacket\"", StringComparison.Ordinal)));
            var kind = Invoke(arguments);
            Assert.AreEqual(3, kind.Exit);
            Assert.AreEqual(string.Empty, kind.Stdout);
            Assert.AreEqual("p1_error:unsupported_packet_kind" + Environment.NewLine, kind.Stderr);
            File.WriteAllBytes(candidatePath, Bytes(ToolPacket));
            var invalidState = JsonNode.Parse(Encoding.UTF8.GetString(State()))!;
            invalidState["schemaVersion"] = "0.2";
            File.WriteAllBytes(statePath, Bytes(invalidState.ToJsonString()));
            var invalid = Invoke(arguments);
            Assert.AreEqual(3, invalid.Exit);
            Assert.AreEqual(string.Empty, invalid.Stdout);
            Assert.AreEqual("p1_error:state_invalid" + Environment.NewLine, invalid.Stderr);
            var usage = Invoke("aster-f1-evidence", "--state", statePath);
            Assert.AreEqual(2, usage.Exit);
            Assert.AreEqual(string.Empty, usage.Stdout);
            Assert.AreEqual("p1_error:usage_invalid" + Environment.NewLine, usage.Stderr);
            var help = Invoke("--help");
            Assert.AreEqual(0, help.Exit);
            StringAssert.Contains(help.Stdout, "aster-f1-evidence");
            Assert.AreEqual(0, Invoke("--version").Exit);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
