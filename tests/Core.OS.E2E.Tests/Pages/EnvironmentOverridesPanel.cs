using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the "Environment variables" control panel (System category). The panel edits the
/// instance's runtime environment-variable override file through a grid: rows are added, edited and
/// deleted in place, and the whole grid reaches the instance when the settings popup's confirm button
/// is used (see <see cref="SettingsPage"/>).
/// <para>
/// Only the active control panel renders content, so the panel's own <c>environment-overrides-grid</c>
/// element is unique while the panel is open and scopes every row selector. Cells are addressed by
/// the column classes the panel itself sets, and buttons by their icon, so neither depends on a
/// translated label.
/// </para>
/// </summary>
public sealed class EnvironmentOverridesPanel(IPage page)
{
    /// <summary>Title of the panel's entry in the settings navigation.</summary>
    public const string PanelTitle = "Environment variables";

    private ILocator Grid => page.Locator(".environment-overrides-grid");

    private ILocator Table => Grid.Locator("table.quickgrid");

    private ILocator Rows => Grid.Locator("tbody tr");

    private ILocator AddButton =>
        Grid.Locator(".action-buttons button.grid-action-button:has(.monochrome-icon-plus-slim)");

    private ILocator DeleteButton =>
        Grid.Locator(".action-buttons button.grid-action-button:has(.monochrome-icon-delete)");

    // Exactly one row carries text boxes: the one currently open for editing.
    private ILocator EditingRow =>
        Rows.Filter(new LocatorFilterOptions { Has = page.Locator("input.text-box") });

    // The panel's description banner is rendered into the popup's header section, outside the grid.
    private ILocator RiskBanner => page.Locator("aside.description-banner");

    // The in-panel restart action, offered only once overrides were stored.
    private ILocator RestartAction => page.Locator(".control-panel-container .settings-field button");

    private ILocator Row(string name) =>
        Rows.Filter(new LocatorFilterOptions
        {
            Has = page.Locator("td.name-column", new PageLocatorOptions { HasTextRegex = Exactly(name) })
        });

    /// <summary>Asserts the panel shows its editable grid rather than a load failure.</summary>
    public Task ExpectGridVisible() => Expect(Table).ToBeVisibleAsync();

    /// <summary>Asserts the panel warns about what these overrides can do.</summary>
    public Task ExpectRiskBannerVisible() => Expect(RiskBanner).ToBeVisibleAsync();

    /// <summary>Asserts an override named <paramref name="name"/> is listed with <paramref name="value"/>.</summary>
    public Task ExpectOverride(string name, string value)
        => Expect(Row(name).Locator("td.value-column")).ToHaveTextAsync(value);

    /// <summary>Asserts no override named <paramref name="name"/> is listed.</summary>
    public Task ExpectNoOverride(string name) => Expect(Row(name)).ToHaveCountAsync(0);

    /// <summary>Asserts the panel offers to restart the Suite.</summary>
    public Task ExpectRestartOffered() => Expect(RestartAction).ToBeVisibleAsync();

    /// <summary>Asserts the panel does not offer to restart the Suite.</summary>
    public Task ExpectRestartNotOffered() => Expect(RestartAction).ToHaveCountAsync(0);

    /// <summary>
    /// Adds a row and commits its editor. The row is in the grid at this point; it reaches the
    /// instance when the settings popup's confirm button is used.
    /// </summary>
    public async Task AddOverride(string name, string value)
    {
        await AddButton.ClickAsync();

        await Fill(EditingRow.Locator("td.name-column input"), name);
        await Fill(EditingRow.Locator("td.value-column input"), value);

        await EditingRow.Locator("button:has(.monochrome-icon-check)").ClickAsync();

        // A row whose name is rejected stays in edit mode, so a closed editor means the row was taken.
        await Expect(EditingRow).ToHaveCountAsync(0);
    }

    /// <summary>Selects a row and removes it from the grid.</summary>
    public async Task DeleteOverride(string name)
    {
        await Row(name).Locator("td.item-select-column input[type=checkbox]").CheckAsync();

        await Expect(DeleteButton).ToBeEnabledAsync();
        await DeleteButton.ClickAsync();

        await ExpectNoOverride(name);
    }

    // Tab out after filling: the text boxes commit on change, which needs the field to lose focus.
    private static async Task Fill(ILocator input, string value)
    {
        await input.FillAsync(value);
        await input.PressAsync("Tab");
    }

    private static Regex Exactly(string text) => new($"^{Regex.Escape(text)}$");
}
