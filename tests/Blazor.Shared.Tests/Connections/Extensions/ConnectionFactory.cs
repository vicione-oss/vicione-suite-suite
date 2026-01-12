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
                Type = ConnectionType.SQLite,
            };

            connection.SetSQLiteConnection(new() { ConnectionString = "XpoProvider=SQLite; Data Source=AppData/nwind.db;", });

            return connection;
        }
    }

    public static Connection SQLiteConnection
    {
        get
        {
            var connection = new Connection()
            {
                Description = "Northwind",
                Id = Guid.NewGuid(),
                Name = "nwind",
                Type = ConnectionType.SQLite,
            };

            connection.SetSQLiteConnection(new() { ConnectionString = "XpoProvider=SQLite; Data Source=AppData/nwind.db;", });

            return connection;
        }
    }

    public static Connection PostgresConnection
    {
        get
        {
            var connection = new Connection()
            {
                Description = "Postgres",
                Id = Guid.NewGuid(),
                Name = "test",
                Type = ConnectionType.Postgres,
            };

            connection.SetPostgresConnection(new() { ConnectionString = "Host=localhost;Database=test;Username=postgres;Password=password;" });

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
                Type = i % 2 == 0 ? ConnectionType.SQLite : ConnectionType.Mqtt,
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
