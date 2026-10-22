using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the page that starts linking an account to the external ID provider
/// (<c>/account/manage/add-external-login</c>), opened from <see cref="ExternalIdProvidersPanel"/>.
/// </summary>
public sealed class AddExternalLoginPage(IPage page)
{
    private ILocator LinkButton => page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Link with OpenID" });

    /// <summary>Starts linking, which sends the browser to the provider's sign-in.</summary>
    public Task Link() => LinkButton.ClickAsync();
}
