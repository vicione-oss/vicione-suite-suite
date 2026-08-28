using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the settings popup and its navigation tree. Control panels have no route of
/// their own — the popup is opened from the gear in the top bar, then a category is expanded and a
/// panel picked from it — so every panel test goes through here.
/// <para>
/// Categories and panels are addressed by their display text. A fresh browser context gets
/// <c>en-US</c>: the culture comes from a cookie or the UI host default and falls back to the first
/// supported culture, and the <c>Accept-Language</c> header is not consulted — so the English titles
/// the tests pass are what the UI shows.
/// </para>
/// </summary>
public sealed class SettingsPage(IPage page)
{
    /// <summary>Title of the settings category the instance-level panels live in.</summary>
    public const string SystemCategory = "System";

    /// <summary>How long to wait for the settings gear before concluding it is not offered.</summary>
    private const float GearTimeoutMs = 10_000;

    // Every authenticated page renders the top bar. Used as a positive anchor so that an assertion
    // about something *not* being offered cannot pass just because the UI never rendered at all.
    private ILocator TopBar => page.Locator(".top-bar");

    // The settings gear is a notification element in the top bar, scoped to that area so a reuse of
    // the icon elsewhere cannot match. Its icon (rather than its translated title) identifies it;
    // the light variant is the closed state, which is the only state we click it in.
    private ILocator GearButton =>
        page.Locator(".top-bar-notification-area button:has(.monochrome-icon-gear-light)");

    private ILocator Container => page.Locator(".settings-container");

    // The confirm/cancel toolbar exists only while a panel has an edit running, so its absence is
    // the signal that a save went through.
    private ILocator ConfirmButton => Container.Locator("button.popup-content-action-button--confirm");

    private ILocator SaveErrorMessage =>
        Container.Locator(".popup-content-action-button-container .error-message");

    private ILocator ActivePanel => Container.Locator(".control-panel-container");

    // Each accordion item repeats its category title in a title attribute, which matches exactly
    // without the surrounding whitespace the rendered label carries.
    private ILocator CategoryItem(string title) => Container.Locator($".accordion-item[title=\"{title}\"]");

    // Entries exist only while their category is expanded, and only for categories that hold at
    // least one panel the signed-in user may see.
    private ILocator PanelEntries(string category) => CategoryItem(category).Locator("button.menu-entry");

    private ILocator PanelEntry(string category, string panelTitle) =>
        PanelEntries(category).Filter(new LocatorFilterOptions
        {
            Has = page.Locator(".menu-entry-text", new PageLocatorOptions { HasTextRegex = Exactly(panelTitle) })
        });

    /// <summary>Opens the settings popup from the top bar.</summary>
    public async Task Open()
    {
        await GearButton.ClickAsync();

        await Expect(Container).ToBeVisibleAsync();
    }

    /// <summary>Opens a panel by expanding its category and selecting it.</summary>
    public async Task OpenPanel(string category, string panelTitle)
    {
        await ExpandCategory(category);

        await PanelEntry(category, panelTitle).ClickAsync();

        await Expect(ActivePanel).ToBeVisibleAsync();
    }

    /// <summary>
    /// Asserts a panel cannot be reached by the current user. A user who may not see any panel at
    /// all is not offered the gear in the first place, which is why the absence of the gear counts:
    /// insisting that the popup open would fail for such a user for a reason that has nothing to do
    /// with the panel under test. The top bar is asserted first, so this cannot pass on a UI that
    /// never rendered.
    /// </summary>
    public async Task ExpectPanelNotReachable(string category, string panelTitle)
    {
        await Expect(TopBar).ToBeVisibleAsync();

        if (!await IsGearOffered())
            return;

        await Open();

        // The category is only there when it holds at least one panel this user may see.
        if (await CategoryItem(category).CountAsync() > 0)
            await ExpandCategory(category);

        await Expect(PanelEntry(category, panelTitle)).ToHaveCountAsync(0);
    }

    /// <summary>Confirms the pending edits and asserts they were saved.</summary>
    public async Task ConfirmAndExpectSaved()
    {
        await ConfirmButton.ClickAsync();

        // A save error keeps the edit running, so the toolbar disappearing means the save succeeded.
        await Expect(ConfirmButton).Not.ToBeVisibleAsync();
        await Expect(SaveErrorMessage).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Confirms the pending edits and asserts the save was refused with a message containing
    /// <paramref name="expectedMessagePart"/>.
    /// </summary>
    public async Task ConfirmAndExpectSaveError(string expectedMessagePart)
    {
        await ConfirmButton.ClickAsync();

        await Expect(SaveErrorMessage).ToContainTextAsync(expectedMessagePart);

        // The refused edit is still pending, so the toolbar stays.
        await Expect(ConfirmButton).ToBeVisibleAsync();
    }

    private async Task ExpandCategory(string category)
    {
        var item = CategoryItem(category);

        await Expect(item).ToBeVisibleAsync();

        // The popup opens with one panel preselected, so that panel's category is already expanded —
        // clicking its header would collapse it again.
        if (await PanelEntries(category).CountAsync() == 0)
            await item.Locator("button.header").ClickAsync();
    }

    private async Task<bool> IsGearOffered()
    {
        try
        {
            await GearButton.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = GearTimeoutMs
            });

            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static Regex Exactly(string text) => new($"^{Regex.Escape(text)}$");
}
