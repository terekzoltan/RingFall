using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ringfall.Core.Actions;
using Ringfall.Core.Artifacts;
using Ringfall.Core.Snapshots;
using Ringfall.Core.State;

namespace Ringfall.Core.Formal;

internal static class AsterF1P1EvidenceBridge
{
    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    internal static string Produce(byte[] stateBytes, byte[] candidateBytes, byte[] pulseBytes, byte[] contextBytes)
    {
        var input = AsterF1P1InputParser.Parse(stateBytes, candidateBytes, pulseBytes, contextBytes);
        var before = WorldStateJsonSerializer.Serialize(input.State);
        // Only Core executes; this bridge never validates a candidate a second time.
        var execution = input.Candidate switch
        {
            AsterL1ToolActionCandidate tool => AsterL1ActionExecutor.Execute(input.State, tool),
            AsterL1WorkOrderCandidate work => AsterL1ActionExecutor.Execute(input.State, work),
            _ => throw new AsterF1P1Exception("input_invalid")
        };
        VerifyExecution(input.State, input.Candidate, execution);
        if (before != WorldStateJsonSerializer.Serialize(input.State))
            throw new AsterF1P1Exception("evidence_mismatch");
        var facts = input.Candidate switch
        {
            AsterL1ToolActionCandidate tool => AsterF1FactExporter.ExportP1(input.State, tool, execution.Decision),
            AsterL1WorkOrderCandidate work => AsterF1FactExporter.ExportP1(input.State, work, execution.Decision),
            _ => throw new AsterF1P1Exception("input_invalid")
        };
        var packetId = input.Packet.GetProperty("packet_id").GetString()!;
        var contextId = input.Packet.GetProperty("source_context_id").GetString()!;
        var trace = execution.ActionTrace;
        var result = execution.ExecutionResult;
        var diff = execution.StateDiff;
        var unsupported = facts.UnsupportedPredicates.Count != 0;
        var output = new
        {
            record_type = "AsterF1P1Evidence",
            evidence_version = "0.1",
            family_id = AsterF1Policy.FamilyId,
            family_version = AsterF1Policy.FamilyVersion,
            input_versions = new { state = input.State.SchemaVersion, candidate = "0.1", pulse = "0.1", context_format = "actor_context_projection" },
            inputs = new
            {
                state_input_sha256 = Hash(stateBytes), candidate_input_sha256 = Hash(candidateBytes),
                pulse_input_sha256 = Hash(pulseBytes), context_input_sha256 = Hash(contextBytes),
                packet_id_sha256 = Hash(packetId), source_context_id_sha256 = Hash(contextId)
            },
            state = new
            {
                initial_sha256 = Hash(before), final_sha256 = Hash(WorldStateJsonSerializer.Serialize(execution.FinalState)),
                changed = !ReferenceEquals(execution.InitialState, execution.FinalState)
            },
            binding = new { status = "matched_projection_and_pulse", evidence_ref_count = input.EvidenceRefCount },
            core = new
            {
                status = execution.Decision.Status.ToString().ToLowerInvariant(),
                issues = execution.Decision.Issues.Select(issue => issue.Code).ToArray(),
                disposition = execution.Disposition.ToString().ToLowerInvariant()
            },
            links = new
            {
                trace = trace is null ? null : (object)new
                {
                    id_sha256 = Hash(trace.ActionTraceId), source_packet_id_sha256 = Hash(trace.SourcePacketId),
                    execution_status = trace.ExecutionStatus,
                    state_diff_ref_sha256 = trace.StateDiffRefs.Count == 0 ? null : Hash(trace.StateDiffRefs[0].RefId),
                    validation = new
                    {
                        schema = trace.Validation.Schema.Status, authority = trace.Validation.Authority.Status,
                        context = trace.Validation.Context.Status, visibility = trace.Validation.Visibility.Status
                    }
                },
                result = result is null ? null : (object)new
                {
                    id_sha256 = Hash(result.PacketId), source_packet_id_sha256 = Hash(result.SourcePacketId),
                    execution_status = result.ExecutionStatus,
                    true_effect_ref_sha256 = result.TrueEffectRefs.Count == 0 ? null : Hash(result.TrueEffectRefs[0])
                },
                diff = diff is null ? null : (object)new
                {
                    id_sha256 = Hash(diff.DiffId), source_trace_id_sha256 = Hash(diff.SourceActionTraceId),
                    tick = diff.Tick, change_class = "crew_status_available_to_unavailable"
                }
            },
            facts = new
            {
                packet_kind = facts.PacketKind,
                issuer_id = facts.IssuerId, issuer_layer = facts.IssuerLayer,
                issuer_home_sector_id = facts.IssuerHomeSectorId,
                tool = facts.Tool, work_order = facts.WorkOrder, tool_arguments = facts.ToolArguments
            },
            completeness = unsupported ? "unsupported" : "complete",
            unsupported_predicates = facts.UnsupportedPredicates,
            diagnostics = unsupported ? ["facts_unsupported"] : execution.Decision.Status != AsterL1DecisionStatus.Allowed
                ? new[] { "core_rejected" } : Array.Empty<string>(),
            schema_validation = "not_run", formal_proof = "not_run", mutation_authorized = false
        };
        return JsonSerializer.Serialize(output, OutputOptions);
    }

