using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;

namespace Core.OS.E2E.Tests.Authentication;

/// <summary>
/// Full passkey lifecycle: password login, add a passkey, log out, log back in with the passkey.
/// A Playwright virtual authenticator (<see cref="Microsoft.Playwright.IBrowserContext.Credentials"/>)
/// answers the in-page WebAuthn ceremonies, so this runs headless with no real security key.
/// Everything happens in one browser context — the authenticator's stored credential lives there.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class PasskeyLoginSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    [Fact]
    public async Task User_can_add_a_passkey_and_log_in_with_it()
    {
        await MakeSureWebAuthnAuthenticatorIsInstalled();
        await DisablePasskeyAutofillSoTheSignInButtonDrivesLogin();
        var login = await GoToLoginPage();
        await SkipTestIfPasskeyFeatureIsUnavailableOnLoginPage(login);
        await LoginThenCreateAndStorePasskey(login);
        await Logout();
        await AssertPasskeyCanBeUsedToLogIn(login);
    }

    private async Task<LoginPage> GoToLoginPage()
    {
        var login = new LoginPage(Page);
        await login.Goto();
        return login;
    }

    private async Task MakeSureWebAuthnAuthenticatorIsInstalled()
        =>
            // Answers navigator.credentials.create()/get() in-page for this context (Playwright 1.61+).
            await Context.Credentials.InstallAsync();

    private async Task DisablePasskeyAutofillSoTheSignInButtonDrivesLogin()
        =>
            // The login page runs passkey conditional mediation (autofill) on load. Playwright's virtual
            // authenticator answers that navigator.credentials.get() with no user gesture, so once a passkey
            // exists it silently submits the login form the instant the page loads — a flow no real user
            // performs, and one that would otherwise pre-empt the explicit "sign in with passkey" button this
            // test drives. Conditional autofill is also surfaced through the browser's native UI, which
            // Playwright cannot operate the way a user would in any case. Report conditional mediation as
            // unavailable (a legitimate real-browser configuration) so the page skips autofill and the button
            // click stays the thing that logs in.
            await Context.AddInitScriptAsync(
                "if (window.PublicKeyCredential) " +
                "PublicKeyCredential.isConditionalMediationAvailable = () => Promise.resolve(false);");

    private static async Task AssertPasskeyCanBeUsedToLogIn(LoginPage login)
    {
        await login.LoginViaPasskey();
        await login.ExpectAuthenticated();
    }

    private async Task Logout()
    {
        var logout = new LogoutPage(Page);
        await logout.Logout();
    }

    private async Task LoginThenCreateAndStorePasskey(LoginPage login)
    {
        var passkeySettings = new PasskeySettingsPage(Page);

        await login.Login(TestUsers.UserName, TestUsers.Password);
        await login.ExpectAuthenticated();

        await new OnboardingWizardPage(Page).DismissIfShown();

        var passkeyName = $"e2e-passkey-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await passkeySettings.CreatePasskey(passkeyName);

        // The settings dialog is modal and covers the top bar that logout is reached through, so close it.
        await passkeySettings.Close();
    }

    private static async Task SkipTestIfPasskeyFeatureIsUnavailableOnLoginPage(LoginPage login)
    {
        if (!await login.IsPasskeySignInAvailable())
        {
            TestContext.Current.TestOutputHelper?.WriteLine(
                "!!! PASSKEY SMOKE TEST SKIPPED: passkey sign-in is not available on this instance " +
                "(the 'Passkeys' feature is off, or the host is not DNS-capable). The passkey lifecycle " +
                "was NOT exercised.");
            Assert.Skip("Passkey sign-in not available (feature off or non-DNS host) — see test output.");
        }
    }
}
