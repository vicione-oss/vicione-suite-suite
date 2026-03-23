using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.Module.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddModuleManagement(this IServiceCollection services)
    {
        services.TryAddScoped<IModuleManagementService, ModuleManagementService>();

        services.AddModuleManagementControlPanel();
        services.AddModuleDetailControlPanel();

        return services;
    }
}
