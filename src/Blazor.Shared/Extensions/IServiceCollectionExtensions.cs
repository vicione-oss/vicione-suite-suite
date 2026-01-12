using Blazor.Shared.Authorization.Extensions;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.MessageBanner.Extensions;
using Blazor.Shared.Module.Extensions;
using Blazor.Shared.Mqtt.Enums;
using Blazor.Shared.Mqtt.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.NotificationArea.Extensions;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Profile.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.SystemInformation.Extensions;
using Blazor.Shared.UserInterface.Extensions;
using Blazor.Shared.UserManagement.Extensions;
using Core.Shared.HostManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.NavTiles.Extensions;
using Sdk.Client.NotificationArea.Extensions;
using Sdk.Client.Services;
using ViciOne.Ui.Blazor.Components.Popup.Extensions;
using ViciOne.Ui.Blazor.Components.SectionRail.Extensions;
using ViciOne.Ui.Blazor.Components.Tooltip.Extensions;
using ViciOne.Ui.Shared.Dx.Components.Scrolling.Extensions;

namespace Blazor.Shared.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorShared(this IServiceCollection services)
    {
        services.AddSharedUiServices();

        services.AddMqttViewer();

        services.AddNavTiles<SharedClientModule>()
            .AddNotificationElements<SharedClientModule>();

        services.AddBlazorSharedAuthorization()
            .AddSettings()
            .AddNetwork()
            .AddConnectionManagement()
            .AddInstanceManagement()
            .AddModuleManagement()
            .AddUserManagement()
            .AddUserInterfaceControlPanels();

        services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();

        services.AddOnboarding();

        return services;
    }

    internal static IServiceCollection AddLoginDesignService(this IServiceCollection services)
    {
        services.TryAddSingleton<LoginDesignService>();

        return services;
    }

    private static IServiceCollection AddSharedUiServices(this IServiceCollection services)
    {
        // Components
        services.AddPopup();

        // Common
        services.AddScoped<ILayoutService, LayoutService>()
            .AddScoped<IControlPanelService, ControlPanelService>()
            .AddTransient<IJsInterop, JsInterop>()
            .AddLoginDesignService()
            .AddProfile()
            .AddSystemInformation()
            .AddMessageBanner()
            .AddTooltip()
            .AddNotificationArea()
            .AddScoped<ISuiteControlService, SuiteControlService>()
            .AddScrolling()
            .AddSingleton<CopyrightYearProvider>();

        return services;
    }

    private static IServiceCollection AddMqttViewer(this IServiceCollection services)
    {
        services.AddSectionRail<SectionId>();

        services.AddScoped<IMqttService, MqttService>();
        services.AddScoped<MqttViewerComponentService>();

        return services;
    }
}
