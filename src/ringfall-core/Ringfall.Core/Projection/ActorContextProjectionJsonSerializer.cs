using System.Text;
using System.Text.Json;
using Ringfall.Core.State;

namespace Ringfall.Core.Projection;

public static class ActorContextProjectionJsonSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static byte[] SerializeToUtf8(WorldState state, string actorId)
    {
        var projection = ActorContextProjector.Project(state, actorId);

        var json = JsonSerializer.Serialize(projection, SerializerOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(json + "\n");
    }
}
