using Core.OS.MessageBus.MassTransit.Configuration;
using MassTransit;

namespace Core.OS.MessageBus.Extensions;

internal static class IConfigurationExtensions
{
    internal static MessageBusOptions GetMessageBusOptions(this IConfiguration config)
    {
        var configItem = config.GetSection(MessageBusOptions.ConfigSection).Get<MessageBusOptions>() ?? new();
        configItem.Connection = config.GetSection(MessageBusOptions.TransportOptionsConfigSection).Get<RabbitMqTransportOptions>() ?? new();
        return configItem;
    }
}
