using Core.OS.E2E.Tests.Infrastructure;
using Core.OS.E2E.Tests.Pages;
using Core.Tests.Tools;
using Xunit;

namespace Core.OS.E2E.Tests.EnvironmentOverrides;

/// <summary>
/// Smoke tests for the "Environment variables" settings panel on a standalone instance.
/// <para>
/// The panel is only offered where overrides are switched on, so the instance under test has to be
/// started with <c>VICIONE_SUITE_ENV_OVERRIDES</c> enabled (the E2E jobs do; see
/// docs/e2e-testing.md).
/// </para>
/// <para>
/// Each test works on its own variable name and removes it again: the override file outlives the
/// test run and is applied to the process environment the next time the instance starts, so a
/// leftover name that means something to the Suite could change how a later run behaves. The
/// <c>E2E_OVERRIDE_*</c> names used here are read by nothing.
/// </para>
/// </summary>
[Collection(E2ECollectionDefinition.Name)]
[Trait(Traits.Category, Traits.E2E)]
public sealed class EnvironmentOverridesSmokeTests(PlaywrightFixture fixture) : E2ETest(fixture)
{
    private readonly string _variableName = $"E2E_OVERRIDE_{Guid.NewGuid():N}".ToUpperInvariant();

    [Fact]
    public async Task Panel_is_offered_to_an_admin_and_renders_its_grid()
    {
        // Act
        var ui = await EnvironmentOverridesUi.Open(Page, TestUsers.UserName, TestUsers.Password);

        // Assert: the grid — rather than the load-failure text — means the stored overrides were read.
        await ui.Panel.ExpectRiskBannerVisible();
        await ui.Panel.ExpectGridVisible();
    }

    [Fact]
    public async Task Stored_override_survives_a_reload_and_can_be_deleted_again()
    {
        // Arrange
        var ui = await EnvironmentOverridesUi.Open(Page, TestUsers.UserName, TestUsers.Password);

        // Act
        await ui.Store(_variableName, "e2e-value");

        // Assert
        await ui.Reload();
        await ui.Panel.ExpectOverride(_variableName, "e2e-value");

        // Act
        await ui.Remove(_variableName);

        // Assert
        await ui.Reload();
        await ui.Panel.ExpectNoOverride(_variableName);
    }

    [Fact]
    public async Task Storing_an_override_asks_for_a_suite_restart()
    {
        // Arrange
        var ui = await EnvironmentOverridesUi.Open(Page, TestUsers.UserName, TestUsers.Password);

        await ui.Panel.ExpectRestartNotOffered();

        // Act
        await ui.Store(_variableName, "e2e-value");

        // Assert: overrides only take effect on the next start, which the instance announces through
        // the shared restart banner and the panel's own restart action. Neither is acted on — the
        // harness keeps the instance under test running.
        await new MessageBanner(Page).ExpectSuiteRestartRequired();
        await ui.Panel.ExpectRestartOffered();

        // Cleanup
        await ui.Remove(_variableName);
    }

    [Fact]
    public async Task Duplicate_variable_names_are_refused_and_nothing_is_stored()
    {
        // Arrange
        var ui = await EnvironmentOverridesUi.Open(Page, TestUsers.UserName, TestUsers.Password);

        // Act: the row editor accepts each name on its own; only the save sees them together.
        await ui.Panel.AddOverride(_variableName, "first");
        await ui.Panel.AddOverride(_variableName, "second");

        // Assert
        await ui.Settings.ConfirmAndExpectSaveError("Duplicate variable names");

        // Assert: the batch is refused as a whole, so nothing reached the instance.
        await ui.Reload();
        await ui.Panel.ExpectNoOverride(_variableName);
    }
}
