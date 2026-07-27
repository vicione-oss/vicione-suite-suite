using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Logs the current user out. Not a real page — the control lives in the top-bar profile flyout
/// (<c>button.sign-out-button</c>). Kept as its own object so tests stay decoupled from where the
/// button physically sits. Sign out POSTs to <c>/account/logout</c> and redirects to the login page.
/// </summary>
public sealed partial class LogoutPage(IPage page)
{
    // Top-bar Profile trigger that opens the flyout. Matched by its user icon, but scoped to the top-bar
    // area so a reuse of that icon elsewhere on the page can't match. Culture-independent (no title).
    private ILocator ProfileTrigger => page.Locator(".top-bar-notification-area button:has(.monochrome-icon-user-light)");

    // CssClass="sign-out-button" (LinkButton in ProfileNotificationElementFlyoutContent.razor).
    private ILocator SignOutButton => page.Locator("button.sign-out-button");

    [GeneratedRegex("/account/login")]
    private static partial Regex LoginUrl();

    /// <summary>Open the profile flyout and sign out, waiting for the redirect to the login page.</summary>
    public async Task Logout()
    {
        await ProfileTrigger.ClickAsync();
        await SignOutButton.ClickAsync();
        await Expect(page).ToHaveURLAsync(LoginUrl(), new() { IgnoreCase = true });
    }
}
