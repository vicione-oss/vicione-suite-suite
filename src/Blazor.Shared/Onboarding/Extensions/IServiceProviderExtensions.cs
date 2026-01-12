using Core.Shared.Instance.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.NavTiles.Services;
using Sdk.Instance;

namespace Blazor.Shared.Onboarding.Extensions;

public static class IServiceProviderExtensions
{
    public static IServiceProvider UseOnboarding(this IServiceProvider services)
    {
        var instanceInformationProvider = services.GetRequiredService<IInstanceInformationProvider>();
        var instanceId = instanceInformationProvider.Local.Id;

        var onboardingStateStore = services.GetRequiredService<IOnboardingStateStore>();
        var onboardingState = onboardingStateStore.GetOnboardingState(instanceId);

        if (!onboardingState.Completed && !onboardingState.ShowWizardWhenNotCompleted)
        {
            var registry = services.GetRequiredService<INavTileRegistry<SharedClientModule>>();

            registry.AddOnboardingNavTile();
        }

        return services;
    }
}
