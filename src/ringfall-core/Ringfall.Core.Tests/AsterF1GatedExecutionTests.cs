using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ringfall.Core.Actions;
using Ringfall.Core.Formal;
using Ringfall.Core.Snapshots;

namespace Ringfall.Core.Tests;

[TestClass]
public sealed class AsterF1GatedExecutionTests
{
    private const string WorkPacket = """
        {"packet_id":"draft_A1_work_order_heat_alarm_inspection","packet_type":"WorkOrderRequest","schema_version":"0.1",
        "issuer_id":"A1","issuer_layer":"L1","issued_at_tick":0,"source_context_id":"ctx_A1_aster_heat_t000",
        "evidence_refs":["obs-a1-t000-aster-r2-heat-alarm","event:event-t000-aster-r2-heat-alarm"],
        "target_crew_id":"crew_aster_repair_02","target_location":"Aster/Grid-Spine-03",
        "task_type":"inspect_and_patch","priority":"high"}
        """;

    private const string ToolPacket = """
        {"packet_id":"draft_A1_tool_heat_alarm_check","packet_type":"ToolActionRequest","schema_version":"0.1",
        "issuer_id":"A1","issuer_layer":"L1","issued_at_tick":0,"source_context_id":"ctx_A1_aster_heat_t000",
        "evidence_refs":["obs-a1-t000-aster-r2-heat-alarm","event:event-t000-aster-r2-heat-alarm"],
        "tool_id":"local_grid_panel","action":"query_heat_alarm","mode":"dry_run"}
        """;

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "ringfall-contracts"))) return dir.FullName;
        throw new DirectoryNotFoundException();
    }

    private static AsterF1GatedExecution.Preflight Prepare(string packet = WorkPacket)
    {
        var root = Root();
        return AsterF1GatedExecution.Prepare(
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "aster-minimal-world-state.json")),
            Encoding.UTF8.GetBytes(packet),
            File.ReadAllBytes(Path.Combine(root, "src", "ringfall-brain", "examples", "aster-a1-pulse.example.json")),
            File.ReadAllBytes(Path.Combine(root, "src", "ringfall-brain", "examples", "aster-a1-context.example.json")));
    }

    private static string WitnessText(AsterF1GatedProof.ClosedInstance instance) =>
        Encoding.UTF8.GetString(instance.ModelPrefix) + "\ndeclare "
        + string.Join(", ", instance.Nodes.Order(StringComparer.Ordinal)) + ".\n"
        + string.Join("\n", instance.WitnessStatements.Order(StringComparer.Ordinal)) + "\n";

    [TestMethod]
    public void Preflight_has_no_execution_fields_and_preserves_original_state()
    {
        var preflight = Prepare();
        using var doc = JsonDocument.Parse(preflight.Json);
        var root = doc.RootElement;
        Assert.AreEqual("AsterF1Preflight", root.GetProperty("record_type").GetString());
        Assert.AreEqual("1.0", root.GetProperty("contract_version").GetString());
        Assert.AreEqual("not_run", root.GetProperty("execution").GetString());
        Assert.AreEqual("not_run", root.GetProperty("schema_validation").GetString());
        Assert.AreEqual("allowed", root.GetProperty("core").GetProperty("status").GetString());
        Assert.AreEqual(0, root.GetProperty("core").GetProperty("issues").GetArrayLength());
        Assert.AreEqual("complete", root.GetProperty("completeness").GetString());
        Assert.IsFalse(root.TryGetProperty("state", out _));
        Assert.IsFalse(root.TryGetProperty("links", out _));
        Assert.IsFalse(root.GetProperty("core").TryGetProperty("disposition", out _));
        Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
        Assert.IsTrue(preflight.Input.State.Crews.Any(crew => crew.CrewId == "crew_aster_repair_02" && crew.Status == "available"));
    }

    [TestMethod]
    public void Unsupported_fact_blocks_without_execution()
    {
        var packet = WorkPacket.Replace("\"priority\":\"high\"", "\"priority\":\"high\",\"public_visibility\":\"yes\"", StringComparison.Ordinal);
        var preflight = Prepare(packet);
        using var doc = JsonDocument.Parse(preflight.Json);
        Assert.AreEqual("unsupported", doc.RootElement.GetProperty("completeness").GetString());
        Assert.AreEqual("not_run", doc.RootElement.GetProperty("execution").GetString());
        Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
    }

    [TestMethod]
    public void Untrusted_receipt_cannot_reach_executor_or_publish_a_bundle()
    {
        var preflight = Prepare();
        var output = Path.Combine(Path.GetTempPath(), "opencode", "gate-reject-" + Guid.NewGuid().ToString("N"));
        var instance = AsterF1GatedProof.Map(preflight, File.ReadAllBytes(Path.Combine(Root(),
            "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem")));
        Assert.ThrowsExactly<AsterF1P1Exception>(() => AsterF1GatedExecution.Publish(preflight,
            "{}"u8.ToArray(), output, instance, "Model is consistent\n"u8.ToArray(), []));
        Assert.IsFalse(Directory.Exists(output));
        Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
    }

    [TestMethod]
    public void Publication_failure_after_tentative_execute_keeps_authoritative_state_unpublished()
    {
        var preflight = Prepare();
        var execution = AsterL1ActionExecutor.Execute(preflight.Input.State, (AsterL1WorkOrderCandidate)preflight.Input.Candidate);
        AsterF1P1EvidenceBridge.VerifyExecution(preflight.Input.State, preflight.Input.Candidate, execution);
        Assert.IsNotNull(execution.StateDiff);
        var output = Path.Combine(Path.GetTempPath(), "opencode", "gate-existing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            File.WriteAllText(Path.Combine(output, "original.txt"), "sentinel");
            Assert.ThrowsExactly<AsterF1P1Exception>(() => AsterF1GatedExecution.PublishBundle(preflight, "{}"u8.ToArray(), execution, output));
            Assert.AreEqual("sentinel", File.ReadAllText(Path.Combine(output, "original.txt")));
            Assert.IsFalse(File.Exists(Path.Combine(output, "world-state.json")));
            Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
        }
        finally { Directory.Delete(output, true); }
    }

    [TestMethod]
    public void Closed_witness_requires_every_statement_once_and_rejects_replay_or_extras()
    {
        var preflight = Prepare();
        var instance = AsterF1GatedProof.Map(preflight, File.ReadAllBytes(Path.Combine(Root(),
            "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem")));
        Assert.AreEqual(instance.Sha256, AsterF1GatedExecution.Hash(instance.Bytes));
        var witness = WitnessText(instance);
        Assert.IsTrue(AsterF1GatedProof.WitnessIsClosed(Encoding.UTF8.GetBytes(witness), instance));
        foreach (var altered in new[] { witness + "extraPredicate(r1).\n", witness + "declare a1, r1, s1, c1, loc1.\n",
            witness + instance.WitnessStatements.First() + "\n", witness + "default !toolRef(*, *).\n",
            witness + "% unexpected comment\n", witness.Replace("crewAvailable(c1).", "!crewAvailable(c1)."),
            witness.Replace("crewRef(a1, c1).\n", "", StringComparison.Ordinal),
            witness.Replace("fractionOrZero(r1): 0.0.", "fractionOrZero(r1): 0.1.", StringComparison.Ordinal),
            witness.Replace("class F1Actor {", "class F1Actor { % changed", StringComparison.Ordinal),
            witness.Replace("\ndeclare ", "\n% unexpected prefix\ndeclare ", StringComparison.Ordinal),
            witness.Replace("\ndeclare ", "\n\ndeclare ", StringComparison.Ordinal), witness + "\n" })
            Assert.IsFalse(AsterF1GatedProof.WitnessIsClosed(Encoding.UTF8.GetBytes(altered), instance));
    }

    [TestMethod]
    public void Real_refinery_work_witness_diagnostic_is_closed_only_with_exact_pinned_prefix()
    {
        // Concrete suffix from a private parent capture of Refinery 0.3.0 generate -o -;
        // the SHA binds this test sample to the captured raw stdout without moving it into Git.
        var model = File.ReadAllBytes(Path.Combine(Root(), "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem"));
        var instance = AsterF1GatedProof.Map(Prepare(), model);
        Assert.AreEqual("dfde1b953fb241dd7e6d1d1d271c0b67fa595a35619ad6d7eed45917d35739ef", instance.Sha256);
        var concrete = new[]
        {
            "declare a1, r1, s1, c1, loc1.",
            "!exists(F1Actor::new).", "!exists(F1Tool::new).", "!exists(F1Crew::new).",
            "!exists(F1Sector::new).", "!exists(F1Action::new).", "!exists(F1Location::new).",
            "!exists(F1Request::new).", "F1Actor(a1).", "F1Request(r1).", "F1Sector(s1).",
            "F1Crew(c1).", "F1Location(loc1).", "default !toolRef(*, *).", "default !crewRef(*, *).",
            "crewRef(a1, c1).", "default !actorHomeSector(*, *).", "actorHomeSector(a1, s1).",
            "default !supports(*, *).", "default !assignedActor(*, *).", "assignedActor(c1, a1).",
            "default !crewHomeSector(*, *).", "crewHomeSector(c1, s1).", "default !issuer(*, *).",
            "issuer(r1, a1).", "default !targetTool(*, *).", "default !targetCrew(*, *).",
            "targetCrew(r1, c1).", "default !action(*, *).", "default !targetLocation(*, *).",
            "targetLocation(r1, loc1).", "crewAvailable(c1).", "!modeExecute(r1).",
            "!requiresDryRunFalse(r1).", "!missingRequiredArgument(r1).", "!fractionPresent(r1).",
            "fractionOrZero(r1): 0.0."
        };
        var raw = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(model) + "\n" + string.Join("\n", concrete) + "\n");
        Assert.AreEqual("a6dfd345ad15df841264c715447bf57f6b88e26cc0d6d97b2403af488ccd9649",
            AsterF1GatedExecution.Hash(raw));
        Assert.IsTrue(AsterF1GatedProof.WitnessIsClosed(raw, instance));
        var changedPrefix = (byte[])raw.Clone();
        changedPrefix[0] ^= 1;
        Assert.IsFalse(AsterF1GatedProof.WitnessIsClosed(changedPrefix, instance));
        Assert.IsFalse(AsterF1GatedProof.WitnessIsClosed(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(raw) + "crewAvailable(c1).\n"), instance));
        Assert.IsFalse(AsterF1GatedProof.WitnessIsClosed(Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(raw) + "extraPredicate(r1).\n"), instance));
    }

    [TestMethod]
    public void Tool_and_work_order_instances_are_distinct_and_cannot_share_a_witness()
    {
        var model = File.ReadAllBytes(Path.Combine(Root(), "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem"));
        var work = AsterF1GatedProof.Map(Prepare(), model);
        var tool = AsterF1GatedProof.Map(Prepare(ToolPacket), model);
        Assert.AreNotEqual(work.Sha256, tool.Sha256);
        StringAssert.Contains(Encoding.UTF8.GetString(tool.Bytes), "toolAvailable(t1): true.");
        var witness = WitnessText(tool);
        Assert.IsTrue(AsterF1GatedProof.WitnessIsClosed(Encoding.UTF8.GetBytes(witness), tool));
        Assert.IsFalse(AsterF1GatedProof.WitnessIsClosed(Encoding.UTF8.GetBytes(witness), work));
    }

    [TestMethod]
    public void Swapped_or_replayed_parent_receipt_is_rejected_before_execution()
    {
        var preflight = Prepare();
        var instance = AsterF1GatedProof.Map(preflight, File.ReadAllBytes(Path.Combine(Root(),
            "src", "ringfall-core", "formal", "aster-f1-v0.1", "aster-f1-v0.1.problem")));
        var witness = Encoding.UTF8.GetBytes(WitnessText(instance));
        var check = "Model is consistent\n"u8.ToArray();
        var invocation = new string('a', 32);
        var report = JsonNode.Parse(JsonSerializer.Serialize(new
        {
            record_type = "AsterF1PreflightSolverReport", version = "1.0", invocation_id = invocation,
            operator_anchor_sha256 = new string('1', 64), source_aggregate = new string('2', 64),
            preflight_sha256 = preflight.Sha256,
            packet_id_sha256 = AsterF1GatedExecution.Hash(Encoding.UTF8.GetBytes(preflight.PacketId)),
            source_context_id_sha256 = AsterF1GatedExecution.Hash(Encoding.UTF8.GetBytes(preflight.ContextId)),
            executed_image = "ghcr.io/graphs4value/refinery-cli@sha256:5d7eacdef0ddfb98e264cad405badf96753c68203e498a12d91331a0aa96ad57",
            inspected_platform = "linux/amd64", engine = "28.0.1 linux/amd64", instance_sha256 = instance.Sha256,
            input_sha256 = new { state = AsterF1GatedExecution.Hash(preflight.Input.StateBytes),
                candidate = AsterF1GatedExecution.Hash(preflight.Input.CandidateBytes),
                pulse = AsterF1GatedExecution.Hash(preflight.Input.PulseBytes),
                context = AsterF1GatedExecution.Hash(preflight.Input.ContextBytes) },
            check = new { invocation_id = invocation, instance_sha256 = instance.Sha256, exit_code = 0,
                stdout_sha256 = AsterF1GatedExecution.Hash(check), stderr_sha256 = AsterF1GatedExecution.Hash([]),
                stdout_bytes = check.Length, stderr_bytes = 0 },
            generated = new { invocation_id = invocation, instance_sha256 = instance.Sha256, exit_code = 0,
                stdout_sha256 = AsterF1GatedExecution.Hash(witness), stderr_sha256 = AsterF1GatedExecution.Hash([]),
                stdout_bytes = witness.Length, stderr_bytes = 0 },
            witness_closed = true, verdict = "valid", agreement = "agreed", diagnostics = Array.Empty<string>()
        }))!;
        var output = Path.Combine(Path.GetTempPath(), "opencode", "gate-replay-" + Guid.NewGuid().ToString("N"));
        foreach (var mutate in new Action<JsonNode>[]
        {
            node => node["check"]!["invocation_id"] = new string('b', 32),
            node => node["generated"]!["instance_sha256"] = new string('c', 64),
            node => node["generated"]!["stdout_sha256"] = AsterF1GatedExecution.Hash(check),
            node => node["check"]!["stdout_bytes"] = witness.Length
        })
        {
            var altered = report.DeepClone();
            mutate(altered);
            Assert.ThrowsExactly<AsterF1P1Exception>(() => AsterF1GatedExecution.Publish(preflight,
                Encoding.UTF8.GetBytes(altered.ToJsonString()), output, instance, check, witness));
            Assert.IsFalse(Directory.Exists(output));
        }
        Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
    }

    [TestMethod]
    public void Staging_write_failure_after_tentative_execute_leaves_no_bundle_or_owned_stage()
    {
        var preflight = Prepare();
        var execution = AsterL1ActionExecutor.Execute(preflight.Input.State, (AsterL1WorkOrderCandidate)preflight.Input.Candidate);
        var parent = Path.Combine(Path.GetTempPath(), "opencode");
        var output = Path.Combine(parent, "gate-stage-failure-" + Guid.NewGuid().ToString("N"));
        var stages = Directory.GetDirectories(parent, ".ringfall-gate-*").ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.ThrowsExactly<AsterF1P1Exception>(() => AsterF1GatedExecution.PublishBundle(preflight,
            "{}"u8.ToArray(), execution, output, _ => throw new AsterF1P1Exception("injected_stage_write_failure")));
        Assert.IsFalse(Directory.Exists(output));
        CollectionAssert.AreEquivalent(stages.ToArray(), Directory.GetDirectories(parent, ".ringfall-gate-*"));
        Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
        Assert.IsTrue(execution.FinalState.Crews.Any(crew => crew.CrewId == "crew_aster_repair_02" && crew.Status == "unavailable"));
    }

    [TestMethod]
    public void Failed_success_output_after_publication_requires_destination_reconciliation()
    {
        var preflight = Prepare();
        var execution = AsterL1ActionExecutor.Execute(preflight.Input.State, (AsterL1WorkOrderCandidate)preflight.Input.Candidate);
        var output = Path.Combine(Path.GetTempPath(), "opencode", "gate-published-" + Guid.NewGuid().ToString("N"));
        var record = Path.Combine(Path.GetTempPath(), "opencode", "gate-published-record-" + Guid.NewGuid().ToString("N"));
        try
        {
            AsterF1GatedExecution.PublishBundle(preflight, "{}"u8.ToArray(), execution, output);
            var error = Assert.ThrowsExactly<AsterF1P1Exception>(() => AsterF1GatedExecution.EmitPublished(
                output, record, "invocation-one", "source-one", _ => throw new IOException("broken stdout")));
            Assert.AreEqual("published_output_failed", error.Code);
            Assert.AreEqual(AsterF1GatedExecution.VerifyPublished(output),
                JsonDocument.Parse(File.ReadAllBytes(record)).RootElement.GetProperty("manifest_sha256").GetString());
            Assert.IsTrue(File.Exists(Path.Combine(output, "world-state.json")));
            Assert.AreEqual(preflight.InitialStateJson, WorldStateJsonSerializer.Serialize(preflight.Input.State));
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
            if (File.Exists(record)) File.Delete(record);
        }
    }
}
