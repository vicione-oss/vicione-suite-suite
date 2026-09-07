using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the login page (<c>/account/login</c>). Encapsulates selectors and
/// actions so smoke tests don't depend on markup details. Add new account-page
/// interactions here rather than in the tests.
/// </summary>
public sealed partial class LoginPage(IPage page)
{
    public const string Route = "/account/login";

    // The challenge endpoint the passkey element calls before navigator.credentials.get().
    private const string PasskeyChallengeRoute = "/account/passkey-request-options";

    // The 'name' attributes are required for server-side model binding, so they are stable selectors.
    private ILocator UsernameField => page.Locator("input[name='Input.Username']");
    private ILocator PasswordField => page.Locator("input[name='Input.Password']");

    // On a rejected login the offending fields are marked with the 'invalid' CSS class.
    private ILocator InvalidField =>
        page.Locator("input[name='Input.Username'].invalid, input[name='Input.Password'].invalid");

    // Passkey sign-in button. AccountButton renders name="__passkeySubmit"; it is only present when
    // the 'Passkeys' feature is enabled, and disabled when the host is not DNS-capable.
    private ILocator PasskeySignInButton => page.Locator("button[name='__passkeySubmit']");

    [GeneratedRegex("/account/login")]
    private static partial Regex LoginUrl();

    /// <summary>Navigate directly to the login page.</summary>
    public Task Goto() => page.GotoAsync(Route);

    /// <summary>Fill the credentials and submit the (server-rendered) login form.</summary>
    public async Task Login(string userName, string password)
    {
        await UsernameField.FillAsync(userName);
        await PasswordField.FillAsync(password);
        // Submit via Enter so we don't depend on the submit-button markup.
        await PasswordField.PressAsync("Enter");
    }

    /// <summary>Navigates to the login page, signs in and asserts the sign-in succeeded.</summary>
    public async Task SignIn(string userName, string password)
    {
        await Goto();
        await Login(userName, password);
        await ExpectAuthenticated();
    }

    /// <summary>Asserts the browser is on the login page (login did not succeed).</summary>
    public Task ExpectOnLoginPage()
        => Expect(page).ToHaveURLAsync(LoginUrl(), new PageAssertionsToHaveURLOptions { IgnoreCase = true });

    /// <summary>Asserts the login was rejected (a credential field is marked invalid).</summary>
    public Task ExpectRejected() => Expect(InvalidField.First).ToBeVisibleAsync();

    /// <summary>Asserts the user is authenticated (navigated away from the login page).</summary>
    public Task ExpectAuthenticated() => Expect(page).Not.ToHaveURLAsync(LoginUrl());

    /// <summary>
    /// True when passkey sign-in is offered and actionable — i.e. the 'Passkeys' feature is on and
    /// the host is DNS-capable (the button is rendered but disabled on a non-DNS host).
    /// </summary>
    public async Task<bool> IsPasskeySignInAvailable()
        => await PasskeySignInButton.IsVisibleAsync() && await PasskeySignInButton.IsEnabledAsync();

    /// <summary>Trigger passkey login; the virtual authenticator answers navigator.credentials.get().</summary>
    public Task LoginViaPasskey() => PasskeySignInButton.ClickAsync();

    /// <summary>
    /// Enters <paramref name="userName"/>, triggers passkey sign-in and returns the URL of the
    /// challenge request the page issues, so a test can assert which identifier reached the server.
    /// Only the request the click causes is observed, so the conditional-mediation autofill the page
    /// runs on load (with an empty field) cannot be mistaken for it.
    /// </summary>
    public async Task<string> RequestPasskeyChallengeFor(string userName)
    {
        await UsernameField.FillAsync(userName);

        var challengeRequest = await page.RunAndWaitForRequestAsync(
            () => PasskeySignInButton.ClickAsync(),
            request => request.Url.Contains(PasskeyChallengeRoute, StringComparison.OrdinalIgnoreCase));

        return challengeRequest.Url;
    }
}
