using Blazor.Shared.Settings.DateAndTime.Services;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Components;
using Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Extensions;

namespace Blazor.Shared.UserInterface.ControlPanels.DateAndTime.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDateAndTimeControlPanel(this IServiceCollection services)
    {
        services.AddScoped<ITimeZoneDescriptorProvider, TimeZoneDescriptorProvider>();

        services.AddControlPanel<SharedClientModule, DateAndTimeControlPanel, DateAndTimeControlPanelState>()
            .WithAutoDiscovery<DateAndTimeControlPanelDescriptor>()
            .WithSaveHandler<DateAndTimeControlPanelSaveHandler>()
            .WithResetHandler<DateAndTimeControlPanelResetHandler>();

        return services;
    }
}
