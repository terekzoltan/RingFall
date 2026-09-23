using System.Text.Json;
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
    public required StateDiffValue Old { get; init; }

    [JsonPropertyName("new")]
    public required StateDiffValue New { get; init; }

    [JsonPropertyName("visibility")]
    public required string Visibility { get; init; }
}

public enum StateDiffValueKind
{
    Number,
    String
}

[JsonConverter(typeof(StateDiffValueConverter))]
public readonly record struct StateDiffValue
{
    private readonly double _number;
    private readonly string? _text;

    private StateDiffValue(StateDiffValueKind kind, double number, string? text)
    {
        Kind = kind;
        _number = number;
        _text = text;
    }

    public StateDiffValueKind Kind { get; }

    public double Number => Kind == StateDiffValueKind.Number && IsInitialized
        ? _number
        : throw new InvalidOperationException("StateDiff value is not an initialized number.");

    public string String => Kind == StateDiffValueKind.String && _text is not null
        ? _text
        : throw new InvalidOperationException("StateDiff value is not an initialized string.");

    // Default structs have the Number discriminator; this flag prevents their serialization.
    private bool IsInitialized { get; init; }

    public static StateDiffValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "StateDiff numbers must be finite.");
        }

        return new StateDiffValue(StateDiffValueKind.Number, value, null) { IsInitialized = true };
    }

    public static StateDiffValue FromString(string value) =>
        new(StateDiffValueKind.String, 0, value ?? throw new ArgumentNullException(nameof(value)))
        { IsInitialized = true };

    // Existing T0 numeric initializers remain source-compatible without widening to object.
    public static implicit operator StateDiffValue(double value) => FromNumber(value);
}

public sealed class StateDiffValueConverter : JsonConverter<StateDiffValue>
{
    public override StateDiffValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number when reader.TryGetDouble(out var number) && double.IsFinite(number) =>
                StateDiffValue.FromNumber(number),
            JsonTokenType.String => StateDiffValue.FromString(reader.GetString()!),
            _ => throw new JsonException("StateDiff values must be finite numbers or strings.")
        };
    }

    public override void Write(Utf8JsonWriter writer, StateDiffValue value, JsonSerializerOptions options)
    {
        switch (value.Kind)
        {
            case StateDiffValueKind.Number:
                writer.WriteNumberValue(value.Number);
                break;
            case StateDiffValueKind.String:
                writer.WriteStringValue(value.String);
                break;
            default:
                throw new JsonException("Unknown StateDiff value discriminator.");
        }
    }
}
