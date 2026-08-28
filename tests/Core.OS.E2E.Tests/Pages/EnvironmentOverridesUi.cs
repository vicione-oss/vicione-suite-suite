using Microsoft.Playwright;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// One instance's environment-overrides panel, opened and ready to work with: the settings popup and
/// the panel inside it. Bundled because every test needs both — the panel edits the grid, the popup
/// around it stores the result — and because the master/slave tests hold several of them at once, one
/// per instance.
/// </summary>
internal sealed class EnvironmentOverridesUi(IPage page, SettingsPage settings, EnvironmentOverridesPanel panel)
{
    public SettingsPage Settings => settings;

    public EnvironmentOverridesPanel Panel => panel;

    /// <summary>Signs in and opens the panel.</summary>
    public static async Task<EnvironmentOverridesUi> Open(IPage page, string userName, string password)
    {
        await new LoginPage(page).SignIn(userName, password);

        var ui = new EnvironmentOverridesUi(page, new SettingsPage(page), new EnvironmentOverridesPanel(page));

        await ui.OpenPanel();

        return ui;
    }

    /// <summary>
    /// Reloads the page and opens the panel again. This is what makes an assertion about stored
    /// overrides meaningful: a reload discards everything the browser held, so the panel has to read
    /// the overrides back from the instance.
    /// </summary>
    public async Task Reload()
    {
        await page.ReloadAsync();

        await OpenPanel();
    }

    /// <summary>Adds an override and stores it on the instance.</summary>
    public async Task Store(string name, string value)
    {
        await panel.AddOverride(name, value);
        await settings.ConfirmAndExpectSaved();
    }

    /// <summary>Removes an override and stores the result on the instance.</summary>
    public async Task Remove(string name)
    {
        await panel.DeleteOverride(name);
        await settings.ConfirmAndExpectSaved();
    }

    private async Task OpenPanel()
    {
        await settings.Open();
        await settings.OpenPanel(SettingsPage.SystemCategory, EnvironmentOverridesPanel.PanelTitle);
    }
}