    internal static void VerifyExecution(WorldState state, object candidate, AsterL1ActionExecution execution)
    {
        void Mismatch() => throw new AsterF1P1Exception("evidence_mismatch");
        if (!ReferenceEquals(state, execution.InitialState)
            || execution.Decision.Issues.Any(issue => !AsterL1IssueCatalog.Codes.Contains(issue.Code, StringComparer.Ordinal)))
            Mismatch();
        var (packetId, tick, kind) = candidate switch
        {
            AsterL1ToolActionCandidate tool => (tool.PacketId, tool.IssuedAtTick, "tool_action"),
            AsterL1WorkOrderCandidate work => (work.PacketId, work.IssuedAtTick, "work_order"),
            _ => throw new AsterF1P1Exception("evidence_mismatch")
        };
        var disposition = execution.Disposition;
        var trace = execution.ActionTrace;
        var result = execution.ExecutionResult;
        var diff = execution.StateDiff;
        if (disposition == AsterL1ExecutionDisposition.IdentityRejected)
        {
            if (trace is not null || result is not null || diff is not null
                || !ReferenceEquals(state, execution.FinalState) || execution.Decision.Status == AsterL1DecisionStatus.Allowed)
                Mismatch();
            return;
        }
        var status = disposition switch
        {
            AsterL1ExecutionDisposition.Rejected => "rejected",
            AsterL1ExecutionDisposition.Deferred => "deferred",
            AsterL1ExecutionDisposition.Succeeded => "success",
            AsterL1ExecutionDisposition.Failed => "failed",
            _ => throw new AsterF1P1Exception("evidence_mismatch")
        };
        if (trace is null || result is null || string.IsNullOrWhiteSpace(packetId)
            || trace.ActionTraceId != $"action-trace:{packetId}"
            || result.PacketId != $"execution-result:{packetId}"
            || trace.SourcePacketId != packetId || result.SourcePacketId != packetId
            || trace.ActionClass != kind || trace.ExecutionStatus != status || result.ExecutionStatus != status
            || !new[] { trace.Validation.Schema.Status, trace.Validation.Authority.Status,
                trace.Validation.Context.Status, trace.Validation.Visibility.Status }
                .All(value => value is "pass" or "fail")
            || (disposition == AsterL1ExecutionDisposition.Rejected) == (execution.Decision.Status == AsterL1DecisionStatus.Allowed))
            Mismatch();
        if (diff is null)
        {
            if (trace!.StateDiffRefs.Count != 0 || result!.TrueEffectRefs.Count != 0
                || !ReferenceEquals(state, execution.FinalState)
                || disposition == AsterL1ExecutionDisposition.Succeeded && kind == "work_order") Mismatch();
            return;
        }
        if (disposition != AsterL1ExecutionDisposition.Succeeded || kind != "work_order"
            || ReferenceEquals(state, execution.FinalState) || diff.DiffId != $"state-diff:{packetId}"
            || diff.SourceActionTraceId != trace!.ActionTraceId || diff.Tick != tick
            || diff.DeterministicSeed != state.SimulationSeed
            || trace.StateDiffRefs.Count != 1 || trace.StateDiffRefs[0].RefType != "state_diff"
            || trace.StateDiffRefs[0].RefId != diff.DiffId
            || result!.TrueEffectRefs.Count != 1 || result.TrueEffectRefs[0] != diff.DiffId
            || diff.Changes.Count != 1 || diff.Changes[0].Path != "/crews/crew_aster_repair_02/status"
            || diff.Changes[0].Visibility != "issuer_observable"
            || diff.Changes[0].Old != StateDiffValue.FromString("available")
            || diff.Changes[0].New != StateDiffValue.FromString("unavailable")) Mismatch();
        var expected = state with
        {
            Crews = state.Crews.Select(crew => crew.CrewId == "crew_aster_repair_02"
                ? crew with { Status = "unavailable" } : crew).ToArray()
        };
        if (WorldStateJsonSerializer.Serialize(expected) != WorldStateJsonSerializer.Serialize(execution.FinalState))
            Mismatch();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
}
