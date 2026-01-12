using Blazor.Shared.Module.ControlPanels.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Shared.Module.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddModuleManagement(this IServiceCollection services)
    {
        services.AddModuleManagementControlPanel();

        return services;
    }
}
