using Blazor.Shared.NotificationArea.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.NotificationArea.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddNotificationArea(this IServiceCollection services)
    {
        services.AddScoped<IActiveNotificationElementPolicy, SingleActiveNotificationElementPolicy>();

        return services;
    }
}
