using System.IO.Abstractions;
using System.Text.Json;

namespace Suite.Deps;

internal static class AssemblyMappingAnalyser
{
    public static async Task Execute(AssemblyMappingAnalyserOptions options)
    {
        var manifest = options.Manifest.Any()
            ? options.Manifest
            : [];

        var context = SuiteDependencyContextFactory.Create(new FileSystem(), options.SuitePath, manifest);

        if (!string.IsNullOrEmpty(options.OutputPath))
        {
            await using var fs = File.OpenWrite(options.OutputPath);
            await JsonSerializer.SerializeAsync(fs, context.Mappings, JsonSerializerOptions.Default, CancellationToken.None);
            return;
        }

        foreach (var mapping in context.Mappings.OrderBy(k => k.AssemblyName))
        {
            Console.WriteLine("Map {0} to {1} v{2}", mapping.AssemblyName, mapping.MapTo.Module, mapping.MapTo.Version);

            foreach (var mapFrom in mapping.MapFrom)
            {
                Console.WriteLine(" from {0} v{1}", mapFrom.Module, mapFrom.Version);
            }
        }
    }
}
