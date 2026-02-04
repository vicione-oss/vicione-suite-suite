using Blazor.Shared.NotificationArea.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NotificationArea.Services;

namespace Blazor.Shared.NotificationArea.Extensions;

internal static class IServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        internal IServiceCollection AddNotificationArea()
        {
            services.AddNotificationElementInfrastructure();
            services.AddScoped<IActiveNotificationElementPolicy, SingleActiveNotificationElementPolicy>();

            return services;
        }

        internal IServiceCollection AddNotificationElementInfrastructure()
        {
            services.AddScoped<INotificationElementRegistryFactory, NotificationElementRegistryFactory>();

            return services;
        }
    }
}
