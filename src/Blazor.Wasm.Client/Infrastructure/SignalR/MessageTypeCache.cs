using System.Collections.Concurrent;
using System.Runtime.Loader;

namespace Blazor.Wasm.Client.Infrastructure.SignalR;

//if we need a MessageTypeCache somewhere at some point, this should be moved to Core.Os and become a service
public static class MessageTypeCache
{
    private static readonly ConcurrentDictionary<string, Type> _entries = new();

    public static Type GetOrAdd(string typeFullName)
        => _entries.GetOrAdd(typeFullName,
            s =>
            {
                var entityType = AssemblyLoadContext.All
                    .SelectMany(ass => ass.Assemblies)
                    .Select(a => a.GetType(s))
                    .FirstOrDefault(t => t is not null) ??
                    throw new InvalidOperationException($"Could not determine payload type from name '{typeFullName}'");

                return entityType;
            });
}
