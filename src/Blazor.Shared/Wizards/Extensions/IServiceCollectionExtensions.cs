using Blazor.Shared.Popup.Extensions;
using Blazor.Shared.Wizards.Factories;
using Blazor.Shared.Wizards.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Wizards.Services;
using ViciOne.Ui.Blazor.Components.Popup.Extensions;

namespace Blazor.Shared.Wizards.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddWizards(this IServiceCollection services)
    {
        services.AddPopup();

        services.AddTransient<IWizardBodyContentRenderCycle, WizardBodyContentRenderCycle>();
        services.AddScoped<IWizardPageRegistryFactory, WizardPageRegistryFactory>();
        services.AddScoped<IWizardContentComponentTypeProvider, WizardContentComponentTypeProvider>();
        services.AddScoped<IWizardStepFactory, WizardStepFactory>();

        services.AddScoped<WizardPageEditFactory>();
        services.AddWizardPageEditRegistry();

        services.AddTransient<IWizardState, WizardState>();

        services.AddLoadingIndicationPlacementBehavior();

        return services;
    }

    internal static IServiceCollection AddWizardPageEditRegistry(this IServiceCollection services)
        => services.AddScoped<IWizardPageEditRegistry, WizardPageEditRegistry>();
}
