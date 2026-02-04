using Blazor.Shared.Onboarding.NavTiles;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.Onboarding.Extensions;

public static class INavTileRegistryExtensions
{
    extension(INavTileRegistry<SharedClientModule> registry)
    {
        public void AddOnboardingNavTile()
        {
            if (registry.Any(i => i.ComponentType == typeof(OnboardingNavTile)))
                return;

            var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);

            registry.Add<OnboardingNavTile>(OnboardingNavTile.Id, linkTarget: Constants.Route, group: NavTileGroup.Administration,
                authorizationRequirement: accessLevelAuthorizationRequirement);
        }

        public void RemoveOnboardingNavTile()
            => registry.Remove(OnboardingNavTile.Id);
    }
}
