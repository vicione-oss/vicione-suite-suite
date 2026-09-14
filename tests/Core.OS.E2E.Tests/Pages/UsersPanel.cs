using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the "Users" control panel, reached through <see cref="SettingsPage"/>. Its grid
/// is virtualized, which is what makes it worth driving in a security test: the framework's
/// <c>Virtualize</c> spacers carry inline <c>style</c> attributes, the reason the policy relaxes
/// <c>style-src-attr</c>.
/// </summary>
public sealed class UsersPanel(IPage page)
{
    /// <summary>Title of the settings category the panel lives in.</summary>
    public const string Category = "User management";

    /// <summary>Title of the panel's entry in the settings navigation.</summary>
    public const string PanelTitle = "Users";

    // Only the active control panel renders content, so the panel's own grid element is unique.
    private ILocator Grid => page.Locator(".users-grid");

    private ILocator Rows => Grid.Locator("tbody tr");

    /// <summary>Opens the panel from the top bar and waits until its grid has rendered rows.</summary>
    public async Task Open()
    {
        var settings = new SettingsPage(page);

        await settings.Open();
        await settings.OpenPanel(Category, PanelTitle);

        // Rows only appear once the virtualized grid has run, which is the part under test.
        await Expect(Rows).Not.ToHaveCountAsync(0);
    }
}
