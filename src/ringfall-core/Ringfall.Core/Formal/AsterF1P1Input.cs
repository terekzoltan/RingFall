using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ringfall.Core.Actions;
using Ringfall.Core.Projection;
using Ringfall.Core.Scenarios;
using Ringfall.Core.State;

namespace Ringfall.Core.Formal;

internal sealed record AsterF1P1Input(WorldState State, object Candidate, JsonElement Packet,
    int EvidenceRefCount, byte[] StateBytes, byte[] CandidateBytes, byte[] PulseBytes, byte[] ContextBytes);

internal static class AsterF1P1InputParser
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions CandidateOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "packet_id", "packet_type", "schema_version", "issuer_id", "issuer_layer", "issued_at_tick",
        "issued_at_turn", "source_context_id", "rationale", "confidence", "evidence_refs",
        "visibility_intent", "urgency"
    };
    private static readonly HashSet<string> Tool = new(Common, StringComparer.Ordinal)
    {
        "tool_id", "action", "arguments", "mode", "requires_dry_run"
    };
    private static readonly HashSet<string> Work = new(Common, StringComparer.Ordinal)
    {
        "target_crew_id", "target_crew_pool_id", "target_location", "task_type", "priority",
        "risk_tolerance", "authorized_resources", "constraints", "expected_report", "fallback_protocol",
        "stealth_level", "public_visibility"
    };

    internal static AsterF1P1Input Parse(byte[] stateBytes, byte[] candidateBytes, byte[] pulseBytes, byte[] contextBytes)
    {
        var stateJson = Decode(stateBytes);
        var packet = ParseJson(candidateBytes);
        var pulse = ParseJson(pulseBytes);
        var context = ParseJson(contextBytes);
        _ = ParseJson(stateBytes);
        WorldState state;
        try { state = InitialStateLoader.LoadFromJson(stateJson); }
        catch (InitialStateValidationException) { throw new AsterF1P1Exception("state_invalid"); }
        if (state.SchemaVersion != "0.1") throw new AsterF1P1Exception("state_invalid");

        var kind = RequiredString(packet, "packet_type");
        if (kind is not ("ToolActionRequest" or "WorkOrderRequest"))
            throw new AsterF1P1Exception("unsupported_packet_kind");
        var fields = kind == "ToolActionRequest" ? Tool : Work;
        if (packet.EnumerateObject().Any(property => !fields.Contains(property.Name)))
            throw new AsterF1P1Exception("input_invalid");
        foreach (var name in new[] { "packet_id", "issuer_id", "issuer_layer", "source_context_id" })
            _ = RequiredString(packet, name);
        if (RequiredString(packet, "schema_version") != "0.1"
            || !packet.TryGetProperty("issued_at_tick", out var tick) || tick.ValueKind != JsonValueKind.Number
            || !tick.TryGetInt32(out var issuedAtTick) || issuedAtTick < 0)
            throw new AsterF1P1Exception("input_invalid");
        foreach (var name in new[] { "issued_at_turn", "rationale", "visibility_intent", "urgency" })
            OptionalString(packet, name);
        if (packet.TryGetProperty("confidence", out var confidence)
            && (confidence.ValueKind != JsonValueKind.Number || !confidence.TryGetDouble(out var number)
                || !double.IsFinite(number)))
            throw new AsterF1P1Exception("input_invalid");
        if (kind == "ToolActionRequest")
        {
            foreach (var name in new[] { "tool_id", "action", "mode" }) _ = RequiredString(packet, name);
            if (packet.TryGetProperty("arguments", out var args) && args.ValueKind != JsonValueKind.Object)
                throw new AsterF1P1Exception("input_invalid");
            if (packet.TryGetProperty("requires_dry_run", out var dryRun)
                && dryRun.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new AsterF1P1Exception("input_invalid");
        }
        else
        {
            foreach (var name in new[] { "target_location", "task_type", "priority" }) _ = RequiredString(packet, name);
            foreach (var name in new[] { "target_crew_id", "target_crew_pool_id", "risk_tolerance",
                "expected_report", "fallback_protocol", "stealth_level", "public_visibility" }) OptionalString(packet, name);
            foreach (var name in new[] { "authorized_resources", "constraints" }) OptionalStrings(packet, name);
        }
        object candidate;
        try
        {
            candidate = kind == "ToolActionRequest"
                ? (object)(packet.Deserialize<AsterL1ToolActionCandidate>(CandidateOptions)
                    ?? throw new AsterF1P1Exception("input_invalid"))
                : packet.Deserialize<AsterL1WorkOrderCandidate>(CandidateOptions)
                    ?? throw new AsterF1P1Exception("input_invalid");
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        { throw new AsterF1P1Exception("input_invalid"); }

        var refs = RequiredStrings(packet, "evidence_refs", "context_unbound");
        var pulseRefs = RequiredStrings(pulse, "evidence_refs", "context_unbound");
        if (pulseRefs.Length != refs.Length || !pulseRefs.SequenceEqual(refs, StringComparer.Ordinal))
            throw new AsterF1P1Exception("context_mismatch");
        if (RequiredString(pulse, "packet_type") != "AvatarPulsePacket"
            || RequiredString(pulse, "schema_version") != "0.1"
            || RequiredString(pulse, "issuer_id") != RequiredString(packet, "issuer_id")
            || RequiredString(pulse, "actor_id") != RequiredString(packet, "issuer_id")
            || RequiredString(pulse, "issuer_layer") != RequiredString(packet, "issuer_layer")
            || RequiredString(pulse, "source_context_id") != RequiredString(packet, "source_context_id")
            || !pulse.TryGetProperty("issued_at_tick", out var pulseTick)
            || pulseTick.ValueKind != JsonValueKind.Number || !pulseTick.TryGetInt32(out var pt)
            || pt != issuedAtTick)
            throw new AsterF1P1Exception("context_mismatch");
        _ = RequiredString(pulse, "packet_id");
        if (!pulse.TryGetProperty("requested_packets", out var requests)
            || requests.ValueKind != JsonValueKind.Array || requests.EnumerateArray().Count(request =>
                request.ValueKind == JsonValueKind.Object
                && request.TryGetProperty("packet_type", out var requestKind)
                && request.TryGetProperty("draft_ref", out var requestId)
                && requestKind.ValueKind == JsonValueKind.String && requestId.ValueKind == JsonValueKind.String
                && requestKind.GetString() == kind && requestId.GetString() == RequiredString(packet, "packet_id")) != 1)
            throw new AsterF1P1Exception("context_mismatch");

        ActorContextProjection projection;
        try { projection = ActorContextProjector.Project(state, RequiredString(packet, "issuer_id")); }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        { throw new AsterF1P1Exception("context_mismatch"); }
        var expected = ActorContextProjectionJsonSerializer.SerializeToUtf8(state, projection.ActorId);
        if (!JsonNode.DeepEquals(JsonNode.Parse(Decode(contextBytes)), JsonNode.Parse(expected)))
            throw new AsterF1P1Exception("context_mismatch");
        var allowed = projection.Observations.SelectMany(observation => new[] { observation.ObservationId, observation.SourceRef })
            .ToHashSet(StringComparer.Ordinal);
        if (refs.Length == 0 || refs.Distinct(StringComparer.Ordinal).Count() != refs.Length
            || refs.Any(value => !allowed.Contains(value)))
            throw new AsterF1P1Exception("context_unbound");
        if (pulse.TryGetProperty("observed", out var observed)
            && (observed.ValueKind != JsonValueKind.Array || observed.EnumerateArray().Any(item =>
                item.ValueKind != JsonValueKind.String || !projection.Observations.Any(ob => ob.Signal == item.GetString()))))
            throw new AsterF1P1Exception("context_mismatch");
        return new AsterF1P1Input(state, candidate, packet, refs.Length,
            stateBytes, candidateBytes, pulseBytes, contextBytes);
    }

    private static string Decode(byte[] bytes)
    {
        try
        {
            if (bytes.Length == 0 || bytes.Length > 1024 * 1024 || bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
                throw new AsterF1P1Exception("input_invalid");
            return Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException) { throw new AsterF1P1Exception("input_invalid"); }
    }

    private static JsonElement ParseJson(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(Decode(bytes), new JsonDocumentOptions { MaxDepth = 32 });
            if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new AsterF1P1Exception("input_invalid");
            CheckDuplicates(doc.RootElement);
            return doc.RootElement.Clone();
        }
        catch (JsonException) { throw new AsterF1P1Exception("input_invalid"); }
    }

    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) CheckDuplicates(item);
        }
        if (value.ValueKind != JsonValueKind.Object) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw new AsterF1P1Exception("input_invalid");
            CheckDuplicates(property.Value);
        }
    }

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())) throw new AsterF1P1Exception("input_invalid");
        return value.GetString()!;
    }

    private static void OptionalString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.String)
            throw new AsterF1P1Exception("input_invalid");
    }

    private static void OptionalStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return;
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
            throw new AsterF1P1Exception("input_invalid");
    }

    private static string[] RequiredStrings(JsonElement root, string name, string missingCode)
    {
        if (!root.TryGetProperty(name, out var value)) throw new AsterF1P1Exception(missingCode);
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item =>
            item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
            throw new AsterF1P1Exception("input_invalid");
        return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
    }
}
