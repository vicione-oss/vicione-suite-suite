using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Core.OS.Tests.Connections;

public static class ConnectionFactory
{
    public static List<Connection> CreateConnections(int count = 2)
    {
        var ret = new List<Connection>();

        for (var i = 1; i <= count; i++)
        {
            ret.Add(new Connection()
            {
                Id = Guid.NewGuid(),
                Json = $"ConnectionString {i}",
                Description = $"Description {i}",
                Name = $"Connection {i}",
                Type = i % 2 == 0 ? ConnectionType.Database : ConnectionType.Mqtt,
            });
        }

        return ret;
    }

    public static Connection HttpConnection(HashSet<Tag>? tags = null) => new()
    {
        Json = "https://localhost/greatapi",
        Description = "a fancy cloud service api",
        Id = Guid.NewGuid(),
        Name = "CloudStorage",
        Type = ConnectionType.AzureIotHub,
        Tags = tags ?? []
    };

    public static Connection UpdatedConnection(Guid connectionId, HashSet<Tag>? tags = null) => new()
    {
        Json = "ConnectionString updated",
        Description = "Description updated",
        Id = connectionId,
        Name = "Connection updated",
        Type = ConnectionType.AzureIotHub,
        Tags = tags ?? []
    };

    public static Connection CreateMqttConnection(string host = "127.0.0.1", int port = 1883, MqttConnectionType protocol = MqttConnectionType.TCP, HashSet<Tag>? tags = null)
    {
        var connection = new Connection
        {
            Description = "MqttService description",
            Id = Guid.NewGuid(),
            Name = "MqttService",
            Tags = tags ?? []
        };

        connection.SetMqttConnection(
            new MqttConnection { Address = host, Port = port, Protocol = protocol });

        return connection;
    }


    public static Connection CreateDatabaseConnection(string connectionString = "XpoProvider=SQLite; Data Source=AppData/nwind.db;", DatabaseConnectionType dbType = DatabaseConnectionType.XPO, HashSet<Tag>? tags = null)
    {
        var connection = new Connection
        {
            Description = "Northwind",
            Id = Guid.NewGuid(),
            Name = "nwind",
            Type = ConnectionType.Database,
            Tags = tags ?? []
        };
        connection.SetDatabaseConnection(
            new DatabaseConnection { DatabaseType = dbType, ConnectionString = connectionString });

        return connection;
    }
}
