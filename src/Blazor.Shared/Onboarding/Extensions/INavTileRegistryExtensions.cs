using Blazor.Shared.Onboarding.NavTiles;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.Onboarding.Extensions;

public static class INavTileRegistryExtensions
{
    public static void AddOnboardingNavTile(this INavTileRegistry<SharedClientModule> registry)
    {
        if (registry.Any(i => i.ComponentType == typeof(OnboardingNavTile)))
            return;

        var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);

        registry.Add<OnboardingNavTile>(OnboardingNavTile.Id, linkTarget: Constants.Route, group: NavTileGroup.Administration,
            authorizationRequirement: accessLevelAuthorizationRequirement);
    }

    public static void RemoveOnboardingNavTile(this INavTileRegistry<SharedClientModule> registry)
        => registry.Remove(OnboardingNavTile.Id);
}
