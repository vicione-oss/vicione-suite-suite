using Core.OS.Connections.Extensions;
using Core.OS.DbContext;
using Core.OS.Tests.Connections;
using Sdk.Connections.Contracts;

namespace Core.OS.Tests.Extensions;

internal static class ConnectionDbContextExtensions
{
    extension(IConnectionDbContext dbContext)
    {
        public Connection SeedDatabaseConnection(HashSet<Tag>? tags = null)
        {
            var entry = dbContext.Connections.Add(ConnectionFactory.CreateSQLiteConnection(tags: tags));
            dbContext.SaveChanges();
            return entry.Entity;
        }

        public Connection SeedMqttConnection(HashSet<Tag>? tags = null)
        {
            var entry = dbContext.Connections.Add(ConnectionFactory.CreateMqttConnection("MqttService", tags: tags));
            dbContext.SaveChanges();
            return entry.Entity;
        }

        public Connection SeedMqttConnection(Guid instanceId, MqttConnectionType protocol)
        {
            var connection = ConnectionFactory.CreateMqttConnection("MqttService");
            connection.SetInstanceMetadata(instanceId, protocol);

            var entry = dbContext.Connections.Add(connection);
            dbContext.SaveChanges();
            return entry.Entity;
        }

        public Connection SeedCloudConnection(HashSet<Tag>? tags = null)
        {
            var entry = dbContext.Connections.Add(ConnectionFactory.HttpConnection(tags));
            dbContext.SaveChanges();
            return entry.Entity;
        }
    }
}
