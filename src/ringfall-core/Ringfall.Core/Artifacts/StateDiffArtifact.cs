using System.Text.Json.Serialization;

namespace Ringfall.Core.Artifacts;

public sealed record StateDiffArtifact
{
    [JsonPropertyName("diff_type")]
    public required string DiffType { get; init; }

    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("diff_id")]
    public required string DiffId { get; init; }

    [JsonPropertyName("tick")]
    public required int Tick { get; init; }

    [JsonPropertyName("source_action_trace_id")]
    public required string SourceActionTraceId { get; init; }

    [JsonPropertyName("changes")]
    public required IReadOnlyList<StateDiffChange> Changes { get; init; }

    [JsonPropertyName("deterministic_seed")]
    public required int DeterministicSeed { get; init; }
}

public sealed record StateDiffChange
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("old")]
    public required double Old { get; init; }

    [JsonPropertyName("new")]
    public required double New { get; init; }

    [JsonPropertyName("visibility")]
    public required string Visibility { get; init; }
}
