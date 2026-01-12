using JiTChat.Client.Contracts;
using JiTChat.Client.Localization;
using JiTChat.Client.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Modules;
using Sdk.Client.Modules.Localization.Extensions;
using Sdk.Client.NotificationArea.Extensions;

namespace JiTChat.Client;

public sealed class JiTChatClientModule : ClientModule
{
    public override Action<IServiceCollection> Configure => (services) =>
    {
        services.AddScoped<IJiTChatService, JiTChatService>();

        services.AddLocalization<JiTChatClientModule, Localizer>();
        services.AddNotificationElements<JiTChatClientModule>();
    };
}
