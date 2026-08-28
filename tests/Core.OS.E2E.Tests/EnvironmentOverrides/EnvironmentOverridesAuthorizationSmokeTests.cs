using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Xunit;

namespace Core.OS.E2E.Tests.EnvironmentOverrides;

/// <summary>
/// The environment-overrides panel may set any variable the Suite starts with, so it is restricted
/// to users with full access. This test signs in as a seeded account without full access
/// (<see cref="TestUsers.NonAdminUserName"/>) and asserts the panel is not reachable for it.
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class EnvironmentOverridesAuthorizationSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    [Fact]
    public async Task Panel_is_not_offered_to_a_user_without_full_access()
    {
        // Arrange
        await new LoginPage(Page).SignIn(TestUsers.NonAdminUserName, TestUsers.NonAdminPassword);

        // Act + Assert
        await new SettingsPage(Page)
            .ExpectPanelNotReachable(SettingsPage.SystemCategory, EnvironmentOverridesPanel.PanelTitle);
    }
}
