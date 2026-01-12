using Core.OS.Connections.Extensions;
using Core.OS.DbContext;
using Core.OS.Tests.Connections;
using Sdk.Connections.Contracts;

namespace Core.OS.Tests.Extensions;

internal static class ConnectionDbContextExtensions
{
    public static Connection SeedDatabaseConnection(this IConnectionDbContext dbContext, HashSet<Tag>? tags = null)
    {
        var entry = dbContext.Connections.Add(ConnectionFactory.CreateSQLiteConnection(tags: tags));
        dbContext.Instance.SaveChanges();
        return entry.Entity;
    }

    public static Connection SeedMqttConnection(this IConnectionDbContext dbContext, HashSet<Tag>? tags = null)
    {
        var entry = dbContext.Connections.Add(ConnectionFactory.CreateMqttConnection("MqttService", tags: tags));
        dbContext.Instance.SaveChanges();
        return entry.Entity;
    }

    public static Connection SeedMqttConnection(this IConnectionDbContext dbContext, Guid instanceId, MqttConnectionType protocol)
    {
        var connection = ConnectionFactory.CreateMqttConnection("MqttService");
        connection.SetInstanceMetadata(instanceId, protocol);

        var entry = dbContext.Connections.Add(connection);
        dbContext.Instance.SaveChanges();
        return entry.Entity;
    }


    public static Connection SeedCloudConnection(this IConnectionDbContext dbContext, HashSet<Tag>? tags = null)
    {
        var entry = dbContext.Connections.Add(ConnectionFactory.HttpConnection(tags));
        dbContext.Instance.SaveChanges();
        return entry.Entity;
    }
}
