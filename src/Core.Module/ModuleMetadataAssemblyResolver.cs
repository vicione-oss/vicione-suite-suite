using System.Reflection;
using System.Runtime.Loader;

namespace Core.Module;

public sealed class ModuleMetadataAssemblyResolver : MetadataAssemblyResolver
{
    private readonly List<AssemblyDependencyResolver> _resolvers = [];
    private readonly Dictionary<string, string> _paths;

    internal ModuleMetadataAssemblyResolver(IEnumerable<string> components, IEnumerable<string> paths)
    {
        foreach (var component in components)
            _resolvers.Add(new AssemblyDependencyResolver(component));
        _paths = paths.ToDictionary(p => Path.GetFileNameWithoutExtension(p), p => p, StringComparer.OrdinalIgnoreCase);
    }

    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
    {
        _paths.TryGetValue(assemblyName.Name ?? string.Empty, out var path);
        path ??= ResolveAssemblyToPath(assemblyName);
        path ??= Assembly.Load(assemblyName).Location;

        if (string.IsNullOrEmpty(path))
            return null;
        else
            return context.LoadWithStreamFromPath(path);

        string? ResolveAssemblyToPath(AssemblyName name)
        {
            foreach (var resolver in _resolvers)
            {
                var assemblyPath = resolver.ResolveAssemblyToPath(name);
                if (!string.IsNullOrEmpty(assemblyPath))
                    return assemblyPath;
            }

            return null;
        }
    }
}

internal static class MetaDataLoadContextExtensions
{
    internal static Assembly LoadWithStreamFromPath(this MetadataLoadContext context, string path)
    {
        using var file = File.OpenRead(path);
        return context.LoadFromStream(file);
    }
}
