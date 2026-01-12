using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Blazor.Shared.Tests.Connections.Extensions;

public static class ConnectionFactory
{
    public static Connection DefinedConnection
    {
        get
        {
            var connection = new Connection()
            {
                Description = "Northwind",
                Id = Guid.Parse("10806844-cac5-4ac1-8d16-58a7357212a4"),
                Name = "nwind",
                Type = ConnectionType.Database,
            };

            connection.SetDatabaseConnection(new() { ConnectionString = "XpoProvider=SQLite; Data Source=AppData/nwind.db;", DatabaseType = DatabaseConnectionType.XPO, });

            return connection;
        }
    }

    public static Connection DatabaseConnection
    {
        get
        {
            var connection = new Connection()
            {
                Description = "Northwind",
                Id = Guid.NewGuid(),
                Name = "nwind",
                Type = ConnectionType.Database,
            };

            connection.SetDatabaseConnection(new() { ConnectionString = "XpoProvider=SQLite; Data Source=AppData/nwind.db;", DatabaseType = DatabaseConnectionType.XPO, });

            return connection;
        }
    }

    public static Connection HttpConnection
    {
        get
        {
            var connection = new Connection()
            {
                Description = "Some http api",
                Id = Guid.NewGuid(),
                Name = "HttpApi",
                Type = ConnectionType.Http,
            };

            connection.SetHttpConnection(new() { BaseAddress = "https://www.google.com" });

            return connection;
        }
    }

    public static Connection AzureIotConnection
    {
        get
        {
            var connection = new Connection()
            {
                Description = "Some azure iot api",
                Id = Guid.NewGuid(),
                Name = "AzureIoT",
                Type = ConnectionType.AzureIotHub,
            };

            connection.SetAzureIotHubConnection(new()
            {
                Hostname = "hostname",
                SharedAccessSignatureKey = "signature key",
                SharedAccessSignatureKeyName = "signature key name"
            });

            return connection;
        }
    }
    public static Connection MqttConnection
    {
        get
        {
            var connection = new Connection
            {
                Description = "MqttService description",
                Id = Guid.NewGuid(),
                Name = "MqttService",
            };

            connection.SetMqttConnection(
                new MqttConnection
                {
                    Address = "127.0.0.1",
                    Port = 1883,
                    Protocol = MqttConnectionType.TCP,
                    Password = "password",
                    Username = "username",
                    ClientId = "clientId",
                    WillMessage = "WillMessage",
                    WillTopic = "WillTopic",
                    WillRetain = true,
                    CleanSession = false
                });

            return connection;
        }
    }

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

    public static Connection UpdatedConnection(Guid connectionId) => new()
    {
        Json = "ConnectionString updated",
        Description = "Description updated",
        Id = connectionId,
        Name = "Connection updated"
    };
}
