namespace Core.OS.Connections.Mqtt;

internal static class MqttConfiguration
{
    // this is done in backend directly because we can't access webbuilder here! (yet?) 
    //public static IWebHostBuilder UseKestrelMqtt(this IWebHostBuilder builder)
    //{
    //    builder.UseKestrel(options =>
    //    {
    //        options.Listen(IPAddress.Loopback, 1883, listenOptions => listenOptions.UseMqtt());
    //    });
    //    return builder;
    //}

    internal static IServiceCollection AddMqttServices(this IServiceCollection services)
    {
        services.AddOptions<MqttClientOptions>()
            .BindConfiguration(MqttClientOptions.ConfigSection)
            .ValidateDataAnnotations();

        return services;
    }

    public static MqttClientOptions GetMqttClientOptions(this IConfiguration config)
        => config.GetSection(MqttClientOptions.ConfigSection).Get<MqttClientOptions>() ?? new MqttClientOptions();
}
