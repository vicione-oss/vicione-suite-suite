using System.ComponentModel.DataAnnotations;
using Core.Shared.Connections.Contracts;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Contracts;

public class EditConnectionModel
{
    private readonly IConnectionTypeRegistry _connectionTypeRegistry;

    /// <summary>
    /// flag that indicates if connection test is supported
    /// </summary>
    public bool CanBeTested { get; private set; }

    public Connection Connection { get; }

    public string? Description
    {
        get => Connection.Description;
        set => Connection.Description = value;
    }

    public Guid Id
    {
        get => Connection.Id;
        set => Connection.Id = value;
    }

    [Required]
    [MinLength(1)]
    public string? Name
    {
        get => Connection.Name;
        set => Connection.Name = value;
    }

    public ConnectionType Type
    {
        get => Connection.Type;
        set
        {
            if (Connection.Type == value)
                return;

            Connection.Type = value;

            TypedConnection = CreateConnection(value, _connectionTypeRegistry);
            TypedConnectionChanged();
        }
    }

    public HashSet<Tag> Tags
    {
        get => Connection.Tags;
        set => Connection.Tags = value;
    }

    public TestConnectionResult? TestResult { get; set; }
    public IConnection? TypedConnection { get; private set; }

    public EditConnectionModel(Connection connection, IConnectionTypeRegistry connectionTypeRegistry)
    {
        Connection = connection;
        _connectionTypeRegistry = connectionTypeRegistry;
        TypedConnection = GetOrCreateConnection(connection, connectionTypeRegistry);

        TypedConnectionChanged();
    }


    public void TypedConnectionChanged()
    {
        if (TypedConnection is not null && _connectionTypeRegistry.TryGetConnectionSerializer(Type, out var serializer))
            Connection.Json = serializer.Serialize(TypedConnection);

        if (_connectionTypeRegistry.TryGetConnectionTest(Type, out _))
            CanBeTested = true;
        else
            CanBeTested = false;
    }

    private static IConnection? GetOrCreateConnection(Connection connection, IConnectionTypeRegistry connectionTypeRegistry)
    {
        if (connectionTypeRegistry.TryGetConnectionSerializer(connection.Type, out var serializer))
        {
            if (connection.Json is not null)
            {
                var result = serializer.Deserialize(connection.Json);

                if (result is not null)
                    return result;
            }

            return CreateConnection(connection.Type, connectionTypeRegistry);
        }
        return null;
    }

    private static IConnection CreateConnection(ConnectionType connectionType, IConnectionTypeRegistry connectionTypeRegistry)
    {
        if (connectionTypeRegistry.TryCreateConnection(connectionType, out var connectionInstance))
            return connectionInstance;

        throw new InvalidOperationException($"No connection instance could be created for type '{connectionType}'.");
    }
}
