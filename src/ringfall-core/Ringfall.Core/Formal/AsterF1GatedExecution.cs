using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ringfall.Core.Actions;
using Ringfall.Core.Artifacts;
using Ringfall.Core.Snapshots;

namespace Ringfall.Core.Formal;

// A successor to the P1 evidence interface, not a P1 execution record.
internal static class AsterF1GatedExecution
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly HashSet<string> ModeledIssues = new(StringComparer.Ordinal)
    {
        "tool_unavailable", "crew_unavailable", "tool_execute_denied", "tool_requires_dry_run_conflict",
        "tool_arguments_missing", "tool_argument_value_invalid", "tool_macro_surface_denied"
    };

    internal sealed record Preflight(AsterF1P1Input Input, AsterL1ActionDecision Decision, string Json,
        string PacketId, string ContextId, string InitialStateJson)
    {
        internal string Sha256 => Hash(Encoding.UTF8.GetBytes(Json));
    }

    internal static Preflight Prepare(byte[] state, byte[] candidate, byte[] pulse, byte[] context)
    {
        var input = AsterF1P1InputParser.Parse(state, candidate, pulse, context);
        var decision = input.Candidate switch
        {
            AsterL1ToolActionCandidate tool => AsterL1ActionValidator.Validate(input.State, tool),
            AsterL1WorkOrderCandidate work => AsterL1ActionValidator.Validate(input.State, work),
            _ => throw new AsterF1P1Exception("input_invalid")
        };
        var facts = input.Candidate switch
        {
            AsterL1ToolActionCandidate tool => AsterF1FactExporter.ExportP1(input.State, tool, decision),
            AsterL1WorkOrderCandidate work => AsterF1FactExporter.ExportP1(input.State, work, decision),
            _ => throw new AsterF1P1Exception("input_invalid")
        };
        var packetId = input.Packet.GetProperty("packet_id").GetString()!;
        var contextId = input.Packet.GetProperty("source_context_id").GetString()!;
        var before = WorldStateJsonSerializer.Serialize(input.State);
        var unsupported = facts.UnsupportedPredicates.Count > 0 || decision.Status == AsterL1DecisionStatus.Unsupported
            || decision.Issues.Any(issue => !ModeledIssues.Contains(issue.Code));
        // Preserve Core's complete issue set even when it cannot be represented in F1.
        var record = new
        {
            record_type = "AsterF1Preflight", contract_version = "1.0",
            family_id = AsterF1Policy.FamilyId, family_version = AsterF1Policy.FamilyVersion,
            input_versions = new { state = input.State.SchemaVersion, candidate = "0.1", pulse = "0.1", context_format = "actor_context_projection" },
            inputs = new
            {
                state_input_sha256 = Hash(state), candidate_input_sha256 = Hash(candidate),
                pulse_input_sha256 = Hash(pulse), context_input_sha256 = Hash(context),
                packet_id = packetId, packet_id_sha256 = Hash(Encoding.UTF8.GetBytes(packetId)),
                source_context_id = contextId, source_context_id_sha256 = Hash(Encoding.UTF8.GetBytes(contextId))
            },
            initial_state_sha256 = Hash(Encoding.UTF8.GetBytes(before)),
            binding = new { status = "matched_projection_and_pulse", evidence_ref_count = input.EvidenceRefCount },
            core = new { status = decision.Status.ToString().ToLowerInvariant(), issues = decision.Issues.Select(issue => issue.Code).ToArray() },
            facts = new
            {
                packet_kind = facts.PacketKind, issuer_id = facts.IssuerId, issuer_layer = facts.IssuerLayer,
                issuer_home_sector_id = facts.IssuerHomeSectorId, tool = facts.Tool, work_order = facts.WorkOrder,
                tool_arguments = facts.ToolArguments
            },
            completeness = unsupported ? "unsupported" : "complete",
            unsupported_predicates = facts.UnsupportedPredicates,
            execution = "not_run", schema_validation = "not_run"
        };
        return new Preflight(input, decision, JsonSerializer.Serialize(record, JsonOptions), packetId, contextId, before);
    }

    internal static string Hash(byte[] raw) => Convert.ToHexStringLower(SHA256.HashData(raw));

    internal static string Publish(Preflight preflight, byte[] reportBytes, string outputDirectory,
        AsterF1GatedProof.ClosedInstance expected, byte[] checkOutput, byte[] witness)
    {
        if (reportBytes.Length is 0 or > 1_048_576) throw new AsterF1P1Exception("gate_report_invalid");
        JsonElement report;
        try
        {
            using var document = JsonDocument.Parse(reportBytes, new JsonDocumentOptions { MaxDepth = 32 });
            report = document.RootElement.Clone();
            CheckUnique(report);
            if (report.ValueKind != JsonValueKind.Object
                || report.GetProperty("record_type").GetString() != "AsterF1PreflightSolverReport"
                || report.GetProperty("version").GetString() != "1.0"
                || report.GetProperty("verdict").GetString() != "valid"
                || report.GetProperty("agreement").GetString() != "agreed"
                || report.GetProperty("preflight_sha256").GetString() != preflight.Sha256
                || report.GetProperty("packet_id_sha256").GetString() != Hash(Encoding.UTF8.GetBytes(preflight.PacketId))
                || report.GetProperty("source_context_id_sha256").GetString() != Hash(Encoding.UTF8.GetBytes(preflight.ContextId))
                || report.GetProperty("executed_image").GetString() !=
                    "ghcr.io/graphs4value/refinery-cli@sha256:5d7eacdef0ddfb98e264cad405badf96753c68203e498a12d91331a0aa96ad57"
                || report.GetProperty("invocation_id").GetString() is not string invocation || invocation.Length != 32
                || report.GetProperty("operator_anchor_sha256").GetString()?.Length != 64
                || report.GetProperty("source_aggregate").GetString()?.Length != 64
                || report.GetProperty("inspected_platform").GetString() != "linux/amd64"
                || report.GetProperty("engine").GetString() != "28.0.1 linux/amd64"
                || report.GetProperty("instance_sha256").GetString() != expected.Sha256
                || report.GetProperty("diagnostics").GetArrayLength() != 0
                || report.GetProperty("witness_closed").ValueKind != JsonValueKind.True
                || report.GetProperty("check").GetProperty("invocation_id").GetString() != invocation
                || report.GetProperty("generated").GetProperty("invocation_id").GetString() != invocation
                || report.GetProperty("check").GetProperty("instance_sha256").GetString() != expected.Sha256
                || report.GetProperty("generated").GetProperty("instance_sha256").GetString() != expected.Sha256
                || report.GetProperty("check").GetProperty("exit_code").GetInt32() != 0
                || report.GetProperty("generated").GetProperty("exit_code").GetInt32() != 0)
                throw new AsterF1P1Exception("gate_not_positive");
            foreach (var (name, bytes) in new[] { ("state", preflight.Input.StateBytes),
                ("candidate", preflight.Input.CandidateBytes), ("pulse", preflight.Input.PulseBytes),
                ("context", preflight.Input.ContextBytes) })
                if (report.GetProperty("input_sha256").GetProperty(name).GetString() != Hash(bytes))
                    throw new AsterF1P1Exception("gate_input_drift");
            if (checkOutput.Length > 65_536 || witness.Length > 65_536
                || !checkOutput.AsSpan().SequenceEqual("Model is consistent\n"u8)
                || !AsterF1GatedProof.WitnessIsClosed(witness, expected)
                || report.GetProperty("check").GetProperty("stdout_sha256").GetString() != Hash(checkOutput)
                || report.GetProperty("check").GetProperty("stderr_sha256").GetString() != Hash([])
                || report.GetProperty("generated").GetProperty("stderr_sha256").GetString() != Hash([])
                || report.GetProperty("check").GetProperty("stdout_bytes").GetInt32() != checkOutput.Length
                || report.GetProperty("check").GetProperty("stderr_bytes").GetInt32() != 0
                || report.GetProperty("generated").GetProperty("stdout_bytes").GetInt32() != witness.Length
                || report.GetProperty("generated").GetProperty("stderr_bytes").GetInt32() != 0
                || report.GetProperty("generated").GetProperty("stdout_sha256").GetString() != Hash(witness))
                throw new AsterF1P1Exception("gate_receipt_mismatch");
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
            or FormatException or IOException or UnauthorizedAccessException)
        {
            throw new AsterF1P1Exception("gate_report_invalid");
        }

        if (preflight.Decision.Status != AsterL1DecisionStatus.Allowed || preflight.Decision.Issues.Count != 0)
            throw new AsterF1P1Exception("core_gate_disagreement");
        // This is the exact parsed in-memory snapshot that was hashed before the solver ran.
        if (WorldStateJsonSerializer.Serialize(preflight.Input.State) != preflight.InitialStateJson
            || preflight.Sha256 != report.GetProperty("preflight_sha256").GetString())
            throw new AsterF1P1Exception("gate_input_drift");
        var fresh = preflight.Input.Candidate switch
        {
            AsterL1ToolActionCandidate tool => AsterL1ActionValidator.Validate(preflight.Input.State, tool),
            AsterL1WorkOrderCandidate work => AsterL1ActionValidator.Validate(preflight.Input.State, work),
            _ => throw new AsterF1P1Exception("input_invalid")
        };
        if (fresh.Status != preflight.Decision.Status || !fresh.Issues.SequenceEqual(preflight.Decision.Issues))
            throw new AsterF1P1Exception("core_gate_disagreement");
        var execution = preflight.Input.Candidate switch
        {
            AsterL1ToolActionCandidate tool => AsterL1ActionExecutor.Execute(preflight.Input.State, tool),
            AsterL1WorkOrderCandidate work => AsterL1ActionExecutor.Execute(preflight.Input.State, work),
            _ => throw new AsterF1P1Exception("input_invalid")
        };
        AsterF1P1EvidenceBridge.VerifyExecution(preflight.Input.State, preflight.Input.Candidate, execution);
        if (execution.Decision.Status != fresh.Status || !execution.Decision.Issues.SequenceEqual(fresh.Issues)
            || WorldStateJsonSerializer.Serialize(preflight.Input.State) != preflight.InitialStateJson)
            throw new AsterF1P1Exception("core_gate_disagreement");
        return PublishBundle(preflight, reportBytes, execution, outputDirectory);
    }

    // The caller may only choose a new location under the private local temp root.
    internal static string PrivateOutput(string path)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "opencode"));
        var output = Path.GetFullPath(path);
        if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || output == root || Path.GetFileName(output).StartsWith(".", StringComparison.Ordinal))
            throw new AsterF1P1Exception("output_not_private");
        var parent = new DirectoryInfo(Path.GetDirectoryName(output)!);
        if (!parent.Exists) throw new AsterF1P1Exception("output_parent_missing");
        for (var current = parent; current is not null && current.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase);
             current = current.Parent)
            if (!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new AsterF1P1Exception("output_path_untrusted");
        if (Directory.Exists(output) || File.Exists(output)) throw new AsterF1P1Exception("output_exists");
        return output;
    }

    // Also exercised directly by Core tests with a tentative execution; this does not bypass the gate in Headless.
    internal static string PublishBundle(Preflight preflight, byte[] report, AsterL1ActionExecution execution, string path,
        Action<string>? afterWrite = null)
    {
        var output = PrivateOutput(path);
        var parent = Path.GetDirectoryName(output)!;
        if (!Directory.Exists(parent)) throw new AsterF1P1Exception("output_parent_missing");
        var stage = Path.Combine(parent, ".ringfall-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            var snapshot = WorldStateJsonSerializer.Serialize(execution.FinalState);
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["preflight.json"] = Encoding.UTF8.GetBytes(preflight.Json),
                ["solver-report.json"] = report,
                ["world-state.json"] = Encoding.UTF8.GetBytes(snapshot),
                ["action-trace.json"] = Encoding.UTF8.GetBytes(ArtifactJsonSerializer.Serialize(execution.ActionTrace)),
                ["execution-result.json"] = Encoding.UTF8.GetBytes(ArtifactJsonSerializer.Serialize(execution.ExecutionResult))
            };
            if (execution.StateDiff is not null)
                files.Add("state-diff.json", Encoding.UTF8.GetBytes(ArtifactJsonSerializer.Serialize(execution.StateDiff)));
            var manifest = new
            {
                record_type = "AsterF1GatedBundle", version = "1.0", packet_id = preflight.PacketId,
                source_context_id = preflight.ContextId, initial_state_sha256 = Hash(Encoding.UTF8.GetBytes(preflight.InitialStateJson)),
                final_state_sha256 = Hash(files["world-state.json"]),
                disposition = execution.Disposition.ToString().ToLowerInvariant(),
                schema_validation = "not_run", preflight_sha256 = preflight.Sha256,
                artifact_sha256 = files.ToDictionary(pair => pair.Key, pair => Hash(pair.Value), StringComparer.Ordinal)
            };
            files.Add("manifest.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, JsonOptions)));
            foreach (var (name, bytes) in files)
            {
                var file = Path.Combine(stage, name);
                File.WriteAllBytes(file, bytes);
                if (Hash(File.ReadAllBytes(file)) != Hash(bytes)) throw new AsterF1P1Exception("bundle_write_mismatch");
                afterWrite?.Invoke(name);
            }
            if (Directory.Exists(output) || File.Exists(output)) throw new AsterF1P1Exception("output_exists");
            Directory.Move(stage, output);
            return output;
        }
        finally
        {
            if (Directory.Exists(stage))
                try { Directory.Delete(stage, recursive: true); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                { throw new AsterF1P1Exception("stage_cleanup_uncertain"); }
        }
    }

    internal static string VerifyPublished(string output)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "manifest.json")));
        var manifest = document.RootElement;
        if (manifest.GetProperty("record_type").GetString() != "AsterF1GatedBundle"
            || manifest.GetProperty("schema_validation").GetString() != "not_run")
            throw new AsterF1P1Exception("publication_reconciliation_uncertain");
        foreach (var entry in manifest.GetProperty("artifact_sha256").EnumerateObject())
        {
            if (entry.Name is not ("preflight.json" or "solver-report.json" or "world-state.json"
                or "action-trace.json" or "execution-result.json" or "state-diff.json")
                || Hash(File.ReadAllBytes(Path.Combine(output, entry.Name))) != entry.Value.GetString())
                throw new AsterF1P1Exception("publication_reconciliation_uncertain");
        }
        return Hash(File.ReadAllBytes(Path.Combine(output, "manifest.json")));
    }

    internal static void EmitPublished(string output, string privateRecordPath, string invocationId,
        string sourceAggregate, Action<string> emitter)
    {
        try
        {
            var manifestHash = VerifyPublished(output);
            File.WriteAllText(privateRecordPath, JsonSerializer.Serialize(new
            {
                invocation_id = invocationId, destination = output, manifest_sha256 = manifestHash,
                source_aggregate = sourceAggregate
            }));
            emitter(output);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AsterF1P1Exception)
        {
            try { VerifyPublished(output); }
            catch (Exception) { throw new AsterF1P1Exception("publication_reconciliation_uncertain"); }
            throw new AsterF1P1Exception("published_output_failed");
        }
    }

    private static void CheckUnique(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray()) CheckUnique(item);
        }
        else if (node.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in node.EnumerateObject())
            {
                if (!seen.Add(item.Name)) throw new AsterF1P1Exception("gate_report_invalid");
                CheckUnique(item.Value);
            }
        }
    }
}
