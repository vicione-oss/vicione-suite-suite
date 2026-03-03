using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.Instance;
using Core.OS.Modules;
using Core.OS.Persistence;
using Microsoft.Extensions.Options;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Core.OS.Connections.Extensions;

internal static class IServiceCollectionExtensions
{
    public static void AddConnectionServices(this IServiceCollection services)
    {
        services.RegisterModuleDbContext<IConnectionDbContext, ConnectionDbContextSqlite, ConnectionDbContextPostgres>(
            Shared.Constants.SystemModuleId,
            typeof(SystemBackendModule),
            ConnectionDbContext.DbSchemaName,
            enableSynchronization: true);

        services.AddSingleton<IConnectionTypeRegistry, ConnectionTypeRegistry>(s =>
        {
            ConnectionTypeRegistry registry = new();
            registry
                .Register<MqttConnection, DefaultJsonConnectionSerializer<MqttConnection>, MqttConnectionTest>(
                    ConnectionType.Mqtt)
                .Register<HttpConnection, DefaultJsonConnectionSerializer<HttpConnection>, HttpConnectionTest>(
                    ConnectionType.Http)
                .Register<SQLiteConnection, DefaultJsonConnectionSerializer<SQLiteConnection>>(
                    ConnectionType.SQLite,
                    new DefaultJsonConnectionSerializer<SQLiteConnection>(),
                    new SQLiteConnectionTest(s.GetRequiredService<IOptions<InstanceOptions>>()))
                .Register<PostgresConnection, DefaultJsonConnectionSerializer<PostgresConnection>>(
                    ConnectionType.Postgres,
                    new DefaultJsonConnectionSerializer<PostgresConnection>(),
                    new PostgresConnectionTest());
            return registry;
        });
        services.AddMqttServices();
    }
}
