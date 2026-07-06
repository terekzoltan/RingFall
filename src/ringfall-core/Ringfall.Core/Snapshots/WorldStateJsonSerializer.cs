using System.Text.Json;
using System.Text.Json.Serialization;
using Ringfall.Core.State;

namespace Ringfall.Core.Snapshots;

public static class WorldStateJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static string Serialize(WorldState worldState)
    {
        return JsonSerializer.Serialize(worldState, SerializerOptions);
    }

    public static WorldState Deserialize(string json)
    {
        return JsonSerializer.Deserialize<WorldState>(json, SerializerOptions)
            ?? throw new InvalidOperationException("WorldState JSON did not produce a state object.");
    }
}
