using System.Text.Json.Serialization;

namespace Ringfall.Core.Artifacts;

public sealed record EventLogArtifact
{
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("scenario_id")]
    public required string ScenarioId { get; init; }

    [JsonPropertyName("deterministic_seed")]
    public required int DeterministicSeed { get; init; }

    [JsonPropertyName("events")]
    public required IReadOnlyList<SimulationEventArtifact> Events { get; init; }
}

public sealed record SimulationEventArtifact
{
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    [JsonPropertyName("event_id")]
    public required string EventId { get; init; }

    [JsonPropertyName("tick")]
    public required int Tick { get; init; }

    [JsonPropertyName("sector_id")]
    public required string SectorId { get; init; }

    [JsonPropertyName("system_id")]
    public required string SystemId { get; init; }

    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("deterministic_seed")]
    public required int DeterministicSeed { get; init; }
}
