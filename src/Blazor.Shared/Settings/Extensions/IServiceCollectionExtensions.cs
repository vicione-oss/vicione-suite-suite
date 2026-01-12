using System.Security.Claims;
using Blazor.Shared.Popup.Extensions;
using Blazor.Shared.Settings.Services;
using Core.Shared.UserManagement.Comparers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Blazor.Components.CheckBox.Extensions;
using ViciOne.Ui.Blazor.Components.SpinEdit.Extensions;

namespace Blazor.Shared.Settings.Extensions;

internal static class IServiceCollectionExtensions
{
    public static IServiceCollection AddSettings(this IServiceCollection services)
    {
        services.AddSettingsPopup();

        services.TryAddSingleton<IEqualityComparer<Claim>, ClaimEqualityComparer>();
        services.AddScoped<IControlPanelRegistryItemCache, ControlPanelRegistryItemCache>();
        services.AddTransient<SettingsModuleService>();
        services.AddScoped<SettingsModuleState>();
        services.AddScoped<IActiveControlPanelDescriptorProvider, ActiveControlPanelDescriptorProvider>();
        services.AddScoped<IActiveControlPanelPageProvider, ActiveControlPanelPageProvider>();
        services.AddScoped<IControlPanelRequest, ControlPanelRequest>();
        services.AddScoped<INavigateBackRequest, NavigateBackRequest>();

        services.AddCheckBox();
        services.AddIntSpinEdit();
        services.AddFloatSpinEdit();

        services.AddLoadingIndicationPlacementBehavior();

        return services;
    }

    internal static IServiceCollection AddSettingsPopup(this IServiceCollection services)
    {
        services.AddScoped<ISettingsPopupRequest, SettingsPopupRequest>();
        services.AddScoped<ISettingsPopupState, SettingsPopupState>();

        return services;
    }
}
