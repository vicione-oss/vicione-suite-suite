using System.Text.Json;
using System.Text.Json.Serialization;
using Sdk.Backend.Artifacts;

namespace Core.Artifacts.JFrog;

internal static class JFrogArtifactSerializerOptions
{
    private static readonly JsonSerializerOptions serializerOptions = CreateSerializerOptions();

    public static JsonSerializerOptions GetOptions() => serializerOptions;

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter<ArtifactKind>() }
        };

        return options;
    }
}
