using Blazor.Shared.Popup.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blazor.Shared.Popup.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLoadingIndicationPlacementBehavior(this IServiceCollection services)
    {
        services.TryAddTransient<ILoadingIndicationPlacementBehavior, LoadingIndicationPlacementBehavior>();

        return services;
    }
}
