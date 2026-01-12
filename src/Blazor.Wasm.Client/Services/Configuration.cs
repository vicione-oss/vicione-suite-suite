using Blazor.Shared.Services;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Wasm.Client.Services;

public static class Configuration
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        //Common
        services
            .AddSingleton<IClientModuleService, ClientModuleService>()
            .AddTransient<INavigationService, NavigationService>()
            .AddScoped<IInstanceInformationProvider, ClientInstanceInformationProvider>()
            .AddScoped<IClusterInformationProvider, ClientClusterInformationProvider>();

        return services;
    }
}
