using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Components;

public sealed partial class SetPasswordLinkButton
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private void OpenSetPasswordPage()
        => NavigationManager.NavigateTo(IdentityRoutes.SetPasswordRoute, forceLoad: true);
}
