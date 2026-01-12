using System.Collections.Concurrent;
using System.Runtime.Loader;

namespace Core.OS.Persistence;

public static class EntityTypeCache
{
    private static readonly ConcurrentDictionary<string, Type> _entries = new();

    public static Type GetOrAdd(string typeFullName, string? assemblyFullName)
        => _entries.GetOrAdd(typeFullName,
            s =>
            {
                var entityType = AssemblyLoadContext.All
                    .SelectMany(ass => ass.Assemblies)
                    .Where(ass => assemblyFullName is null || ass.FullName is null || ass.FullName.Equals(assemblyFullName, StringComparison.Ordinal))
                    .Select(a => a.GetType(s))
                    .FirstOrDefault(t => t is not null) ?? throw new InvalidOperationException(
                        $"{typeFullName} is not a known type of the {assemblyFullName ?? "<name not provided>"}-assembly");

                return entityType;
            });
}
