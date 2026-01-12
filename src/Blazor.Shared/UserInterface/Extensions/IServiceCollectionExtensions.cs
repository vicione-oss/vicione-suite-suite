using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Extensions;
using Blazor.Shared.UserInterface.ControlPanels.Language.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Blazor.Shared.UserInterface.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUserInterfaceControlPanels(this IServiceCollection services)
    {
        services.AddDateAndTimeControlPanel()
            .AddLanguageControlPanel();

        // Currently disabled as functionality is tbd:
        //.AddThemeControlPanel();

        return services;
    }
}
