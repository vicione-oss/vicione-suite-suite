using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the "OpenID provider" control panel, reached through <see cref="SettingsPage"/>.
/// An entered provider reaches the instance when the settings popup's confirm button is used, and
/// clearing both fields removes it again.
/// </summary>
public sealed class OpenIdProviderPanel(IPage page)
{
    /// <summary>Title of the settings category the panel lives in.</summary>
    public const string Category = "User management";

    /// <summary>Title of the panel's entry in the settings navigation.</summary>
    public const string PanelTitle = "OpenID provider";

    private ILocator AuthorityField => page.GetByPlaceholder("Authority");

    private ILocator ClientIdField => page.GetByPlaceholder("Client ID");

    private ILocator RedirectUriField => page.GetByPlaceholder("Redirect URI");

    /// <summary>Enters a provider; the settings popup still has to be confirmed.</summary>
    public async Task EnterProvider(string authority, string clientId)
    {
        await Fill(AuthorityField, authority);
        await Fill(ClientIdField, clientId);
    }

    /// <summary>The redirect URI the panel asks the admin to register at the provider.</summary>
    public Task<string> RedirectUri() => RedirectUriField.InputValueAsync();

    /// <summary>Clears the provider; the settings popup still has to be confirmed.</summary>
    public Task ClearProvider() => EnterProvider(string.Empty, string.Empty);

    // Tab out after filling: the text boxes commit on change, which needs the field to lose focus.
    private static async Task Fill(ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }
}
