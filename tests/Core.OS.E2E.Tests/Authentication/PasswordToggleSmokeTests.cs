using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;

namespace Core.OS.E2E.Tests.Authentication;

/// <summary>
/// Smoke tests for the show/hide button of a password field. It is driven from
/// <c>wwwroot/js/text-input-field.js</c> through a delegated listener, so a page with more than
/// one password field must still toggle exactly one field per click — the script is rendered once
/// per field, and a listener attached per rendering would toggle a field twice or not at all.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class PasswordToggleSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    [Fact]
    public async Task Password_is_revealed_and_masked_again_by_the_toggle()
    {
        var login = new LoginPage(Page);
        await login.Goto();
        await login.ExpectPasswordRevealed(false);

        await login.TogglePasswordVisibility();

        await login.ExpectPasswordRevealed(true);

        await login.TogglePasswordVisibility();

        await login.ExpectPasswordRevealed(false);
    }

    [Fact]
    public async Task Every_password_field_on_a_page_has_its_own_toggle()
    {
        var resetPassword = new ResetPasswordPage(Page);
        await resetPassword.Goto();

        await resetPassword.TogglePasswordVisibility(0);

        await resetPassword.ExpectPasswordRevealed(0, true);
        await resetPassword.ExpectPasswordRevealed(1, false);

        for (var fieldIndex = 1; fieldIndex < ResetPasswordPage.PasswordFieldCount; fieldIndex++)
            await resetPassword.TogglePasswordVisibility(fieldIndex);

        for (var fieldIndex = 0; fieldIndex < ResetPasswordPage.PasswordFieldCount; fieldIndex++)
            await resetPassword.ExpectPasswordRevealed(fieldIndex, true);
    }
}
