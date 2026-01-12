using Core.OS.Connections.Mqtt;
using Core.OS.DbContext;
using Core.OS.Instance;
using Microsoft.Extensions.Options;

namespace Core.OS.Connections.Extensions;

internal static class IServiceProviderExtensions
{
    public static async Task SeedConnections(this IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var connectionContext = scopedServices.GetRequiredService<IConnectionDbContext>();
        var mqttOptions = scopedServices.GetRequiredService<IOptions<MqttClientOptions>>().Value;
        var instanceId = scopedServices.GetRequiredService<ILocalInstanceInformationProvider>().ReadLocalInstanceId();

        await connectionContext.SeedSystemDefaultTag(cancellationToken);

        await connectionContext.SeedMqttBrokerConnections(mqttOptions, instanceId, cancellationToken);
    }
}
