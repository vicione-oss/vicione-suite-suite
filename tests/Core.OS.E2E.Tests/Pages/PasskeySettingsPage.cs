using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the Passkeys profile settings panel. It is opened from the top-bar Settings
/// menu (there is no routed URL): Settings gear → Profile category → Passkeys panel → Add.
/// Confirming the Add panel triggers <c>navigator.credentials.create()</c>, which the Playwright
/// virtual authenticator answers.
///
/// This is interactive Blazor Server UI, so every step relies on Playwright auto-wait rather than
/// fixed delays.
/// </summary>
public sealed class PasskeySettingsPage(IPage page)
{
    // Top-bar Settings trigger. Matched by its gear icon, but scoped to the top-bar area so a reuse of
    // that icon elsewhere on the page can't match. Culture-independent (no title).
    private ILocator SettingsTrigger => page.Locator(".top-bar-notification-area button:has(.monochrome-icon-gear-light)");

    // Profile category in the settings menu. Matched by text: its icon is shared with other entries and
    // is slated to change, so the label is the more stable anchor here. ':has-text' is a case-insensitive
    // substring match, so the "Profil" stem covers both English ("Profile") and German ("Profil").
    private ILocator ProfileCategory => page.Locator("button.header:has-text(\"Profil\")");

    // Passkeys menu entry. No icon to anchor on, so this one is matched by text. The "Passkey" stem
    // (case-insensitive substring) covers singular/plural and a German rendering should it be translated.
    private ILocator PasskeysPanelNav => page.Locator("button.menu-entry:has-text(\"Passkey\")");

    // Grid "Add" action button, one of several grid-action-buttons (Add/Edit/Delete). Matched by its
    // plus icon, which distinguishes it culture-independently from its siblings' "Add" title/label.
    private ILocator AddButton =>
        page.Locator(".action-buttons button.grid-action-button:has(.monochrome-icon-plus-slim)");

    // Name field: SettingsFieldTextBox with Placeholder = CommonVocabulary.Name ("Name" / localized).
    private ILocator NameField => page.GetByPlaceholder("Name");

    // Confirm/save button that commits the Add panel. Matched by its '--confirm' modifier class:
    // culture-independent and specific to the confirm variant. Only rendered once the name field
    // loses focus (see CreatePasskey), so it must be blurred before this can be clicked.
    private ILocator ConfirmButton => page.Locator("button.popup-content-action-button--confirm");

    // Close (X) in the settings dialog's title bar. Same close icon as the wizard's exit, so it's
    // scoped to this popup's own 'popup-action-button' class to keep the two distinct.
    private ILocator CloseButton => page.Locator("button.popup-action-button:has(.monochrome-icon-close-medium)");

    // Back arrow in the content header. It only becomes visible once the carousel has committed the
    // drill-down to a pushed panel (the Add panel here), which is the signal that the panel switch has
    // fully settled. Used to gate confirming so we never save while the switch is still mid-flight.
    private ILocator NavigateBackButton => page.Locator("button.navigate-back-button");

    /// <summary>
    /// Open Settings gear → Profile → Passkeys, add a passkey with the given name and confirm. Asserts
    /// the new passkey appears in the grid.
    /// </summary>
    public async Task CreatePasskey(string name)
    {
        await SettingsTrigger.ClickAsync();
        await ProfileCategory.ClickAsync();
        await PasskeysPanelNav.ClickAsync();

        await AddButton.ClickAsync();
        // The carousel commits the switch to the Add panel on a delayed post-render step; the back arrow
        // appearing marks that commit. Waiting for it keeps the (near-instant, virtual-authenticator) confirm
        // from racing ahead of the commit, which would otherwise drop the follow-up navigate-back.
        await Expect(NavigateBackButton).ToBeVisibleAsync();
        await NameField.FillAsync(name);
        // The Confirm button only appears once the name field loses focus, so tab out to reveal it.
        await NameField.PressAsync("Tab");
        // Commits the Add panel -> SuitePasskeys.ObtainAndCreateCredentials -> navigator.credentials.create().
        await ConfirmButton.ClickAsync();

        // The created passkey appears as a row in the QuickGrid; assert its Name cell (property-column)
        // shows up. Scoped to the grid so it's a row assertion, not just "this text exists on the page".
        await Expect(page.Locator("table.quickgrid td.property-column").Filter(new() { HasTextString = name }))
            .ToBeVisibleAsync();
    }

    /// <summary>Close the settings dialog so the app behind it (e.g. the top bar) is reachable again.</summary>
    public Task Close() => CloseButton.ClickAsync();
}
