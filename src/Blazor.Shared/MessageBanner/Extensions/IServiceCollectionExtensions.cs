using Blazor.Shared.MessageBanner.Components;
using Blazor.Shared.MessageBanner.NotificationArea;
using Blazor.Shared.MessageBanner.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Services;

namespace Blazor.Shared.MessageBanner.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddMessageBanner(this IServiceCollection services)
    {
        services.AddScoped<IMessageBannerService, MessageBannerService>();
        services.AddScoped<IMessageBannerMediator, MessageBannerMediator>();
        services.AddScoped<MessageBannerDialogState>();
        services.AddScoped<MessageBannerNotificationElementIconState>();

        return services;
    }
}
