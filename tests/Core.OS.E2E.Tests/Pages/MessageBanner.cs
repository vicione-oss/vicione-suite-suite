using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the message banner the layout shows for cross-cutting notices, such as a
/// configuration change that only takes effect after a restart.
/// </summary>
public sealed class MessageBanner(IPage page)
{
    private ILocator Warning => page.Locator(".message-banner-dialog.warning");

    /// <summary>Asserts the banner asks for an application restart.</summary>
    public Task ExpectSuiteRestartRequired()
        => Expect(Warning.Locator(".title")).ToHaveTextAsync("Application restart required");
}
