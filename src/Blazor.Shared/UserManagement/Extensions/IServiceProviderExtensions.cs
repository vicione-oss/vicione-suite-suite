using Blazor.Shared.UserManagement.NavTiles;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Authorization;
using Sdk.Client.NavTiles.Enums;
using Sdk.Client.NavTiles.Services;

namespace Blazor.Shared.UserManagement.Extensions;

internal static class IServiceProviderExtensions
{
    extension(IServiceProvider serviceProvider)
    {
        public IServiceProvider UseUserManagementNavTiles()
        {
            try
            {
                var registry = serviceProvider.GetRequiredService<INavTileRegistry<SharedClientModule>>();
                if (registry.Any(i => i.ComponentType == typeof(ManageUsersNavTile)))
                    return serviceProvider;

                var accessLevelAuthorizationRequirement = new AccessLevelAuthorizationRequirement(SharedClientModule.ModuleId, AccessLevel.Full);

                registry.Add<ManageUsersNavTile>(ManageUsersNavTile.Id, group: NavTileGroup.Administration,
                    authorizationRequirement: accessLevelAuthorizationRequirement);
            }
            catch (ObjectDisposedException)
            {
                // Can happen during shutdown.
            }

            return serviceProvider;
        }
    }
}
