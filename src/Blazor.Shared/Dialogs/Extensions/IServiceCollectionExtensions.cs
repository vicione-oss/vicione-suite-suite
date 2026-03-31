using Microsoft.Extensions.DependencyInjection;
using ViciOne.Ui.Blazor.Components.Dialog.Extensions;

namespace Blazor.Shared.Dialogs.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDialogs(this IServiceCollection services)
    {
        services.AddDialog();

        return services;
    }
}
