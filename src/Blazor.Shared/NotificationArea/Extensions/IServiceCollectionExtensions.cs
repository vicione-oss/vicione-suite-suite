using Blazor.Shared.NotificationArea.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.NotificationArea.Extensions;

internal static class IServiceCollectionExtensions
{
    internal static IServiceCollection AddNotificationArea(this IServiceCollection services)
    {
        services.AddNotificationElementInfrastructure();
        services.AddScoped<IActiveNotificationElementPolicy, SingleActiveNotificationElementPolicy>();

        return services;
    }

    internal static IServiceCollection AddNotificationElementInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<INotificationElementRegistryFactory, NotificationElementRegistryFactory>();

        return services;
    }
}
