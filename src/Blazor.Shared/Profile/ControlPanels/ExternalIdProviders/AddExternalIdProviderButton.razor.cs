using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Profile.ControlPanels.ExternalIdProviders;

public partial class AddExternalIdProviderButton(NavigationManager navigationManager) : ComponentBase
{
    private void OpenLinkingPage()
        => navigationManager.NavigateTo(IdentityRoutes.AddExternalLoginRoute, forceLoad: true);
}
