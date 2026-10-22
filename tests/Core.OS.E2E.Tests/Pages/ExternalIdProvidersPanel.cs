using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the "External ID providers" control panel, reached through
/// <see cref="SettingsPage"/>, where signed-in users link their account to the configured provider.
/// </summary>
public sealed class ExternalIdProvidersPanel(IPage page)
{
    /// <summary>Title of the settings category the panel lives in.</summary>
    public const string Category = "Profile";

    /// <summary>Title of the panel's entry in the settings navigation.</summary>
    public const string PanelTitle = "External ID providers";

    private ILocator AddExternalLoginButton => page.Locator(".add-external-login-button");

    /// <summary>Opens <see cref="AddExternalLoginPage"/>, where linking starts.</summary>
    public Task AddExternalLogin() => AddExternalLoginButton.ClickAsync();
}
