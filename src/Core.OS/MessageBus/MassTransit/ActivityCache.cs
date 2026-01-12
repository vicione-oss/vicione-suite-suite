using System.Collections.Concurrent;

namespace Core.OS.MessageBus.MassTransit;

public static class ActivityTypeCache
{
    public static ConcurrentDictionary<string, Type> TypeMap { get; } = new();
}
