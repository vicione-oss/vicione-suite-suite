using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;

namespace Core.OS.E2E.Tests.Authentication;

/// <summary>
/// Pins which identifier the login page hands to the passkey challenge endpoint
/// (<c>POST /account/passkey-request-options</c>). The identifier only narrows
/// <c>allowCredentials</c>, so a wrong one still signs in through a discoverable credential — which
/// is why this is asserted on the outgoing request rather than on the sign-in outcome. Neither an
/// authenticator nor a matching account is needed: the challenge is requested before
/// <c>navigator.credentials.get()</c>, and an unknown user just yields an unnarrowed challenge.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class PasskeyChallengeIdentifierTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    // Uses only characters Identity allows in a user name, so nothing but the lookup can reject it.
    private const string EnteredUserName = "e2e.passkey.identifier";

    [Fact]
    public async Task Challenge_is_requested_for_the_entered_user_name()
    {
        var login = await GoToLoginPage();

        var challengeUrl = await login.RequestPasskeyChallengeFor(EnteredUserName);

        challengeUrl.Should().EndWith($"username={EnteredUserName}");
    }

    [Fact]
    public async Task Entered_user_name_is_percent_encoded_in_the_challenge_request()
    {
        var login = await GoToLoginPage();

        // '+' is a legal Identity user name character, and a raw one decodes to a space server-side.
        var challengeUrl = await login.RequestPasskeyChallengeFor("a+b");

        challengeUrl.Should().EndWith("username=a%2Bb");
    }

    private async Task<LoginPage> GoToLoginPage()
    {
        // The page refuses to start any ceremony unless the browser exposes the WebAuthn API, which
        // headless Chromium only does once an authenticator is present. It never has to answer here.
        await Context.Credentials.InstallAsync();

        var login = new LoginPage(Page);
        await login.Goto();
        await SkipTestIfPasskeySignInIsUnavailable(login);
        return login;
    }

    private static async Task SkipTestIfPasskeySignInIsUnavailable(LoginPage login)
    {
        if (!await login.IsPasskeySignInAvailable())
            Assert.Skip("Passkey sign-in not available (the 'Passkeys' feature is off, or a non-DNS host).");
    }
}
