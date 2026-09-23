using Core.OS.Configuration;
using Core.OS.Extensions;

namespace Core.OS.Connections.Mqtt;

internal static class MqttConfiguration
{
    // The Kestrel MQTT listener is set up in the backend instead, because the web builder is not
    // reachable from here.

    internal static IServiceCollection AddMqttServices(this IServiceCollection services)
    {
        services.AddValidatedOptions<MqttClientOptions, MqttClientOptionsValidator>(MqttClientOptions.ConfigSection);

        return services;
    }

    public static MqttClientOptions GetMqttClientOptions(this IConfiguration config)
        => config.GetSection(MqttClientOptions.ConfigSection).Get<MqttClientOptions>() ?? new MqttClientOptions();
}
