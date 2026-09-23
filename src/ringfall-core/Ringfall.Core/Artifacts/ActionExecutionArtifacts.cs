using System.Text.Json.Serialization;

namespace Ringfall.Core.Artifacts;

internal sealed record ActionTraceArtifact
{
    [JsonPropertyName("trace_type")]
    public string TraceType => "ActionTrace";

    [JsonPropertyName("schema_version")]
    public string SchemaVersion => "0.1";

    [JsonPropertyName("action_trace_id")]
    public required string ActionTraceId { get; init; }

    [JsonPropertyName("source_packet_id")]
    public required string SourcePacketId { get; init; }

    [JsonPropertyName("issuer_id")]
    public required string IssuerId { get; init; }

    [JsonPropertyName("issuer_layer")]
    public required string IssuerLayer { get; init; }

    [JsonPropertyName("action_class")]
    public required string ActionClass { get; init; }

    [JsonPropertyName("validation")]
    public required ActionValidation Validation { get; init; }

    [JsonPropertyName("execution_status")]
    public required string ExecutionStatus { get; init; }

    [JsonPropertyName("state_diff_refs")]
    public required IReadOnlyList<ActionArtifactRef> StateDiffRefs { get; init; }

    [JsonPropertyName("events_created")]
    public required IReadOnlyList<ActionArtifactRef> EventsCreated { get; init; }

    [JsonPropertyName("eval_flags")]
    public required IReadOnlyList<string> EvalFlags { get; init; }
}

internal sealed record ActionArtifactRef
{
    [JsonPropertyName("ref_id")]
    public required string RefId { get; init; }

    [JsonPropertyName("ref_type")]
    public required string RefType { get; init; }
}

internal sealed record ActionValidation
{
    [JsonPropertyName("schema")]
    public required ActionValidationResult Schema { get; init; }

    [JsonPropertyName("authority")]
    public required ActionValidationResult Authority { get; init; }

    [JsonPropertyName("context")]
    public required ActionValidationResult Context { get; init; }

    [JsonPropertyName("visibility")]
    public required ActionValidationResult Visibility { get; init; }
}

internal sealed record ActionValidationResult
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("notes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; init; }
}

internal sealed record ExecutionResultArtifact
{
    [JsonPropertyName("packet_id")]
    public required string PacketId { get; init; }

    [JsonPropertyName("packet_type")]
    public string PacketType => "ExecutionResult";

    [JsonPropertyName("schema_version")]
    public string SchemaVersion => "0.1";

    [JsonPropertyName("source_packet_id")]
    public required string SourcePacketId { get; init; }

    [JsonPropertyName("execution_status")]
    public required string ExecutionStatus { get; init; }

    [JsonPropertyName("true_effect_refs")]
    public required IReadOnlyList<string> TrueEffectRefs { get; init; }

    [JsonPropertyName("issuer_observable")]
    public required IssuerObservableResult IssuerObservable { get; init; }

    [JsonPropertyName("events_created")]
    public required IReadOnlyList<string> EventsCreated { get; init; }
}

internal sealed record IssuerObservableResult
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }
}
