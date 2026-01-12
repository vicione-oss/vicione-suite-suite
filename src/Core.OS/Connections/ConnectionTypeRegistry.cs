using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections;

internal class ConnectionTypeRegistry : IConnectionTypeRegistry
{
    private readonly ConcurrentDictionary<string, (Func<IConnection> CreateConnection, IConnectionSerializer ConnectionSerializer, IConnectionTest? ConnectionTest)> _registry = new();

    public void Register<TConnection, TConnectionSerializer>(string id, Func<IConnection> createConnection, TConnectionSerializer connectionSerializer, IConnectionTest? connectionTest)
        where TConnection : IConnection
        where TConnectionSerializer : IConnectionSerializer
    {
        if (!_registry.TryAdd(id, (createConnection, connectionSerializer, connectionTest)))
            throw new InvalidOperationException($"A component with ID '{id}' is already registered.");
    }

    public IReadOnlyCollection<string> GetConnectionTypes()
        => [.. _registry.Keys];

    public bool TryCreateConnection(string id, [NotNullWhen(true)] out IConnection? connection)
    {
        if (_registry.TryGetValue(id, out var value))
        {
            connection = value.CreateConnection();
            return true;
        }
        connection = null;
        return false;
    }

    public bool TryGetConnectionSerializer(string id, [NotNullWhen(true)] out IConnectionSerializer? connectionSerializer)
    {
        if (_registry.TryGetValue(id, out var value))
        {
            connectionSerializer = value.ConnectionSerializer;
            return true;
        }
        connectionSerializer = null;
        return false;
    }

    public bool TryGetConnectionTest(string id, [NotNullWhen(true)] out IConnectionTest? connectionTest)
    {
        if (_registry.TryGetValue(id, out var value) && value.ConnectionTest is not null)
        {
            connectionTest = value.ConnectionTest;
            return true;
        }
        connectionTest = null;
        return false;
    }
}
