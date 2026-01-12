using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.DbContext.Extensions;
using Core.OS.Instance;
using Microsoft.Extensions.Options;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Core.OS.Connections.Extensions;

internal static class IServiceCollectionExtensions
{
    public static void AddConnectionServices(this IServiceCollection services)
    {
        services.AddCoreDbContext<IConnectionDbContext, ConnectionDbContextSqlite, ConnectionDbContextPostgres>(
            ConnectionDbContext.DbSchemaName);
        services.AddSingleton<IConnectionTypeRegistry, ConnectionTypeRegistry>(s =>
        {
            ConnectionTypeRegistry registry = new();
            registry
                .Register<MqttConnection, DefaultJsonConnectionSerializer<MqttConnection>, MqttConnectionTest>(ConnectionType.Mqtt)
                .Register<HttpConnection, DefaultJsonConnectionSerializer<HttpConnection>, HttpConnectionTest>(ConnectionType.Http)
                .Register<DatabaseConnection, DefaultJsonConnectionSerializer<DatabaseConnection>>(ConnectionType.Database, new(), new DatabaseConnectionTest(s.GetRequiredService<IOptions<InstanceOptions>>()))
                .Register<AzureIotHubConnection, DefaultJsonConnectionSerializer<AzureIotHubConnection>, AzureIotHubConnectionTest>(ConnectionType.AzureIotHub);
            return registry;
        });
        services.AddMqttServices();
    }
}
