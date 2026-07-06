using System.Text.Json;

namespace Ringfall.Core.Artifacts;

internal static class ArtifactJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }
}
