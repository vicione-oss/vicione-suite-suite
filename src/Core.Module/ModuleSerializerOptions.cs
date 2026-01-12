using System.Text.Json;
using System.Text.Json.Serialization;

namespace Core.Module;

public static class ModuleSerializerOptions
{
    private static readonly JsonSerializerOptions serializerOptions = CreateSerializerOptions();

    public static JsonSerializerOptions GetOptions() => serializerOptions;

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
