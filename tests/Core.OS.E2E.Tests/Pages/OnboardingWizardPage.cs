using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// The first-run onboarding wizard. On a not-yet-onboarded instance, the server redirects a freshly
/// logged-in user to the <c>/onboarding</c> route (OnboardingMiddleware). The wizard covers the whole
/// screen (no top bar), so it must be dismissed before the rest of the app is reachable. Exiting
/// persists "don't show again", so it does not reappear on later logins for the same instance.
///
/// Not a routed target we navigate to on purpose — it appears (or not) depending on instance state,
/// so the only entry point is <see cref="DismissIfShown"/>, which is safe to call unconditionally.
/// </summary>
public sealed partial class OnboardingWizardPage(IPage page)
{
    public const string Route = "/onboarding";

    // Exit button in the wizard's action-button row, matched by its close icon (rendered from
    // IconName=CloseMedium). Culture-independent (no label text) and position-independent (no nth-child),
    // so it survives both a locale switch and the button row changing between wizard steps.
    private ILocator ExitButton =>
        page.Locator(".detail__action-buttons button:has(.monochrome-icon-close-medium)");

    [GeneratedRegex("/onboarding")]
    private static partial Regex OnboardingUrl();

    /// <summary>
    /// If the onboarding wizard is currently shown (we're on its route), exit it and wait until we've
    /// navigated back into the app. No-op otherwise, so it's safe to call right after any login.
    /// </summary>
    public async Task DismissIfShown()
    {
        if (!page.Url.Contains(Route, StringComparison.OrdinalIgnoreCase))
            return;

        await ExitButton.ClickAsync();
        await Expect(page).Not.ToHaveURLAsync(OnboardingUrl());
    }
}
