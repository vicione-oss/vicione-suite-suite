using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Microsoft.EntityFrameworkCore;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Serilog;

namespace Core.OS.Connections.Extensions;

internal static class IConnectionDbContextExtensions
{
    extension(IConnectionDbContext dbContext)
    {
        /// <summary>
        /// if connection has tags that already exist in database there entity state will be Added. That leads to
        /// Npgsql.PostgresException (0x80004005): 23505: duplicate key value violates unique constraint "PK_Tags" or
        /// SQLite Error 19: 'UNIQUE constraint failed: Tags.Id' on SaveChangesAsync.
        /// To avoid that we have to replace the message tags with existing tag entities  
        /// </summary>
        public async Task<HashSet<Tag>> ProcessTags(Connection connection, List<Tag> addedTags, List<Tag> changedTags, CancellationToken cancellationToken)
        {
            var existingTags = new HashSet<Tag>();
            var allTags = await dbContext.Tags.ToListAsync(cancellationToken);
            foreach (var tag in connection.Tags)
            {
                var existingTag = allTags.FirstOrDefault(t => t == tag);
                if (existingTag is null)
                {
                    addedTags.Add(tag);
                    continue;
                }

                if (existingTag.Text != tag.Text)
                {
                    existingTag.Text = tag.Text;
                    changedTags.Add(existingTag);
                    continue;
                }

                existingTags.Add(existingTag);
            }

            return existingTags.Union(addedTags).ToHashSet();
        }

        public async Task SeedMqttBrokerConnections(MqttClientOptions mqttClientOptions,
            Guid instanceId,
            CancellationToken stoppingToken)
        {
            // ensure we return the entity reference instead of the static one
            // to prevent change tracker issues
            var systemDefault = dbContext.Tags.FirstOrDefault(k => k.Id == ConnectionConstants.Tags.SystemDefault.Id);
            if (systemDefault is null)
            {
                Log.Logger.Error("System connection tag '{Tag}' is missing", nameof(ConnectionConstants.Tags.SystemDefault));
            }

            if (mqttClientOptions.ServiceClient is not null)
                await dbContext.SeedMqttServiceConnection(
                    mqttClientOptions.ServiceClient,
                    instanceId,
                    systemDefault,
                    stoppingToken);
            else
                await dbContext.RemoveMqttConnection(instanceId, MqttConnectionType.TCP, stoppingToken);

            if (mqttClientOptions.WebSocketClient is not null)
                await dbContext.SeedMqttWebsocketConnection(
                    mqttClientOptions.WebSocketClient,
                    instanceId,
                    systemDefault,
                    stoppingToken);
            else
                await dbContext.RemoveMqttConnection(
                    instanceId,
                    MqttConnectionType.WebSocket,
                    stoppingToken);

            await dbContext.Instance.SaveChangesAsync(stoppingToken);
        }

        private Task SeedMqttServiceConnection(MqttConnectionOptions options,
            Guid instanceId,
            Tag? systemDefault,
            CancellationToken stoppingToken)
        {
            var mqtt = SuiteConnectionFactory.CreateMqttServiceConnection(options);
            if (mqtt is null)
                return Task.CompletedTask;

            return SeedMqttConnection(dbContext,
                mqtt,
                SuiteConnectionFactory.MqttServiceName,
                SuiteConnectionFactory.MqttServiceDescription,
                instanceId,
                systemDefault,
                stoppingToken);
        }

        private Task SeedMqttWebsocketConnection(MqttConnectionOptions options,
            Guid instanceId,
            Tag? systemDefault,
            CancellationToken stoppingToken)
        {
            var mqtt = SuiteConnectionFactory.CreateMqttWebsocketConnection(options);
            if (mqtt is null)
                return Task.CompletedTask;

            return SeedMqttConnection(dbContext,
                mqtt,
                SuiteConnectionFactory.MqttWebsocketName,
                SuiteConnectionFactory.MqttWebsocketDescription,
                instanceId,
                systemDefault,
                stoppingToken);
        }

        public async Task SeedSystemDefaultTag(CancellationToken stoppingToken)
        {
            if (dbContext.Tags.Any(k => k.Id == ConnectionConstants.Tags.SystemDefault.Id))
                return;

            dbContext.Tags.Add(ConnectionConstants.Tags.SystemDefault);

            await dbContext.Instance.SaveChangesAsync(stoppingToken);
        }
    }

    private static async Task SeedMqttConnection(IConnectionDbContext dbContext,
        MqttConnection mqtt,
        string name,
        string description,
        Guid instanceId,
        Tag? systemDefault,
        CancellationToken stoppingToken)
    {
        //cannot filter in database yet. Maybe in EF Core 7. See: https://github.com/dotnet/efcore/issues/4021 
        // todo: check if this was related to the sync issue
        var existing = (await dbContext.Connections.ToListAsync(stoppingToken))
            .FirstOrDefaultInstanceMqttConnection(instanceId, mqtt.Protocol);
        if (existing is null)
        {
            var connection = SuiteConnectionFactory.CreateInstanceMqttConnection(instanceId, mqtt, name, description);

            if (systemDefault != null)
                connection.Tags.Add(systemDefault);

            dbContext.Connections.Add(connection);
            return;
        }

        existing.SetBaseProperties(name, description, true);
        existing.SetMqttConnection(mqtt);

        dbContext.Connections.Update(existing);
    }

    private static async Task RemoveMqttConnection(this IConnectionDbContext dbContext,
        Guid instanceId,
        MqttConnectionType protocol,
        CancellationToken stoppingToken)
    {
        //cannot filter in database yet. Maybe in EF Core 7. See: https://github.com/dotnet/efcore/issues/4021 
        var existing = (await dbContext.Connections.ToArrayAsync(stoppingToken))
            .FirstOrDefaultInstanceMqttConnection(instanceId, protocol);
        if (existing is not null)
            dbContext.Connections.Remove(existing);
    }
}
