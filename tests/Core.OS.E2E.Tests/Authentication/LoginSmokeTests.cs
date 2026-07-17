using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Xunit;

namespace Core.OS.E2E.Tests.Authentication;

/// <summary>
/// Smoke tests around the login flow. Each test runs in its own isolated browser context
/// (see <see cref="E2ETest"/>). The rejection test logs in with a non-existent user so
/// that lockout-on-failure can never affect the seeded accounts other tests rely on.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class LoginSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    [Fact]
    public async Task Anonymous_user_is_redirected_to_login()
    {
        // Act: a fresh context is not authenticated, so the root route is gated.
        await Page.GotoAsync("/");

        // Assert
        await new LoginPage(Page).ExpectOnLoginPage();
    }

    [Fact]
    public async Task Seeded_user_can_log_in()
    {
        // Arrange
        var login = new LoginPage(Page);
        await login.Goto();

        // Act
        await login.Login(TestUsers.UserName, TestUsers.Password);

        // Assert
        await login.ExpectAuthenticated();
    }

    [Fact]
    public async Task Invalid_credentials_are_rejected()
    {
        // Arrange
        var login = new LoginPage(Page);
        await login.Goto();

        // Act: a non-existent user — wrong credentials cannot lock a real (seeded) account.
        await login.Login("nonexistent-user", "WrongPassword123!");

        // Assert
        await login.ExpectRejected();
        await login.ExpectOnLoginPage();
    }
}
