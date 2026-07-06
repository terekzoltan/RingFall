using System.Text.Json.Serialization;

namespace Ringfall.Core.Artifacts;

public sealed record RunManifestArtifact
{
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("created_at_utc")]
    public required string CreatedAtUtc { get; init; }

    [JsonPropertyName("scenario_id")]
    public required string ScenarioId { get; init; }

    [JsonPropertyName("run_mode")]
    public required string RunMode { get; init; }

    [JsonPropertyName("simulation_seed")]
    public required int SimulationSeed { get; init; }

    [JsonPropertyName("contract_version")]
    public required string ContractVersion { get; init; }

    [JsonPropertyName("tracks")]
    public required IReadOnlyList<RunManifestTrack> Tracks { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }
}

public sealed record RunManifestTrack
{
    [JsonPropertyName("track_id")]
    public required string TrackId { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("artifact_refs")]
    public required IReadOnlyList<SourceRef> ArtifactRefs { get; init; }
}

public sealed record SourceRef
{
    [JsonPropertyName("ref_id")]
    public required string RefId { get; init; }

    [JsonPropertyName("ref_type")]
    public required string RefType { get; init; }

    [JsonPropertyName("artifact_uri")]
    public required string ArtifactUri { get; init; }
}
