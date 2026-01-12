using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Sdk.Client.Connections;
using Sdk.Connections.Contracts;

namespace Blazor.Server.Backend.Services;

internal class ConnectionTypeUiRegistry : IConnectionTypeUiRegistry
{
    private readonly ConcurrentDictionary<string, (Type ComponentType, Func<string> GetDisplayName, Func<IItemValidator> GetItemValidator)> _registry = new();

    public void Register<TConnection, TComponent, TValidator>(string id, Func<string> getDisplayName)
        where TConnection : IConnection
        where TComponent : ConnectionSettingsComponentBase<TConnection>
        where TValidator : ItemValidatorBase<TConnection>, new()
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("ID cannot be null or empty", nameof(id));

        _registry.TryAdd(id, (typeof(TComponent), getDisplayName, () => new TValidator()));
    }

    public bool TryGetDisplayName(string id, [NotNullWhen(true)] out string? displayName)
    {
        if (_registry.TryGetValue(id, out var entry))
        {
            displayName = entry.GetDisplayName();
            return true;
        }

        displayName = null;
        return false;
    }

    public bool TryGetComponentType(string id, [NotNullWhen(true)] out Type? componentType)
    {
        if (_registry.TryGetValue(id, out var entry))
        {
            componentType = entry.ComponentType;
            return true;
        }

        componentType = null;
        return false;
    }

    public bool TryGetItemValidator(string id, [NotNullWhen(true)] out IItemValidator? itemValidator)
    {
        if (_registry.TryGetValue(id, out var registryEntry))
        {
            itemValidator = registryEntry.GetItemValidator();
            return true;
        }

        itemValidator = null;
        return false;
    }
}
