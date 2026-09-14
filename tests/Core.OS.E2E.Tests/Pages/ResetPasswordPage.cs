using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Core.OS.E2E.Tests.Pages;

/// <summary>
/// Page object for the reset-password page (<c>/account/reset-password</c>). It is anonymous and
/// renders without a reset code, and it is the only account page carrying an even number of
/// password fields — which is what makes it the page to exercise the show/hide button on.
/// </summary>
public sealed class ResetPasswordPage(IPage page)
{
    public const string Route = "/account/reset-password";

    /// <summary>The 'name' attributes are required for model binding, so they are stable selectors.</summary>
    private static readonly string[] PasswordFieldNames = ["Input.NewPassword", "Input.ConfirmNewPassword"];

    public static int PasswordFieldCount => PasswordFieldNames.Length;

    /// <summary>Navigate directly to the reset-password page.</summary>
    public Task Goto() => page.GotoAsync(Route);

    /// <summary>Clicks the show/hide button belonging to the given password field.</summary>
    public async Task TogglePasswordVisibility(int fieldIndex)
        => await (await PasswordToggle(fieldIndex)).ClickAsync();

    /// <summary>Asserts whether the given password field reveals its value.</summary>
    public Task ExpectPasswordRevealed(int fieldIndex, bool revealed)
        => Expect(PasswordField(fieldIndex)).ToHaveAttributeAsync("type", revealed ? "text" : "password");

    private ILocator PasswordField(int fieldIndex)
        => page.Locator($"input[name='{PasswordFieldNames[fieldIndex]}']");

    /// <summary>
    /// Resolved through the id of the input rather than by position, so the test fails if the
    /// button stops pointing at its own field.
    /// </summary>
    private async Task<ILocator> PasswordToggle(int fieldIndex)
    {
        var inputId = await PasswordField(fieldIndex).GetAttributeAsync("id");

        return page.Locator($"button[data-toggle-password='{inputId}']");
    }
}
